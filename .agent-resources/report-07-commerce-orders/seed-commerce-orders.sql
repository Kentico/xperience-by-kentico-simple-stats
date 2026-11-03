-- Seeds digital commerce customers and orders for visual testing of the "Orders and revenue" and "Customers" reports.
-- LOCAL DEV DB ONLY (DancingGoat example). Never run on a real project.
--
-- Orders: ~350 orders over the last 120 days: weekday/weekend rhythm, a promo spike (days 40-45 back), a few days with no orders,
-- statuses spread (mostly Fulfilled / Payment received; recent orders more often Pending / Processing),
-- both payment and shipping methods (display names copied from the method tables),
-- 1-4 items per order from real DancingGoat products (SKU, name, price from content items).
-- Totals are consistent: item total = qty x unit price, item tax = item total x tax rate,
-- order total = sum of item totals, grand total = total + shipping + tax.
--
-- Customers: ~150. Most order once or twice; new customers follow the orders (so the promo spike brings a spike of new customers).
-- Customer created = shortly before their first order. 12 customers (plus two heavy buyers) were created 150-300 days ago
-- ("returning" customers), 8 customers never ordered. Heavy buyers so the top lists differ:
-- #1 many cheap orders, #2 a few big orders, #3 high item quantities, #4 and #5 frequent buyers.
-- A few customers have no name (email only) or no name and no email.
-- Addresses: every order has a billing address (customer's home location), most have a shipping address; some ship elsewhere.
-- Locations: USA (several states), Canada, United Kingdom, Germany, Czech Republic, France, Netherlands, Australia
-- (by two-letter code; only countries that have states in CMS_State get a state), and a few without a country ("Unknown").
-- Address types use the product's stored names (OrderAddressType.Billing / Shipping: 'billing', 'shipping').
-- Values are pseudo-random but deterministic (CHECKSUM of fixed strings), dates move with today.
--
-- Seeded orders have OrderNumber 'SEED-%', seeded customers CustomerPhone 'SEED-%'. Re-running deletes them
-- (and their items, addresses, promotions, and customers of seeded orders without other orders) first.
--
-- Run (Git Bash):
--   MSYS_NO_PATHCONV=1 docker exec -i mssql2022 /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P '<password>' \
--     -d xperience-by-kentico-simple-stats -b < .agent-resources/report-07-commerce-orders/seed-commerce-orders.sql

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'[Commerce_Order]') IS NULL
BEGIN
    PRINT 'Commerce tables not found; nothing to seed.';
    RETURN;
END;

DECLARE @Pattern nvarchar(20) = N'SEED-%';
DECLARE @TaxRate decimal(19, 4) = 21; -- percent, as DancingGoatTaxRateConstants.TAX_RATE
DECLARE @Today date = CAST(GETDATE() AS date);
DECLARE @Billing nvarchar(20) = N'billing';   -- OrderAddressType.Billing.Name
DECLARE @Shipping nvarchar(20) = N'shipping'; -- OrderAddressType.Shipping.Name

BEGIN TRANSACTION;

-- 1. Remove previously seeded data.
DECLARE @OldCustomers TABLE ([CustomerID] int PRIMARY KEY);
INSERT INTO @OldCustomers
SELECT DISTINCT [OrderCustomerID] FROM [Commerce_Order] WHERE [OrderNumber] LIKE @Pattern
UNION
SELECT [CustomerID] FROM [Commerce_Customer] WHERE [CustomerPhone] LIKE @Pattern;

DELETE P FROM [Commerce_OrderPromotion] P
    JOIN [Commerce_Order] O ON O.[OrderID] = P.[OrderPromotionOrderID]
    WHERE O.[OrderNumber] LIKE @Pattern;
DELETE A FROM [Commerce_OrderAddress] A
    JOIN [Commerce_Order] O ON O.[OrderID] = A.[OrderAddressOrderID]
    WHERE O.[OrderNumber] LIKE @Pattern;
