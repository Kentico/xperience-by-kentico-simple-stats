# Plan: Report 07 – Orders and revenue (digital commerce)

## Goal

Seventh report. Audience: marketers, store managers. simple-stats PLAN "Later candidates → Other → Digital commerce orders and revenue". Time-range report like 01–04, 06. Read `.agent-resources/simple-stats/PLAN.md` and `.agent-resources/report-01-*` to `report-06-*` first. Build on existing code; do not duplicate. Keep complexity low: few tiles, reuse shared pieces, add only the shared infra listed below.

## Research

### Local data (checked 2026-09-30)

DB: `mssql2022` docker, DB `xperience-by-kentico-simple-stats`, creds in `examples/DancingGoat/appsettings.json`. From Git Bash: `MSYS_NO_PATHCONV=1 docker exec mssql2022 /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P ... -d xperience-by-kentico-simple-stats -W -Q "..."`. Package version 31.9.0.

- `Commerce_Order`: **0 rows**. Columns: `OrderID`, `OrderGUID`, `OrderNumber` nvarchar, `OrderCreatedWhen` datetime2 NOT NULL, `OrderModifiedWhen`, `OrderOrderStatusID` int NOT NULL, `OrderTotalPrice`/`OrderTotalShipping`/`OrderTotalTax`/`OrderGrandTotal` decimal NULL, `OrderCustomerID` int NOT NULL, `OrderPaymentMethodID` NULL, `OrderPaymentMethodDisplayName`, `OrderShippingMethodID` NULL, `OrderShippingMethodDisplayName`, `OrderShippingMethodPrice` decimal NOT NULL.
- `Commerce_OrderItem`: 0 rows. `OrderItemOrderID`, `OrderItemSKU`, `OrderItemName` (snapshots, nullable), `OrderItemQuantity`/`OrderItemUnitPrice`/`OrderItemTotalPrice` decimal NULL, `OrderItemTotalTax`, `OrderItemTaxRate` NOT NULL.
- `Commerce_OrderStatus`: 5 rows (Pending 1, Payment failed 2, Payment received 3, Processing 4, Fulfilled 5; `OrderStatusOrder` gives display order). Statuses are editable per project → never hardcode names/IDs.
- `Commerce_Customer` 0 rows (`CustomerCreatedWhen` NOT NULL, `CustomerMemberID` NULL). Payment methods 2, shipping methods 2.
- **No channel column** on orders → no channel filter; normalize channel to null for cache key.
- **No currency** stored on orders or in settings. Currency comes from the project's price formatter `CMS.Commerce.IPriceFormatter` (DancingGoat `Commerce/PriceFormatter.cs`, en-US `C2`). **Amounts must show the project's currency** (user change, see "Amount formatting" below). Say this in the usage guide.
- simple-stats PLAN note: "not yet checked which commerce tables are stable enough". Verify in the `Kentico.Xperience.*` / `CMS.Commerce` assemblies (31.9) that public Info classes exist (`OrderInfo`, `OrderItemInfo`, `OrderStatusInfo`, …) and use their column-name constants / `TYPEINFO` / `IInfoProvider<>` where it keeps SQL simple. Say what you found (and any "preview/experimental" markers). Commerce may be unlicensed or unused → report must work with 0 orders, and must not fail if the tables are missing (`OBJECT_ID` check or equivalent → empty result + hint).

## Slices

1. Orders and revenue (digital commerce) (`implementation-spec-1.md`)
2. Round 2 (user feedback) (`implementation-spec-2.md`)

## Slice log

- 2026-09-30 – Slice 1: Orders and revenue report, decimal/amount infra, `IStatsAmountFormatter` (`e1a1784`).
- 2026-09-30 – Slice 2: combo line contrast, legend markers, amCharts logo space (`9151ea7`).
