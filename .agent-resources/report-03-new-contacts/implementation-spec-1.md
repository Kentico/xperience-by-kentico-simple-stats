# Slice 1 – New contacts over time

Goal, research, definitions and decisions: `PLAN.md`.

## Current repo state (after reports 01 + 02)

- Server `src/Kentico.Xperience.Labs.SimpleStats.Admin/`: `UIPages/` pages (`ActivityCountsPage.cs`, `TopPagesPage.cs` — copy pattern), `Shared/` (`StatsFilter`, `StatsGrouping`, `StatsPeriods`, `StatsLoadRequest`, `StatsCache`, `StatsChannelOptions`, `StatsRanked`), `Reports/<Name>/` split into Models / Repository (SQL) / ReportBuilder (pure) / Service (cache). DI in `SimpleStatsWebAdminModule.cs`.
- Client `Client/src/`: `shared/` (`StatsFilterBar` with `showGrouping`, `StatsTile`, `StackedColumnChart`, `RankedBarChart`, `RankedTable`, `chartTheme.ts`, `csv.ts`, `dates.ts`, `format.ts`, `types.ts` incl. `StatsSeries`, `useStatsCommand.ts`, `DataRetentionNote`, `stats.css`), `activity-counts/`, `top-pages/`, `entry.tsx` (export every template).
- Tests: `tests/Kentico.Xperience.Labs.SimpleStats.Admin.Tests` (NUnit), `TestDoubles.cs`.

## Scope

### Server (C#)

1. **Page** `NewContactsPage` under `StatsApplicationPage`, slug `new-contacts`, name "New contacts", order 300, icon fitting contacts (e.g. `Icons.UserFrame` / verify name in `Icons`), template `@kentico/xperience-labs-simple-stats-admin/NewContacts`. Same permission pattern as report 01 (`[UIEvaluatePermission(SystemPermissions.VIEW)]`, `LOAD` command with `Permission = SystemPermissions.VIEW`). No channel options in client props.
2. **Filter**: reuse `StatsFilter` / `StatsLoadRequest`. Channel ignored (normalize it to null for this report so the cache key does not split on it).
3. **Query** (repository): parameterized SQL only, one aggregate:
   `SELECT CAST(ContactCreated AS date) AS [Date], SUM(CASE WHEN identified THEN 1 ELSE 0 END), SUM(CASE WHEN NOT identified ...) FROM OM_Contact WHERE ContactCreated >= @from AND ContactCreated < @toExclusive GROUP BY CAST(ContactCreated AS date)`.
   - Identified = `ContactEmail IS NOT NULL AND ContactEmail <> ''` (trim if cheap). Document the definition in the UI (short hint) and usage guide.
   - Verify `OM_Contact` column names/types (and whether `ContactCreated` is nullable) in the running DB or `ContactInfo` before writing SQL. Say what you found.
   - Also return the **total contacts now** (all time, `COUNT(*)`) only if cheap; otherwise skip. Not required.
4. **Shared time series** in `Shared/` (names are suggestions): a pure builder that takes daily rows `(string SeriesKey, DateOnly Date, int Count)` + query range/grouping and returns zero-filled `IReadOnlyList<StatsPeriod>` + series values per key. Model: `StatsTimeSeries { Key, DisplayName, Values, Total }` and `StatsTimeSeriesResult { From, To, Grouping, ChannelId, Periods, Series, Total, UpdatedAt }`.
   - Refactor `ActivityCountsReportBuilder` to use the shared builder **only if** it stays a small, safe change and report 01 output + tests stay identical (JSON shape to the client must not change). If it gets messy, leave report 01 alone and say so.
   - New contacts result: series `identified` ("Identified") and `anonymous` ("Anonymous"), fixed order (not sorted by total), plus totals per segment and identified share (0–1, 0 when total is 0).
5. **Service** behind an interface, same cache rules as reports 01/02 (reuse `StatsCache`; key by normalized filter; refresh drops key; no cache dependencies).
6. **Builder tests**: bucketing per grouping, zero fill, share with empty data, fixed series order. Service tests like report 02.
7. **Admin links**: optional. A link to the native Contacts application is fine only if the URL can be built without hardcoding. Otherwise skip and say so.

### Client (React/TS)

New shared pieces in `shared/`:

- `DonutChart` (or `ShareChart`) — amCharts 5 `percent` pie with inner radius (donut), slices from `{ key, name, value }[]`, center label with total or main share %, legend, tooltip with value + %. Same lifecycle/theme as `StackedColumnChart` (`chartTheme.ts`, `getSeriesPalette`). Import only `@amcharts/amcharts5/percent`. Colors from tokens/palette, never hardcoded. Empty state when all values 0 (let `StatsTile` handle it if it already does).
- `StatsFilterBar`: add `showChannel?: boolean` (default true), mirroring `showGrouping`. Reports 01/02 unchanged.
- Types for the time series result in `types.ts`. Reuse `StatsSeries`.
- Reuse `StackedColumnChart` for identified vs anonymous per period. If a generic period × series table exists in report 01 template, move it to `shared/` (e.g. `TimeSeriesTable`) and use it in both, only if it is a clean move.

Template `NewContactsTemplate` (`new-contacts/NewContactsTemplate.tsx`):

- Filter bar: range + grouping + refresh, no channel.
- KPI row like report 01: new contacts, identified (count + %), anonymous.
- Tile 1 "New contacts over time": stacked column (identified + anonymous) / table toggle, CSV.
- Tile 2 "Identified vs anonymous": donut / small table toggle, CSV. Place tiles like the design components `DashboardTemplate` (two-column on wide, stacked on narrow) or full width each — pick what looks native.
- `DataRetentionNote` (contact cleanup deletes old contacts; merged contacts also disappear — mention briefly).

## Look and feel — must feel native

Same rules as report 01 "Look and feel". References:

- `../xperience-by-kentico-admin-design-components` — `AGENTS.md`, `src/components/Charts/*` (look for a pie/donut creator and `ChartTheme.ts`), `src/templates/DashboardTemplate`, `previews/*.png`, `src/styles/tokens.css`.
- `../community-portal/src/Kentico.Community.Portal.Admin/Client/src/features/reports/` — amCharts examples only, not a spec.
- Prefer `@kentico/xperience-admin-components` exports; verify props in `node_modules/@kentico/xperience-admin-components/dist/*.d.ts`. Custom CSS uses only design tokens.
- Memory from earlier work: keep admin `Table` cells plain single-line; CSS alignment/multi-line overrides looked wrong.

## Goal

Shareable across real customer projects: no DancingGoat-specific code in `src/`, no hardcoded IDs, SQL parameterized, works with empty data, one aggregate query.

## Done when

- `npm run typecheck` and `npm run build` pass in `src/Kentico.Xperience.Labs.SimpleStats.Admin/Client`.
- `dotnet build` of `Kentico.Xperience.Labs.SimpleStats.slnx` and `dotnet test` pass. New builder + service tests; report 01/02 tests still pass.
- DancingGoat admin: "Simple Stats (Labs)" nav shows Activity counts, Top pages, New contacts; filters reload; toggles and CSV work; reports 01/02 unchanged. Admin client runs in Proxy mode (port 3050): `npm run watch` in Client must run, or rebuild + restart the app for client changes. Visual check if the app can be run (see `docs/Contributing-Setup.md`, `.vscode/tasks.json`); otherwise say it was not checked.
- `docs/Usage-Guide.md` updated briefly.
- Do not commit. Report: files changed, SQL used, column findings, whether report 01 builder was refactored, what was / was not visually checked, anything skipped and why.
