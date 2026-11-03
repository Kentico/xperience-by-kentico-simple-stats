# Slice 1 – Top pages by visits

Goal, research, definitions and decisions: `PLAN.md`.

## Current repo state (after report 01)

- Server: `src/Kentico.Xperience.Labs.SimpleStats.Admin/`
  - `UIPages/StatsApplicationPage.cs` — app, `SECTION_LAYOUT` template, so each child `UIPage` shows in the left nav.
  - `UIPages/ActivityCountsPage.cs` — page pattern to copy: `ConfigureTemplateProperties` returns default report + channels + today; `LOAD` page command takes `StatsLoadRequest`.
  - `Shared/` — `StatsFilter` (+ `Normalize`), `StatsGrouping`, `StatsPeriods`, `StatsLoadRequest`, `StatsCache` (5 min, no dependencies, refresh drops it), `StatsChannelOptions`.
  - `Reports/ActivityCounts/` — Models, Repository (SQL), ReportBuilder (pure logic), Service (cache). Copy this split.
  - `SimpleStatsWebAdminModule.cs` — DI registration.
- Client: `src/Kentico.Xperience.Labs.SimpleStats.Admin/Client/src/`
  - `shared/` — `StatsFilterBar`, `StatsTile`, `StackedColumnChart`, `chartTheme.ts`, `csv.ts`, `dates.ts`, `types.ts`, `useStatsCommand.ts`, `DataRetentionNote`, `stats.css`.
  - `activity-counts/ActivityCountsTemplate.tsx` — template pattern to copy.
  - `entry.tsx` — export every template here.
- Tests: `tests/Kentico.Xperience.Labs.SimpleStats.Admin.Tests` (NUnit) — builder + service + filter tests to copy.

## Scope

### Server (C#)

1. **Page** `TopPagesPage` under `StatsApplicationPage`, slug `top-pages`, name "Top pages", order 200, template `@kentico/xperience-labs-simple-stats-admin/TopPages`. `[UIEvaluatePermission(SystemPermissions.VIEW)]`, `LOAD` command with `Permission = SystemPermissions.VIEW`, same as report 01.
2. **Filter**: reuse `StatsFilter` / `StatsLoadRequest`. Grouping is ignored by this report (range only). Do not fork the filter type. If a `Limit` / top-N is needed, keep it a server constant (e.g. 25) for now, not user input.
3. **Query** (repository): parameterized SQL only. `WHERE ActivityType = @type AND ActivityCreated >= @from AND ActivityCreated < @toExclusive [AND ActivityChannelID = @channelId]`, `GROUP BY` the URL, `SELECT TOP (@limit)` ordered by visits desc then URL. Columns per row: URL, visits `COUNT(*)`, unique contacts `COUNT(DISTINCT ActivityContactID)`, a page title (e.g. `MAX(ActivityTitle)` — verify what the column holds for page visits; generator writes `Page visit '<title>'`, so show the URL as primary label and do not parse the title unless the product stores a clean title somewhere reliable). Also return the total visit count for the range (all URLs, not just top N) so share % is correct.
   - Verify `OM_Activity` column names and types in the running DB or `ActivityInfo` before writing SQL. `ActivityURL` may be `nvarchar(max)`; check whether grouping on it is OK or needs a cast/hash. Say what you found.
   - URL normalization: keep simple and document the choice. Recommended: group on the stored URL as-is but strip query string + fragment in SQL only if cheap and safe; otherwise leave as-is and note it. Do not do it in memory over all rows.
4. **Shared ranked model** in `Shared/` (names are suggestions): `StatsRankedItem { Key, Label, SecondaryLabel?, Value, SecondaryValue?, Share (0–1), Url? }` and `StatsRankedResult { Items, Total, From, To }`. Keep it generic so referrers/forms/emails can reuse it. Report-specific result may wrap or be this type.
5. **Service** behind an interface with the same cache rules as report 01 (key by filter; refresh drops key; no cache dependencies). Reuse `StatsCache` helpers; extract a shared helper if report 01 service has code worth sharing, but do not break report 01.
6. **Builder** (pure, unit-tested): share % from total, stable ordering, empty data.
7. **Links to admin pages** (simple-stats PLAN "Links to existing admin pages"): optional. Only add if you can reliably map a visit to a web page item (e.g. `ActivityWebPageItemGUID` or similar column — verify it exists) and build a correct admin URL without hardcoding. If not simple, skip and say so. Public URL link (open the visited page in a new tab) is fine if `ActivityURL` is absolute.

### Client (React/TS)

New shared pieces in `shared/`:

- `RankedBarChart` — amCharts 5 horizontal bar chart (category Y axis = label, value X axis), largest on top, value labels at bar ends, tooltip with value + share %. Same lifecycle and theme as `StackedColumnChart` (`chartTheme.ts`, `ChartLicense` if report 01 uses it). Height scales with item count. Long labels truncate with full text in tooltip. Colors from tokens/`Colors`, never hardcoded.
- `RankedTable` — native admin `Table*` components (or whatever report 01 table uses): rank, label (link if `url`), value, secondary value, share %. Numbers right aligned.
- Ranked CSV helper (reuse `csv.ts`).
- `StatsFilterBar`: add a way to hide the grouping control (e.g. `showGrouping?: boolean`, default true). Report 01 must still look and work the same.
- Types for the ranked model in `types.ts`.

Template `TopPagesTemplate` (`top-pages/TopPagesTemplate.tsx`):

- Filter bar (range + channel + refresh, no grouping).
- KPI row like report 01: total page visits, unique pages (distinct URLs in range — include in result if cheap, else "pages shown"), top page.
- One `StatsTile`: `RankedBarChart` / `RankedTable` toggle, CSV export.
- `DataRetentionNote`.

## Look and feel — must feel native

Same rules as report 01 (see its "Look and feel" section). References:

- `../xperience-by-kentico-admin-design-components` — `AGENTS.md`, `src/components/Charts/*` (ColumnChart creator for amCharts setup, `ChartTheme.ts`), `src/components/Table`, `src/templates/DashboardTemplate`, `previews/*.png`, `src/styles/tokens.css`.
- `../community-portal/src/Kentico.Community.Portal.Admin/Client/src/features/reports/` — general amCharts examples only, not a spec.
- Prefer `@kentico/xperience-admin-components` npm exports; verify props in `node_modules/@kentico/xperience-admin-components/dist/*.d.ts`. Custom CSS uses only design tokens.

## Goal

Shareable across real customer projects: no DancingGoat-specific code in `src/`, no hardcoded IDs, SQL parameterized, works with empty data, reasonable on a large `OM_Activity` table (one aggregate query, TOP N in SQL).

## Done when

- `npm run typecheck` and `npm run build` pass in `src/Kentico.Xperience.Labs.SimpleStats.Admin/Client`.
- `dotnet build` of `Kentico.Xperience.Labs.SimpleStats.slnx` and `dotnet test` pass. New builder + service tests.
- DancingGoat admin: "Simple Stats (Labs)" shows both "Activity counts" and "Top pages" in the nav; top pages filters reload data; chart/table toggle and CSV work; report 01 unchanged. Visual check if the app can be run (see `docs/Contributing-Setup.md`, `.vscode/tasks.json`); otherwise say it was not checked.
- `docs/Usage-Guide.md` updated briefly.
- Do not commit. Report: files changed, SQL used, column findings, what was / was not visually checked, anything skipped (e.g. admin links) and why.