DELETE I FROM [Commerce_OrderItem] I
    JOIN [Commerce_Order] O ON O.[OrderID] = I.[OrderItemOrderID]
    WHERE O.[OrderNumber] LIKE @Pattern;
DELETE FROM [Commerce_Order] WHERE [OrderNumber] LIKE @Pattern;
DELETE C FROM [Commerce_Customer] C
    JOIN @OldCustomers S ON S.[CustomerID] = C.[CustomerID]
    WHERE NOT EXISTS (SELECT 1 FROM [Commerce_Order] O WHERE O.[OrderCustomerID] = C.[CustomerID])
        AND NOT EXISTS (SELECT 1 FROM [Commerce_CustomerAddress] CA WHERE CA.[CustomerAddressCustomerID] = C.[CustomerID]);

-- 2. Lookups (by code name, so IDs are not assumed). Missing statuses fall back to the first status.
DECLARE @Fallback int = (SELECT TOP (1) [OrderStatusID] FROM [Commerce_OrderStatus] ORDER BY [OrderStatusOrder]);
IF @Fallback IS NULL
BEGIN
    PRINT 'No order statuses; nothing to seed.';
    ROLLBACK TRANSACTION;
    RETURN;
END;

DECLARE @Fulfilled int = ISNULL((SELECT [OrderStatusID] FROM [Commerce_OrderStatus] WHERE [OrderStatusName] = N'Fulfilled'), @Fallback);
DECLARE @Received int = ISNULL((SELECT [OrderStatusID] FROM [Commerce_OrderStatus] WHERE [OrderStatusName] = N'PaymentReceived'), @Fallback);
DECLARE @Processing int = ISNULL((SELECT [OrderStatusID] FROM [Commerce_OrderStatus] WHERE [OrderStatusName] = N'Processing'), @Fallback);
DECLARE @Pending int = ISNULL((SELECT [OrderStatusID] FROM [Commerce_OrderStatus] WHERE [OrderStatusName] = N'Pending'), @Fallback);
DECLARE @Failed int = ISNULL((SELECT [OrderStatusID] FROM [Commerce_OrderStatus] WHERE [OrderStatusName] = N'PaymentFailed'), @Fallback);

IF OBJECT_ID('tempdb..#Payment') IS NOT NULL DROP TABLE #Payment;
SELECT ROW_NUMBER() OVER (ORDER BY [PaymentMethodID]) - 1 AS [Idx], [PaymentMethodID], [PaymentMethodDisplayName]
INTO #Payment FROM [Commerce_PaymentMethod];

IF OBJECT_ID('tempdb..#Shipping') IS NOT NULL DROP TABLE #Shipping;
SELECT ROW_NUMBER() OVER (ORDER BY [ShippingMethodID]) - 1 AS [Idx], [ShippingMethodID], [ShippingMethodDisplayName], ISNULL([ShippingMethodPrice], 0) AS [Price]
INTO #Shipping FROM [Commerce_ShippingMethod];

