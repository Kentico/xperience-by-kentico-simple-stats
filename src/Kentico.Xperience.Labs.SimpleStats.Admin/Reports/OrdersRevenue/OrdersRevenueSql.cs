using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.Commerce;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.OrdersRevenue;

/// <summary>
/// Builds the orders and revenue batches. Only constant SQL fragments are combined; all values are parameters.
/// Tables and columns are those of <c>CMS.Commerce.OrderInfo</c> (<c>Commerce_Order</c>), <c>OrderItemInfo</c> (<c>Commerce_OrderItem</c>)
/// and <c>OrderStatusInfo</c> (<c>Commerce_OrderStatus</c>).
/// </summary>
/// <remarks>
/// Both batches start with one row (<see cref="AvailableColumn"/>): <c>0</c> when a commerce table does not exist, and then return nothing else.
/// Report batch result sets after that, in order:
/// <list type="number">
/// <item>Orders and revenue (<c>SUM(ISNULL(OrderGrandTotal, 0))</c>) per day from the start of the previous period to the end of the range (status filter applied).</item>
/// <item>Items sold (quantity) in the range and in the previous period (status filter applied), one row.</item>
/// <item>Orders and revenue per status in the range, in status order (no status filter).</item>
/// <item>Top products by item revenue, with quantity and previous period revenue (status filter applied).</item>
/// </list>
/// <c>Commerce_Order</c> has an index on <c>OrderCreatedWhen</c>, so the range conditions can seek.
/// </remarks>
internal static class OrdersRevenueSql
{
    /// <summary>Start of the previous period (inclusive).</summary>
    public const string PreviousFromParameter = "@PreviousFrom";

    /// <summary>Start of the range (inclusive). Earlier orders belong to the previous period.</summary>
    public const string FromParameter = "@From";

    /// <summary>Day after the end of the range (exclusive).</summary>
    public const string ToExclusiveParameter = "@ToExclusive";

    /// <summary>Order status ID of the status filter.</summary>
    public const string OrderStatusIdParameter = CommerceSql.OrderStatusIdParameter;

    /// <summary>Maximum number of top products.</summary>
    public const string LimitParameter = "@Limit";

    public const string AvailableColumn = CommerceSql.AvailableColumn;
    public const string DateColumn = "OrderDate";
    public const string OrdersColumn = "OrderCount";
    public const string RevenueColumn = "Revenue";
    public const string CurrentQuantityColumn = "CurrentQuantity";
    public const string PreviousQuantityColumn = "PreviousQuantity";
    public const string StatusIdColumn = CommerceSql.StatusIdColumn;
    public const string StatusNameColumn = CommerceSql.StatusNameColumn;
    public const string StatusCodeNameColumn = "OrderStatusName";
    public const string ProductKeyColumn = "ProductKey";
    public const string SkuColumn = "Sku";
    public const string NameColumn = "ProductName";
    public const string QuantityColumn = "Quantity";
    public const string PreviousRevenueColumn = "PreviousRevenue";
    public const string GroupCountColumn = "GroupCount";
    public const string TotalRevenueColumn = "TotalRevenue";

    private static readonly string availabilityCheck =
        CommerceSql.BuildAvailabilityCheck("Commerce_Order", "Commerce_OrderItem", "Commerce_OrderStatus");

    // 1. Orders and revenue per day, previous period + range ({0} = status condition).
    private const string DailyQuery = """
        SELECT
            CAST(O.[OrderCreatedWhen] AS date) AS [OrderDate],
            COUNT(*) AS [OrderCount],
            SUM(ISNULL(O.[OrderGrandTotal], 0)) AS [Revenue]
        FROM [Commerce_Order] O
        WHERE O.[OrderCreatedWhen] >= @PreviousFrom
            AND O.[OrderCreatedWhen] < @ToExclusive{0}
        GROUP BY CAST(O.[OrderCreatedWhen] AS date);
        """;

    // 2. Items sold, range and previous period ({0} = status condition). Always one row.
    private const string ItemsSoldQuery = """
        SELECT
            ISNULL(SUM(CASE WHEN O.[OrderCreatedWhen] >= @From THEN ISNULL(I.[OrderItemQuantity], 0) ELSE 0 END), 0) AS [CurrentQuantity],
            ISNULL(SUM(CASE WHEN O.[OrderCreatedWhen] < @From THEN ISNULL(I.[OrderItemQuantity], 0) ELSE 0 END), 0) AS [PreviousQuantity]
        FROM [Commerce_OrderItem] I
        INNER JOIN [Commerce_Order] O ON O.[OrderID] = I.[OrderItemOrderID]
        WHERE O.[OrderCreatedWhen] >= @PreviousFrom
            AND O.[OrderCreatedWhen] < @ToExclusive{0};
        """;

