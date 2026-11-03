# Slice 1 – Orders and revenue (digital commerce)

Goal, research, definitions and decisions: `PLAN.md`.

## Seeding (local dev DB only)

Save as `.agent-resources/report-07-commerce-orders/seed-commerce-orders.sql`, re-runnable (delete previously seeded rows first, e.g. by an `OrderNumber` prefix like `SEED-`). Never put seeding in `src/`. Check `NOT NULL` columns and FKs first.

- ~25 customers (some linked to contacts' emails if easy; not required).
- ~300–400 orders over the last ~120 days: uneven (weekday/weekend rhythm, a promo spike, a few empty days), statuses spread (mostly Fulfilled/Payment received, some Pending/Processing, a few Payment failed), both payment + shipping methods (copy display names from method tables).
- 1–4 items per order, SKU/name from real DancingGoat products (read product content items or reuse names from `examples/DancingGoat` data), totals consistent: item total = qty × unit price; order total = sum items; grand total = total + shipping + tax.
- Addresses only if required by FKs/constraints.

## Scope

### Server (C#)

1. **Page** `OrdersRevenuePage` (name suggestion), slug `orders-revenue`, name "Orders and revenue", order 700, icon same as the native commerce Orders application (find its `UIApplication` icon in the admin assemblies, like report 06 did for Event log). Template `@kentico/xperience-labs-simple-stats-admin/OrdersRevenue`. Permission `StatsPermissions.ORDERS_REVENUE` = `SimpleStats.OrdersRevenue`, "Orders and revenue", same pattern as other pages (`UIPermission` on app, `UIEvaluatePermission` on page, `PageCommand.Permission`).
2. **Filter**: `StatsFilter` range + grouping + optional **order status** (All, or one status ID). Status options come from `Commerce_OrderStatus` sorted by `OrderStatusOrder` (sent to the client as filter options, like channel options). Unknown ID → All. Same wrapping approach as report 06's event type filter (reuse the pattern; do not change 01–06 cache keys).
3. **Queries** (repository), parameterized, aggregate in SQL, one round trip, current + previous period (`StatsComparison.GetPreviousRange`), range by `OrderCreatedWhen`:
   - Daily orders count + daily revenue (`SUM(ISNULL(OrderGrandTotal,0))`).
   - Totals current vs previous: orders, revenue, average order value (revenue / orders; null when 0 orders), items sold (sum quantity).
   - Orders + revenue by status (current range; ignore status filter for this breakdown so it always shows the mix — say so in UI hint).
   - Top products by revenue: group by `OrderItemSKU` (fallback name when SKU null), `TOP N`, with quantity, revenue, previous revenue → change. Respects status filter.
   - Optional if trivial: payment methods and shipping methods share (by denormalized display name). Skip if it adds noise.
4. **Shared infra (new, reusable)** — the main point of this report:
   - **Decimal values.** Existing `StatsComparison`, `StatsTimeSeries`, `StatsDailyCount`, ranked types are `int`. Add decimal support without changing 01–06 JSON output or tests. Pick the simplest clean option (e.g. `StatsDecimalComparison` / `StatsAmountDailyValue` + `BuildFixed` overload, or generic math `INumber<T>` if it stays readable). Say why. Unit tests incl. rounding (round money to 2 decimals server side).
   - **Value kind for the client** (count vs amount vs ratio) so shared client components format correctly. Reusable later for email summary rates.
   - Ranked items with a secondary value (revenue + quantity) — reuse report 06 ranked + change; extend opt-in only if needed.
5. **Admin links** via `IStatsAdminLinks` only: link to native Orders listing; per-status link to a filtered listing only if a public API supports it. Skip + say why if not.
6. **Result** (suggested): `OrdersRevenueResult { From, To, Grouping, OrderStatusId, Periods, Orders (series), Revenue (series), Totals { Orders, Revenue, AverageOrderValue, ItemsSold } (comparisons), ByStatus, TopProducts, OrdersAppUrl, UpdatedAt }`.
7. **Service**: interface + `StatsCache`, same rules (5 min, refresh drops key, key by normalized filter). Builder + service + SQL tests (0 orders, tables missing, one status, null totals, AOV null, previous 0 → change null). 01–06 tests still pass.

### Client (React/TS)

- Template `orders-revenue/OrdersRevenueTemplate.tsx`, export in `entry.tsx`.
- Filter bar: range + grouping + status select (reuse `filterControls.tsx` / event log type filter pattern; generalize in `shared/` if needed), refresh, updated at.
- KPI row: Orders, Revenue, Average order value, Items sold — each with change vs previous period (reuse `ComparisonInfoCard`; extend for amount formatting).
- Tile "Orders and revenue over time": **new shared `ComboChart`** (columns = orders on left axis, line = revenue on right axis, amCharts 5 from the admin) / table toggle (`TimeSeriesTable` with both series, amount column formatted), CSV. Reusable later (email sends + open rate).
- Tile "Orders by status": `DonutChart` / `ShareTable`, show both orders and revenue in table, CSV.
- Tile "Top products": `RankedBarChart` (revenue) / `RankedTable` with quantity, revenue, change columns, CSV. Full-width row.
- Shared `format.ts`: `formatValue(value, kind, text?)` prefers the server-formatted text; fallback amount format is 2 decimals, locale grouping, no symbol. CSV exports raw numbers (dot decimal, no grouping); amount headers say "(raw amount)".
- Link/button to native Orders app if the server gives a path.
- `DataRetentionNote`/hint: revenue = order grand total (incl. shipping and tax) as stored at order time; formatted by the project price formatter, not converted between currencies; deleted orders disappear.
- Empty state when commerce has no orders (or tables missing): friendly message, no errors.

## Look and feel — must feel native

Same rules as earlier reports. References: `../xperience-by-kentico-admin-design-components` (`AGENTS.md`, `src/components/Charts/*`, `src/components/Table`, `src/templates/DashboardTemplate`, `previews/*.png`, `src/styles/tokens.css`), `../community-portal/src/Kentico.Community.Portal.Admin/Client/src/features/reports/` (amCharts examples only, not a spec). Prefer `@kentico/xperience-admin-components` exports; verify props in `node_modules/@kentico/xperience-admin-components/dist/*.d.ts`. CSS only via design tokens; colors from `chartTheme.ts`. Keep admin `Table` cells plain single-line (earlier CSS overrides looked wrong). Charts `React.memo` + content-stable data (report 06 round 4) so toggles don't rebuild other charts.

## Goal

Shareable across customer projects: nothing DancingGoat-specific in `src/`, no hardcoded status names/IDs/URLs/currency, parameterized SQL, works with 0 orders, missing commerce tables, custom statuses.

## Done when

- `npm run typecheck` + `npm run build` pass in `src/Kentico.Xperience.Labs.SimpleStats.Admin/Client`.
- `dotnet build` of `Kentico.Xperience.Labs.SimpleStats.slnx` + `dotnet test` pass; new tests.
- DancingGoat admin: nav shows 7 reports; report renders seeded data; status filter, toggles, CSV, links work; reports 01–06 unchanged. Admin client uses Proxy mode (port 3050): `npm run watch` in Client must run, or rebuild + restart for client changes. Visual check if the app can be run (`docs/Contributing-Setup.md`, `.vscode/tasks.json`); otherwise say not checked.
- `docs/Usage-Guide.md`: report section + permission table row.
- Do not commit. Report: files changed, SQL shape, commerce API/table stability findings, decimal infra decision, filter decision, admin link approach (or why skipped), seeding done, what was / was not visually checked.

## Amount formatting (user change)

Amounts must show the project's currency, not plain decimals.

- Server: shared `IStatsAmountFormatter` (`Shared/StatsAmountFormatter.cs`) wraps the project's `IPriceFormatter` (last registered; resolved as `IEnumerable<IPriceFormatter>`, so none registered is fine) and calls it like the native Orders listing: `Format(price, new PriceFormatContext())` (`PriceFormatContext` has no members in 31.9). The product registers a default `PriceFormatter` (`price.ToString("F2")`, no symbol) via `RegisterImplementation`; projects replace it. Missing / throwing / empty formatter → `null` text → client formats a plain 2-decimal number.
- Raw decimal + formatted string for every amount, in shared types (texts are opt-in, left out of JSON when `null`, so reports 01–06 are unchanged): `StatsValueComparison.CurrentText/PreviousText`, `StatsValueSeries.Texts/TotalText`, `StatsRankedItem.ValueText/SecondaryValueText/PreviousValueText`, `StatsRankedResult.TotalText`. `StatsAmountTexts.WithTexts(...)` fills them for `Amount` kinds only; applied in the service after the cache.
- Client: KPI cards, combo chart tooltip, time series table, ranked table/chart (tooltip + bar-end labels), share table use the texts.
- Axis labels: plain numbers (`#,###.##`). Deriving a currency prefix/suffix from a formatted sample is locale-fragile, so axes stay plain and tooltips/tables show formatted amounts.
- CSV: raw numbers; amount column headers note "(raw amount)".
- Tests: formatter present / absent / failing, amounts only get texts, JSON unchanged without amounts.