DECLARE @PaymentCount int = (SELECT COUNT(*) FROM #Payment);
DECLARE @ShippingCount int = (SELECT COUNT(*) FROM #Shipping);

-- Products with a price (latest content item data), weighted so cheap items sell more often.
IF OBJECT_ID('tempdb..#Products') IS NOT NULL DROP TABLE #Products;
WITH [P] AS (
    SELECT [ProductSKUCode] AS [Sku], MAX([ProductFieldName]) AS [Name], MAX([ProductFieldPrice]) AS [Price]
    FROM [CMS_ContentItemCommonData]
    WHERE [ProductSKUCode] IS NOT NULL AND [ProductSKUCode] <> N'' AND [ProductFieldPrice] > 0 AND [ContentItemCommonDataIsLatest] = 1
    GROUP BY [ProductSKUCode]
), [W] AS (
    SELECT *, CASE WHEN [Price] >= 400 THEN 1 WHEN [Price] >= 50 THEN 3 WHEN [Price] >= 10 THEN 6 ELSE 10 END AS [Weight]
    FROM [P]
)
SELECT [Sku], [Name], [Price], [Weight],
    SUM([Weight]) OVER (ORDER BY [Sku] ROWS UNBOUNDED PRECEDING) - [Weight] AS [Lo]
INTO #Products FROM [W];

DECLARE @TotalWeight int = (SELECT SUM([Weight]) FROM #Products);
IF @TotalWeight IS NULL OR @PaymentCount = 0 OR @ShippingCount = 0
BEGIN
    PRINT 'Missing products, payment or shipping methods; nothing to seed.';
    ROLLBACK TRANSACTION;
    RETURN;
END;

IF OBJECT_ID('tempdb..#Numbers') IS NOT NULL DROP TABLE #Numbers;
SELECT TOP (400) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS [N] INTO #Numbers FROM sys.all_objects;

-- 3. Orders per day: weekdays 2-4, weekends 0-2, promo spike 40-45 days back, some empty days.
IF OBJECT_ID('tempdb..#Days') IS NOT NULL DROP TABLE #Days;
SELECT D.[DaysBack], D.[Day],
    CASE
        WHEN ABS(CHECKSUM(CONCAT(N'empty', D.[DaysBack]))) % 17 = 0 THEN 0
        WHEN D.[DaysBack] BETWEEN 40 AND 45 THEN 8 + ABS(CHECKSUM(CONCAT(N'promo', D.[DaysBack]))) % 6
        WHEN DATEDIFF(day, '19000101', D.[Day]) % 7 >= 5 THEN ABS(CHECKSUM(CONCAT(N'we', D.[DaysBack]))) % 3
        ELSE 2 + ABS(CHECKSUM(CONCAT(N'wd', D.[DaysBack]))) % 3
    END AS [OrderCount]
INTO #Days
FROM (
    SELECT [N] - 1 AS [DaysBack], DATEADD(day, -([N] - 1), @Today) AS [Day] FROM #Numbers WHERE [N] <= 120
) D;

IF OBJECT_ID('tempdb..#Orders') IS NOT NULL DROP TABLE #Orders;
SELECT
    ROW_NUMBER() OVER (ORDER BY D.[Day], N.[N]) AS [Seq],
    D.[DaysBack],
    DATEADD(minute, 8 * 60 + ABS(CHECKSUM(CONCAT(N'time', D.[DaysBack], N'-', N.[N]))) % (14 * 60), CAST(D.[Day] AS datetime2)) AS [CreatedWhen],
    ABS(CHECKSUM(CONCAT(N'status', D.[DaysBack], N'-', N.[N]))) % 100 AS [StatusRoll],
    ABS(CHECKSUM(CONCAT(N'pay', D.[DaysBack], N'-', N.[N]))) % 10 AS [PayRoll],
    ABS(CHECKSUM(CONCAT(N'ship', D.[DaysBack], N'-', N.[N]))) % 10 AS [ShipRoll],
    ABS(CHECKSUM(CONCAT(N'items', D.[DaysBack], N'-', N.[N]))) % 10 AS [ItemRoll],
    CAST(NULL AS int) AS [CustomerIdx]
INTO #Orders
FROM #Days D
JOIN #Numbers N ON N.[N] <= D.[OrderCount];

-- Order lines: 1-4 candidate lines, merged per SKU.
IF OBJECT_ID('tempdb..#Lines') IS NOT NULL DROP TABLE #Lines;
WITH [Candidates] AS (
    SELECT O.[Seq], L.[N] AS [Line],
        ABS(CHECKSUM(CONCAT(N'product', O.[Seq], N'-', L.[N]))) % @TotalWeight AS [ProductRoll],
        ABS(CHECKSUM(CONCAT(N'qty', O.[Seq], N'-', L.[N]))) % 3 AS [QtyRoll]
    FROM #Orders O
    JOIN #Numbers L ON L.[N] <= CASE WHEN O.[ItemRoll] < 4 THEN 1 WHEN O.[ItemRoll] < 7 THEN 2 WHEN O.[ItemRoll] < 9 THEN 3 ELSE 4 END
)
SELECT C.[Seq], P.[Sku], P.[Name], P.[Price],
    CAST(SUM(CASE WHEN P.[Price] < 10 THEN 1 + C.[QtyRoll] ELSE 1 END) AS decimal(19, 4)) AS [Quantity]
INTO #Lines
FROM [Candidates] C
JOIN #Products P ON C.[ProductRoll] >= P.[Lo] AND C.[ProductRoll] < P.[Lo] + P.[Weight]
GROUP BY C.[Seq], P.[Sku], P.[Name], P.[Price];

IF OBJECT_ID('tempdb..#OrderTotals') IS NOT NULL DROP TABLE #OrderTotals;
SELECT [Seq],
    SUM(ROUND([Quantity] * [Price], 2)) AS [TotalPrice],
    SUM(ROUND(ROUND([Quantity] * [Price], 2) * @TaxRate / 100, 2)) AS [TotalTax],
    SUM([Quantity]) AS [Quantity]
INTO #OrderTotals
FROM #Lines
GROUP BY [Seq];

-- 4. Customer of each order. Indexes: 1-5 heavy buyers, 6-17 customers created before the orders, 18+ new customers.
-- #2: the 6 biggest orders; #3: the 12 orders with the most items; #1: the 25 cheapest orders; #4, #5: every 11th / 19th order.
UPDATE O SET [CustomerIdx] = 2
FROM #Orders O JOIN (SELECT TOP (6) [Seq] FROM #OrderTotals ORDER BY [TotalPrice] DESC, [Seq]) X ON X.[Seq] = O.[Seq];

UPDATE O SET [CustomerIdx] = 3
FROM #Orders O JOIN (
    SELECT TOP (12) T.[Seq] FROM #OrderTotals T JOIN #Orders O2 ON O2.[Seq] = T.[Seq]
    WHERE O2.[CustomerIdx] IS NULL ORDER BY T.[Quantity] DESC, T.[Seq]
) X ON X.[Seq] = O.[Seq];

UPDATE O SET [CustomerIdx] = 1
FROM #Orders O JOIN (
    SELECT TOP (25) T.[Seq] FROM #OrderTotals T JOIN #Orders O2 ON O2.[Seq] = T.[Seq]
    WHERE O2.[CustomerIdx] IS NULL ORDER BY T.[TotalPrice], T.[Seq]
) X ON X.[Seq] = O.[Seq];

UPDATE #Orders SET [CustomerIdx] = 4 WHERE [CustomerIdx] IS NULL AND [Seq] % 11 = 5;
UPDATE #Orders SET [CustomerIdx] = 5 WHERE [CustomerIdx] IS NULL AND [Seq] % 19 = 3;

-- Other orders, in date order: about half from a new customer, the rest from a customer who ordered before (or an older customer).
WITH [R] AS (
    SELECT [Seq], CASE WHEN ABS(CHECKSUM(CONCAT(N'new', [Seq]))) % 100 < 52 THEN 1 ELSE 0 END AS [IsNew]
    FROM #Orders WHERE [CustomerIdx] IS NULL
), [R2] AS (
    SELECT [Seq], [IsNew], SUM([IsNew]) OVER (ORDER BY [Seq] ROWS UNBOUNDED PRECEDING) AS [NewSoFar]
    FROM [R]
)
UPDATE O SET [CustomerIdx] =
    CASE WHEN R2.[IsNew] = 1 THEN 17 + R2.[NewSoFar] ELSE 6 + ABS(CHECKSUM(CONCAT(N'repeat', O.[Seq]))) % (12 + R2.[NewSoFar]) END
FROM #Orders O JOIN [R2] ON R2.[Seq] = O.[Seq];

-- 5. Customers: every index with orders, plus 8 customers without orders.
DECLARE @MaxIdx int = (SELECT MAX([CustomerIdx]) FROM #Orders);

IF OBJECT_ID('tempdb..#FirstNames') IS NOT NULL DROP TABLE #FirstNames;
SELECT [Idx] - 1 AS [Idx], [Name] INTO #FirstNames FROM (VALUES
    (1, N'Emma'), (2, N'Liam'), (3, N'Olivia'), (4, N'Noah'), (5, N'Ava'), (6, N'Lucas'), (7, N'Mia'), (8, N'Jakub'),
    (9, N'Sophie'), (10, N'Ben'), (11, N'Chloe'), (12, N'Ethan'), (13, N'Hannah'), (14, N'Leon'), (15, N'Tereza'), (16, N'Oscar'),
    (17, N'Grace'), (18, N'Felix'), (19, N'Zoe'), (20, N'Adam'), (21, N'Lily'), (22, N'Max'), (23, N'Ella'), (24, N'Jonas')) V([Idx], [Name]);

IF OBJECT_ID('tempdb..#LastNames') IS NOT NULL DROP TABLE #LastNames;
SELECT [Idx] - 1 AS [Idx], [Name] INTO #LastNames FROM (VALUES
    (1, N'Smith'), (2, N'Novak'), (3, N'Mueller'), (4, N'Brown'), (5, N'Tremblay'), (6, N'Wilson'), (7, N'Dvorak'), (8, N'Schmidt'),
    (9, N'Taylor'), (10, N'Roy'), (11, N'Evans'), (12, N'Martin'), (13, N'Jansen'), (14, N'Walker'), (15, N'Svoboda'), (16, N'Fischer'),
    (17, N'Clark'), (18, N'Lee'), (19, N'Dubois'), (20, N'King'), (21, N'Weber'), (22, N'Hall'), (23, N'Bakker'), (24, N'Moore'), (25, N'Young')) V([Idx], [Name]);