    // 3. Orders per status in the range, in status order. No status filter: shows the whole mix.
    private const string ByStatusQuery = """
        SELECT
            S.[OrderStatusID],
            S.[OrderStatusName],
            S.[OrderStatusDisplayName],
            COUNT(O.[OrderID]) AS [OrderCount],
            ISNULL(SUM(ISNULL(O.[OrderGrandTotal], 0)), 0) AS [Revenue]
        FROM [Commerce_OrderStatus] S
        LEFT JOIN [Commerce_Order] O ON O.[OrderOrderStatusID] = S.[OrderStatusID]
            AND O.[OrderCreatedWhen] >= @From
            AND O.[OrderCreatedWhen] < @ToExclusive
        GROUP BY S.[OrderStatusID], S.[OrderStatusName], S.[OrderStatusDisplayName], S.[OrderStatusOrder]
        ORDER BY S.[OrderStatusOrder], S.[OrderStatusID];
        """;

    // 4. Top products ({0} = status condition). Grouped by SKU, else by item name (prefixed keys so they cannot collide).
    // COUNT(*) OVER () and SUM(...) OVER () run after HAVING and before TOP, so they cover every product with revenue in the range.
    private const string TopProductsQuery = """
        SELECT TOP (@Limit)
            P.[ProductKey],
            MAX(P.[Sku]) AS [Sku],
            MAX(P.[ProductName]) AS [ProductName],
            SUM(CASE WHEN P.[IsCurrent] = 1 THEN P.[ItemRevenue] ELSE 0 END) AS [Revenue],
            SUM(CASE WHEN P.[IsCurrent] = 1 THEN P.[ItemQuantity] ELSE 0 END) AS [Quantity],
            SUM(CASE WHEN P.[IsCurrent] = 0 THEN P.[ItemRevenue] ELSE 0 END) AS [PreviousRevenue],
            COUNT(*) OVER () AS [GroupCount],
            SUM(SUM(CASE WHEN P.[IsCurrent] = 1 THEN P.[ItemRevenue] ELSE 0 END)) OVER () AS [TotalRevenue]
        FROM (
            SELECT
                CASE
                    WHEN I2.[Sku] IS NOT NULL THEN N'sku:' + I2.[Sku]
                    WHEN I2.[ProductName] IS NOT NULL THEN N'name:' + I2.[ProductName]
                    ELSE N''
                END AS [ProductKey],
                I2.[Sku],
                I2.[ProductName],
                I2.[ItemRevenue],
                I2.[ItemQuantity],
                I2.[IsCurrent]
            FROM (
                SELECT
                    NULLIF(LTRIM(RTRIM(I.[OrderItemSKU])), N'') AS [Sku],
                    NULLIF(LTRIM(RTRIM(I.[OrderItemName])), N'') AS [ProductName],
                    ISNULL(I.[OrderItemTotalPrice], 0) AS [ItemRevenue],
                    ISNULL(I.[OrderItemQuantity], 0) AS [ItemQuantity],
                    CASE WHEN O.[OrderCreatedWhen] >= @From THEN 1 ELSE 0 END AS [IsCurrent]
                FROM [Commerce_OrderItem] I
                INNER JOIN [Commerce_Order] O ON O.[OrderID] = I.[OrderItemOrderID]
                WHERE O.[OrderCreatedWhen] >= @PreviousFrom
                    AND O.[OrderCreatedWhen] < @ToExclusive{0}
            ) I2
        ) P
        GROUP BY P.[ProductKey]
        HAVING SUM(CASE WHEN P.[IsCurrent] = 1 THEN P.[ItemRevenue] ELSE 0 END) > 0
        ORDER BY [Revenue] DESC, P.[ProductKey];
        """;

    /// <summary>
    /// Builds the report batch. With <paramref name="filterByStatus"/>, all result sets except the status breakdown read only orders
    /// in <see cref="OrderStatusIdParameter"/>.
    /// </summary>
    public static string BuildReport(bool filterByStatus)
    {
        string status = filterByStatus ? CommerceSql.StatusCondition : string.Empty;

        return string.Join(
            Environment.NewLine,
            "SET NOCOUNT ON;",
            availabilityCheck,
            string.Format(DailyQuery, status),
            string.Format(ItemsSoldQuery, status),
            ByStatusQuery,
            string.Format(TopProductsQuery, status));
    }

    /// <summary>
    /// Builds the batch that reads the order statuses (for the status filter), in status order. Shared with the other commerce reports.
    /// </summary>
    public static string BuildStatuses() => CommerceSql.BuildStatuses();
}
