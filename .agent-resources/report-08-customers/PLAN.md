# Plan: Report 08 – Customers (digital commerce)

## Goal

Eighth report. Audience: marketers, store managers. simple-stats PLAN "Later candidates → Other → Customer growth over time, broken down by billing or shipping country and state; rank top customers by revenue, order count, and item quantity purchased." Time-range report, sibling of report 07. Read `.agent-resources/simple-stats/PLAN.md`, `.agent-resources/report-07-commerce-orders/` (most relevant), and skim `.agent-resources/report-01-*` to `report-06-*` first. Build on existing code, mostly report 07 (`Reports/OrdersRevenue/*`, `orders-revenue/OrdersRevenueTemplate.tsx`) and `Shared/*` / `Client/src/shared/*`. Do not duplicate. Keep complexity low: few tiles, reuse shared pieces, add shared infra only where a second report clearly benefits.

## Research

### Local data (checked 2026-09-30)

DB: `mssql2022` docker, DB `xperience-by-kentico-simple-stats`, creds in `examples/DancingGoat/appsettings.json`. From Git Bash: `MSYS_NO_PATHCONV=1 docker exec -i mssql2022 /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P '<password>' -d xperience-by-kentico-simple-stats -W < file.sql`. Package version 31.9.0.

- `Commerce_Customer` (26 rows, seeded by `seed-commerce-orders.sql`): `CustomerID`, `CustomerGUID`, `CustomerMemberID` NULL, `CustomerCreatedWhen` datetime2 NOT NULL, `CustomerFirstName`/`LastName`/`Email`/`Phone` NULL.
- `Commerce_CustomerAddress` (1 row): saved addresses per customer, `CustomerAddressCountryID`/`StateID` NULL. Not a reliable location source.
- `Commerce_OrderAddress` (343 rows, all `OrderAddressType = 'Billing'`, only 2 with a country): `OrderAddressOrderID`, `OrderAddressType` nvarchar NULL (verify the type values/constants in `CMS.Commerce`, e.g. an `OrderAddressType` enum or constants — do not hardcode strings if a public constant exists), `OrderAddressCountryID`, `OrderAddressStateID` NULL, snapshot name/email/city/zip.
- `CMS_Country` (`CountryID`, `CountryDisplayName`, `CountryTwoLetterCode`), `CMS_State` (`StateID`, `StateDisplayName`, `StateCode`, `CountryID`).
- Orders: see report 07 (`Commerce_Order.OrderCustomerID`, `OrderCreatedWhen`, `OrderGrandTotal`, `OrderOrderStatusID`; `Commerce_OrderItem.OrderItemQuantity`).
- Verify public Info classes (`CustomerInfo`, `OrderAddressInfo`, `CountryInfo`, `StateInfo`) and reference them in SQL doc comments like report 07. Tables may be missing → `OBJECT_ID` check → empty result + hint (reuse report 07's approach; share the check if simple).

## Slices

1. Customers (digital commerce) (`implementation-spec-1.md`)
2. Active customers (user request) (`implementation-spec-2.md`)

## Slice log

- 2026-09-30 – Slices 1–2: Customers report with active customers (`fca0826`, with nav sections and Export permission).