IF OBJECT_ID('tempdb..#NewCustomers') IS NOT NULL DROP TABLE #NewCustomers;
SELECT
    N.[N] AS [Idx],
    -- Every 37th customer has only an email, customer 23 has neither name nor email.
    CASE WHEN N.[N] % 37 = 0 OR N.[N] = 23 THEN NULL ELSE F.[Name] END AS [FirstName],
    CASE WHEN N.[N] % 37 = 0 OR N.[N] = 23 THEN NULL ELSE L.[Name] END AS [LastName],
    CASE WHEN N.[N] = 23 THEN NULL
        ELSE LOWER(CONCAT(F.[Name], N'.', L.[Name], N.[N], N'@example.com')) END AS [Email],
    CASE
        WHEN F2.[FirstOrder] IS NULL AND N.[N] > @MaxIdx
            THEN DATEADD(minute, 9 * 60 + ABS(CHECKSUM(CONCAT(N'noorder', N.[N]))) % 600,
                CAST(DATEADD(day, -(ABS(CHECKSUM(CONCAT(N'noorderday', N.[N]))) % 110), @Today) AS datetime2))
        WHEN N.[N] BETWEEN 6 AND 17 OR N.[N] IN (1, 4) OR F2.[FirstOrder] IS NULL
            THEN DATEADD(minute, 9 * 60 + ABS(CHECKSUM(CONCAT(N'oldtime', N.[N]))) % 600,
                CAST(DATEADD(day, -(150 + ABS(CHECKSUM(CONCAT(N'oldday', N.[N]))) % 150), @Today) AS datetime2))
        ELSE DATEADD(minute, -(5 + ABS(CHECKSUM(CONCAT(N'signup', N.[N]))) % 240), F2.[FirstOrder])
    END AS [CreatedWhen],
    NEWID() AS [Guid]
