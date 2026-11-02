# Slice 1 – Customers (digital commerce)

Goal, research, definitions and decisions: `PLAN.md`.

## Seeding (local dev DB only)

Current seed has too few customers and almost no locations for this report. Update `.agent-resources/report-07-commerce-orders/seed-commerce-orders.sql` (preferred, stays re-runnable, report 07 must still look fine) or add `seed-customers.sql` that runs after it. Never in `src/`.

- ~150 customers over the last ~120 days, uneven growth (a promo spike, quiet weeks). `CustomerCreatedWhen` = shortly before the customer's first order. Some customers created before the range (so "returning" exists) and a few with no orders.
- Order count per customer skewed: most 1–2 orders, a handful of heavy buyers (10+), so top-customer rankings differ by revenue / order count / quantity (e.g. one customer with many cheap orders, one with few big orders, one with high quantities).
- Every seeded order gets a Billing address and most get a Shipping address (some ship to a different country/state than billing). Countries/states from `CMS_Country`/`CMS_State` by code name (e.g. USA with several states, Canada with provinces, UK, Germany, Czech Republic without states). A few addresses without a country → "Unknown".
- Keep report 07 totals consistent (items, grand totals) as today.

## Scope

### Server (C#)

1. **Page** `CustomersPage`, slug `customers`, name "Customers", order 800, icon = native commerce Customers application icon (find its `UIApplication` icon in admin assemblies, like reports 06/07). Template `@kentico/xperience-labs-simple-stats-admin/Customers`. Permission `StatsPermissions.CUSTOMERS` = `SimpleStats.Customers`, "Customers", same pattern as other pages.
2. **Filter**: wrap `StatsFilter` (range + grouping) like `OrdersRevenueFilter`, plus:
   - optional **order status** (reuse report 07 status options + normalization; extract a shared piece if copying would be needed),
   - **address type**: Billing (default) or Shipping, used only for the location tiles. Unknown value → Billing.
   Channel normalized to null. Do not change 01–07 cache keys.
3. **Definitions** (say them in UI hints and usage guide):
   - *New customer*: `CustomerCreatedWhen` in range (status filter does not apply).
   - *Ordering customer*: customer with ≥1 order in range (status filter applies).
   - *Returning customer*: ordering customer who also has an order before the range start (any time; status filter applies).
   - *Customer location*: country/state of the chosen address type on the customer's **most recent order in the range**. No address / no country → "Unknown" row. State missing → "(no state)" under the country, or skip that customer in the state tile — pick one, say which.
4. **Queries** (repository, one round trip, parameterized, aggregate in SQL, `TOP N`, current + previous period via `StatsComparison.GetPreviousRange`):
   - Daily new customers, previous period + range, plus the count of customers created before the range start (for a cumulative total line).
   - Totals current vs previous: new customers, ordering customers, returning customer share (ratio, null when 0 ordering customers), revenue per ordering customer (amount, null when 0).
   - Customers by country (ordering customers in range), with previous-period count → change.
   - Top states/regions (label "State, Country"), TOP N.
   - Top customers, three lists TOP N each: by revenue, by order count, by item quantity. Each row: customer ID, display name (first + last, fallback email, fallback "Customer #ID"), email, revenue, orders, quantity, previous-period value of the ranked metric → change.