INTO #NewCustomers
FROM #Numbers N
JOIN #FirstNames F ON F.[Idx] = N.[N] % 24
JOIN #LastNames L ON L.[Idx] = (N.[N] * 7) % 25
LEFT JOIN (SELECT [CustomerIdx], MIN([CreatedWhen]) AS [FirstOrder] FROM #Orders GROUP BY [CustomerIdx]) F2 ON F2.[CustomerIdx] = N.[N]
WHERE N.[N] <= @MaxIdx + 8;

INSERT INTO [Commerce_Customer] ([CustomerGUID], [CustomerMemberID], [CustomerCreatedWhen], [CustomerFirstName], [CustomerLastName], [CustomerEmail], [CustomerPhone])
SELECT [Guid], NULL, [CreatedWhen], [FirstName], [LastName], [Email], CONCAT(N'SEED-', RIGHT(CONCAT(N'0000', [Idx]), 4))
FROM #NewCustomers;

IF OBJECT_ID('tempdb..#Customers') IS NOT NULL DROP TABLE #Customers;
SELECT NC.[Idx], C.[CustomerID], NC.[FirstName], NC.[LastName], NC.[Email]
INTO #Customers
FROM #NewCustomers NC
JOIN [Commerce_Customer] C ON C.[CustomerGUID] = NC.[Guid];

-- 6. Home location per customer (billing address). Countries by two-letter code; US customers get a weighted state.
IF OBJECT_ID('tempdb..#UsStates') IS NOT NULL DROP TABLE #UsStates;
SELECT S.[StateID], S.[StateCode], V.[Weight], SUM(V.[Weight]) OVER (ORDER BY V.[Code] ROWS UNBOUNDED PRECEDING) - V.[Weight] AS [Lo]
INTO #UsStates
FROM (VALUES (N'CA', 8), (N'NY', 6), (N'TX', 6), (N'FL', 4), (N'WA', 3), (N'IL', 3), (N'MA', 2), (N'CO', 2), (N'GA', 2), (N'OR', 1), (N'OH', 1), (N'MN', 1)) V([Code], [Weight])
JOIN [CMS_State] S ON S.[StateCode] = V.[Code]
JOIN [CMS_Country] C ON C.[CountryID] = S.[CountryID] AND C.[CountryTwoLetterCode] = N'US';

DECLARE @StateWeight int = (SELECT SUM([Weight]) FROM #UsStates);

IF OBJECT_ID('tempdb..#Homes') IS NOT NULL DROP TABLE #Homes;
WITH [H] AS (
    SELECT C.[Idx],
        CASE
            WHEN R.[Roll] < 44 THEN N'US' WHEN R.[Roll] < 54 THEN N'CA' WHEN R.[Roll] < 66 THEN N'GB' WHEN R.[Roll] < 76 THEN N'DE'
            WHEN R.[Roll] < 84 THEN N'CZ' WHEN R.[Roll] < 88 THEN N'FR' WHEN R.[Roll] < 92 THEN N'NL' WHEN R.[Roll] < 95 THEN N'AU'
            ELSE NULL
        END AS [Code],
        ABS(CHECKSUM(CONCAT(N'state', C.[Idx]))) % ISNULL(@StateWeight, 1) AS [StateRoll]
    FROM #Customers C
    CROSS APPLY (SELECT ABS(CHECKSUM(CONCAT(N'loc', C.[Idx]))) % 100 AS [Roll]) R
)
SELECT H.[Idx], CT.[CountryID], CASE WHEN H.[Code] = N'US' THEN S.[StateID] END AS [StateID],
    CASE H.[Code] WHEN N'US' THEN N'Springfield' WHEN N'CA' THEN N'Toronto' WHEN N'GB' THEN N'London' WHEN N'DE' THEN N'Berlin'
        WHEN N'CZ' THEN N'Brno' WHEN N'FR' THEN N'Lyon' WHEN N'NL' THEN N'Utrecht' WHEN N'AU' THEN N'Melbourne' ELSE N'Unknown' END AS [City]
INTO #Homes
FROM [H]
LEFT JOIN [CMS_Country] CT ON CT.[CountryTwoLetterCode] = H.[Code]
LEFT JOIN #UsStates S ON H.[StateRoll] >= S.[Lo] AND H.[StateRoll] < S.[Lo] + S.[Weight];

-- 7. Orders, items and addresses.
INSERT INTO [Commerce_Order] (
    [OrderGUID], [OrderNumber], [OrderCreatedWhen], [OrderModifiedWhen], [OrderOrderStatusID],
    [OrderTotalPrice], [OrderTotalShipping], [OrderTotalTax], [OrderGrandTotal], [OrderCustomerID],
    [OrderPaymentMethodID], [OrderPaymentMethodDisplayName], [OrderShippingMethodID], [OrderShippingMethodDisplayName], [OrderShippingMethodPrice])
SELECT
    NEWID(),
    CONCAT(N'SEED-', RIGHT(CONCAT(N'00000', O.[Seq]), 5)),
    O.[CreatedWhen],
    O.[CreatedWhen],
    CASE
        WHEN O.[DaysBack] >= 10 THEN
            CASE WHEN O.[StatusRoll] < 62 THEN @Fulfilled WHEN O.[StatusRoll] < 86 THEN @Received WHEN O.[StatusRoll] < 92 THEN @Processing WHEN O.[StatusRoll] < 96 THEN @Pending ELSE @Failed END
        ELSE
            CASE WHEN O.[StatusRoll] < 20 THEN @Fulfilled WHEN O.[StatusRoll] < 45 THEN @Received WHEN O.[StatusRoll] < 70 THEN @Processing WHEN O.[StatusRoll] < 93 THEN @Pending ELSE @Failed END
    END,
    T.[TotalPrice],
    S.[Price],
    T.[TotalTax],
    T.[TotalPrice] + S.[Price] + T.[TotalTax],
    C.[CustomerID],
    P.[PaymentMethodID], P.[PaymentMethodDisplayName],
    S.[ShippingMethodID], S.[ShippingMethodDisplayName], S.[Price]
FROM #Orders O
JOIN #OrderTotals T ON T.[Seq] = O.[Seq]
JOIN #Customers C ON C.[Idx] = O.[CustomerIdx]
JOIN #Payment P ON P.[Idx] = CASE WHEN O.[PayRoll] < 7 THEN 0 ELSE 1 END % @PaymentCount
JOIN #Shipping S ON S.[Idx] = CASE WHEN O.[ShipRoll] < 7 THEN 1 ELSE 0 END % @ShippingCount;

INSERT INTO [Commerce_OrderItem] (
    [OrderItemGUID], [OrderItemOrderID], [OrderItemSKU], [OrderItemName],
    [OrderItemQuantity], [OrderItemUnitPrice], [OrderItemTotalPrice], [OrderItemTotalTax], [OrderItemTaxRate])
SELECT
    NEWID(), O.[OrderID], L.[Sku], L.[Name],
    L.[Quantity], L.[Price], ROUND(L.[Quantity] * L.[Price], 2), ROUND(ROUND(L.[Quantity] * L.[Price], 2) * @TaxRate / 100, 2), @TaxRate
FROM #Lines L
JOIN [Commerce_Order] O ON O.[OrderNumber] = CONCAT(N'SEED-', RIGHT(CONCAT(N'00000', L.[Seq]), 5));

IF OBJECT_ID('tempdb..#Addresses') IS NOT NULL DROP TABLE #Addresses;
SELECT O.[OrderID], C.[FirstName], C.[LastName], C.[Email], H.[Idx] AS [CustomerIdx],
    ABS(CHECKSUM(CONCAT(N'hasship', O.[OrderNumber]))) % 100 AS [ShipRoll],
    ABS(CHECKSUM(CONCAT(N'elsewhere', O.[OrderNumber]))) % 100 AS [ElsewhereRoll]
INTO #Addresses
FROM [Commerce_Order] O
JOIN #Customers C ON C.[CustomerID] = O.[OrderCustomerID]
JOIN #Homes H ON H.[Idx] = C.[Idx]
WHERE O.[OrderNumber] LIKE @Pattern;

-- Billing = home. Shipping for 85% of orders; 18% of those go to another customer's home location (gifts, offices).
INSERT INTO [Commerce_OrderAddress] (
    [OrderAddressGUID], [OrderAddressOrderID], [OrderAddressType], [OrderAddressFirstName], [OrderAddressLastName],
    [OrderAddressEmail], [OrderAddressLine1], [OrderAddressCity], [OrderAddressZip], [OrderAddressCountryID], [OrderAddressStateID])
SELECT NEWID(), A.[OrderID], @Billing, A.[FirstName], A.[LastName], A.[Email], N'1 Seed Street', H.[City], N'10000', H.[CountryID], H.[StateID]
FROM #Addresses A
JOIN #Homes H ON H.[Idx] = A.[CustomerIdx]
UNION ALL
SELECT NEWID(), A.[OrderID], @Shipping, A.[FirstName], A.[LastName], A.[Email], N'2 Seed Street', H.[City], N'10000', H.[CountryID], H.[StateID]
FROM #Addresses A
JOIN #Homes H ON H.[Idx] = CASE
    WHEN A.[ElsewhereRoll] < 18 THEN 1 + (A.[CustomerIdx] * 7 + A.[ElsewhereRoll]) % (@MaxIdx + 8)
    ELSE A.[CustomerIdx]
END
WHERE A.[ShipRoll] < 85;

COMMIT TRANSACTION;

SELECT COUNT(*) AS [SeededOrders], SUM([OrderGrandTotal]) AS [Revenue], MIN([OrderCreatedWhen]) AS [First], MAX([OrderCreatedWhen]) AS [Last]
FROM [Commerce_Order] WHERE [OrderNumber] LIKE @Pattern;

SELECT COUNT(*) AS [SeededCustomers], SUM(CASE WHEN X.[Orders] IS NULL THEN 1 ELSE 0 END) AS [WithoutOrders]
FROM [Commerce_Customer] C
LEFT JOIN (SELECT [OrderCustomerID], COUNT(*) AS [Orders] FROM [Commerce_Order] GROUP BY [OrderCustomerID]) X ON X.[OrderCustomerID] = C.[CustomerID]
WHERE C.[CustomerPhone] LIKE @Pattern;

SELECT S.[OrderStatusDisplayName], COUNT(O.[OrderID]) AS [Orders]
FROM [Commerce_OrderStatus] S
LEFT JOIN [Commerce_Order] O ON O.[OrderOrderStatusID] = S.[OrderStatusID] AND O.[OrderNumber] LIKE @Pattern
GROUP BY S.[OrderStatusDisplayName], S.[OrderStatusOrder]
ORDER BY S.[OrderStatusOrder];

SELECT A.[OrderAddressType], ISNULL(CT.[CountryDisplayName], N'(none)') AS [Country], COUNT(*) AS [Addresses]
FROM [Commerce_OrderAddress] A
JOIN [Commerce_Order] O ON O.[OrderID] = A.[OrderAddressOrderID] AND O.[OrderNumber] LIKE @Pattern
LEFT JOIN [CMS_Country] CT ON CT.[CountryID] = A.[OrderAddressCountryID]
GROUP BY A.[OrderAddressType], CT.[CountryDisplayName]
ORDER BY A.[OrderAddressType], COUNT(*) DESC;