5. **Shared infra** (only what's needed; make it reusable):
   - **Cumulative series**: running total on top of `StatsTimeSeries` bucketing (start value + per-bucket sums), grouping-aware. Put in `Shared/`, unit test. Reusable for other "total over time" reports (members, contacts).
   - **Ratio values**: `StatsValueKind.Ratio` exists; check it is fully supported end to end (server rounding, client `formatValue` as percent, `ComparisonInfoCard` change for ratios = percentage points or relative — pick one, say why). Fix gaps in shared code, not in the report.
   - Ranked items with extra columns (orders, quantity, email): reuse `StatsRankedItem` secondary value where it fits; if a third value is needed, extend opt-in (JSON unchanged for 01–07 when null).
6. **Admin links** via `IStatsAdminLinks` only: native Customers listing; per-customer detail link if the native customer edit page has a stable public route/page type usable like earlier links. Skip + say why if not.
7. **Result** (suggested): `CustomersResult { From, To, Grouping, OrderStatusId, AddressType, Periods, NewCustomers (series), TotalCustomers (cumulative series), Totals { NewCustomers, OrderingCustomers, ReturningShare, RevenuePerCustomer }, ByCountry, TopStates, TopCustomers { ByRevenue, ByOrders, ByQuantity }, CustomersAppUrl, CommerceAvailable, UpdatedAt }`. Amount texts via `StatsAmountTexts.WithTexts` after the cache (report 07 pattern).
8. **Service**: interface + `StatsCache` (5 min, refresh drops key, key by normalized filter). Tests: builder, service, SQL (0 customers, tables missing, customers without orders, no addresses → Unknown, shipping vs billing, one status, previous 0 → change null, cumulative start value, ratio null). 01–07 tests still pass.

### Client (React/TS)

- Template `customers/CustomersTemplate.tsx`, export in `entry.tsx`.
- Filter bar: range + grouping + status select + address type select (Billing / Shipping), refresh, updated at. Reuse `StatsFilterBar` / `filterControls.tsx` and report 07's status select; generalize in `shared/` if needed.
- KPI row: New customers, Ordering customers, Returning customers (%), Revenue per customer — each with change vs previous period (`ComparisonInfoCard`).
- Tile "Customer growth": shared `ComboChart` (columns = new customers per bucket, line = total customers, right axis) / `TimeSeriesTable` toggle, CSV. Both series are counts; make sure `ComboChart` handles a count line (fix in shared if it assumes amount).
- Tile "Customers by country": `DonutChart` or `RankedBarChart` (pick what reads better with 5–15 countries + Unknown; say why) / table with count + change, CSV.
- Tile "Top states and regions": `RankedBarChart` / `RankedTable`, CSV.
- Tile "Top customers" (full width): segmented "Rank by" control (Revenue / Orders / Items) inside the tile, switching client-side between the three lists. Table view primary: name (link to customer if available), email, revenue (formatted text), orders, items, change of the ranked metric. Chart view: `RankedBarChart` of the ranked metric. CSV of the current list. Use a native admin segmented/toggle component if one exists in `@kentico/xperience-admin-components` (check `.d.ts`); else the same control the tiles already use for chart/table toggle.
- Hints / `DataRetentionNote`: definitions above; location = address on most recent order in range; revenue = order grand total, project price formatter; deleted orders/customers disappear.
- Empty state when commerce unused / tables missing (reuse report 07's).

## Look and feel — must feel native

Same rules as earlier reports. References: `../xperience-by-kentico-admin-design-components` (`AGENTS.md`, `src/components/Charts/*`, `src/components/Table`, `src/templates/DashboardTemplate`, `previews/*.png`, `src/styles/tokens.css`). Prefer `@kentico/xperience-admin-components` exports; verify props in `node_modules/@kentico/xperience-admin-components/dist/*.d.ts`. CSS only via design tokens; colors from `chartTheme.ts`; charts via `createChartRoot`. Keep admin `Table` cells plain single-line (no CSS alignment/multi-line overrides). Charts `React.memo` + content-stable data so toggles don't rebuild other charts.

## Goal

Shareable across customer projects: nothing DancingGoat-specific in `src/`, no hardcoded status/country/address-type IDs or names (except public product constants), parameterized SQL, works with 0 customers, missing commerce tables, orders without addresses.

## Done when

- `npm run typecheck` + `npm run build` pass in `src/Kentico.Xperience.Labs.SimpleStats.Admin/Client`.
- `dotnet build` of `Kentico.Xperience.Labs.SimpleStats.slnx` + `dotnet test` pass; new tests. Run `dotnet format` on changed projects.
- DancingGoat admin: nav shows 8 reports; report renders seeded data; filters, rank-by switch, toggles, CSV, links work; reports 01–07 unchanged. Admin client uses Proxy mode (port 3050): `npm run watch` in Client must run, or rebuild + restart for client changes. Visual check if the app can be run (`docs/Contributing-Setup.md`, `.vscode/tasks.json`); otherwise say not checked.
- `docs/Usage-Guide.md`: report section + permission table row. README report list if it has one.
- Do not commit. Report back (concise): files changed, SQL shape, definitions chosen, shared infra added/changed, admin link approach (or why skipped), seeding done, what was / was not visually checked, open questions.
