# Slice 1 – Activity counts by type over time

Goal, research, definitions and decisions: `PLAN.md`.

## Current repo state

- `src/Kentico.Xperience.Labs.SimpleStats.Admin/` — `SimpleStatsWebAdminModule.cs` registers client module `kentico` / `xperience-labs-simple-stats-admin`. Template names will be `@kentico/xperience-labs-simple-stats-admin/<Name>` (React export `<Name>Template`).
- `Client/src/entry.tsx` — empty; every template must be exported here.
- Client deps: `@kentico/xperience-admin-base` + `-components` 31.9.0. `@amcharts/amcharts5` 5.20.6 is already installed transitively (dep of admin-components). Add it as an explicit dependency at that same version.
- **amCharts is NOT a webpack external** (`@kentico/xperience-webpack-config` externals: `@kentico/xperience-admin-*`, react, react-dom, react-router*, i18next). So it gets bundled. .agent-resources/simple-stats/PLAN.md's "no extra bundle size" claim is wrong. Just import only the modules you need (`amcharts5`, `amcharts5/xy`).
- `examples/DancingGoat` already has a project reference to the library. `.vscode/tasks.json` has npm build/watch + dotnet watch DancingGoat tasks.
- Tests: `tests/Kentico.Xperience.Labs.SimpleStats.Admin.Tests` (NUnit).

## Scope

### Server (C#)

1. **Admin application** "Simple Stats (Labs)" (`UIApplication`), own category or `BaseApplicationCategories.DIGITAL_MARKETING`, icon `Icons.Graph` or similar. Declare app permissions with standard `UIPermission` / `SystemPermissions.VIEW` so admins can limit who sees it (simple-stats PLAN: "Stats should have their own application permissions").
2. **Page** for this report under the app (use a single dashboard page for now; later reports become more pages or tiles). `Page<TClientProperties>` with custom template.
3. **Shared query model**: `StatsFilter { DateTime From; DateTime To; Grouping (Day|Week|Month); int? ChannelId }`. Default: last 30 days, grouping Day.
4. **Page command** `LOAD` taking `StatsFilter`, returning the report data. `ConfigureTemplateProperties` returns the default result + channel options (website + email channels from `ChannelInfo`) + activity type display names (`ActivityTypeInfo`).
5. **Query**: aggregate in SQL, not in memory. Group `OM_Activity` by `ActivityType` and period bucket of `ActivityCreated` inside the range, optional `ActivityChannelID` filter. Use `ActivityInfo.Provider.Get()` / ObjectQuery with `.Columns`, `.GroupBy`, `AggregatedColumn` or a parameterized `ConnectionHelper.ExecuteQuery` — no string-concatenated user input. Fill missing periods with zero on the server so the chart has a continuous axis. Short cache (`IProgressiveCache`, few minutes, keyed by filter) is fine; table can be large. **Decided rule (all reports):** cached report data has NO cache dependencies. Only the 5-minute expiry or the user's Refresh button clears it; data changes never do.
6. Result shape (suggestion): `{ periods: [{ start, label }], series: [{ activityType, displayName, values: int[] }], totals: { total, byType[] }, from, to }`.
7. Keep report logic in a service class behind an interface so it can be unit tested (period bucketing + zero fill at minimum).

### Client (React/TS)

Shared pieces in `Client/src/shared/` (names are suggestions):

- `StatsFilterBar` — date presets 7/30/90 + custom (use `DateTimeRangeInput` from admin-components), grouping (`NameToggleButtons`), channel (`Select`). Changing a filter calls the page command.
- `StatsTile` — card with headline, chart/table toggle (`IconToggleButtons`), CSV export button, loading `Spinner`, empty state. Renders a chart or a table of exact numbers.
- `useStatsCommand` — wraps `usePageCommand` for LOAD with loading/error state.
- `XYChart` wrapper for amCharts 5 (stacked column or multi-line, date/category axis, legend, tooltip). Root created in `useLayoutEffect`, disposed on unmount. Apply the Xperience font theme (see design-components `src/components/Charts/ChartTheme.ts`). Wrap in `ChartLicense` from admin-components if it applies the product license (check its source in design components).
- `toCsv` helper + download.
- Data retention note (`Callout`, type `quickTip` or `friendlyWarning`): contact/activity cleanup deletes old data, so drops may be deletions.

Report template `ActivityCountsTemplate`:

- KPI row (`InfoCardGroup` / `InfoCard`): total activities, top type, number of types.
- Tile: stacked column chart of counts per type per period; table toggle shows period × type grid; CSV export.

### Out of scope for this task

Other reports, per-tile permissions, show/hide tiles, links to other admin pages.

## Look and feel — must feel native

Reference repo: `../xperience-by-kentico-admin-design-components` (read `AGENTS.md`, `registry.json`, `previews/*.png`, `src/components/*`, `src/templates/DashboardTemplate`, `src/templates/OverviewPageTemplate`, `src/styles/tokens.css`).

- **Prefer components from the `@kentico/xperience-admin-components` npm package.** Confirmed exported in 31.9.0: `Card`, `Headline`, `HeadlineSize`, `InfoCard`, `InfoCardGroup`-like `InfoCards`, `Callout`, `DateTimeRangeInput`, `NameToggleButtons`, `IconToggleButtons`, `Select`, `Spinner`, `Paper`, `Box`, `Stack`, `Row`, `Column`, `Table*`, `ColumnChart`, `FunnelChart`, `ChartLicense`, `Colors`. Verify props in `node_modules/@kentico/xperience-admin-components/dist/*.d.ts`.
- If the npm `ColumnChart` covers stacked series by period, use it. Otherwise write our own amCharts wrapper modeled on design-components `src/components/Charts/ColumnChart/ColumnChartCreator.ts`.
- Custom CSS uses only design tokens (CSS custom properties from `tokens.css`); check which tokens are already present at runtime in the admin before copying `tokens.css`. Never hardcode colors. Chart series colors from tokens/`Colors`.
- Layout: page padding and spacing like `DashboardTemplate` / `OverviewPageTemplate`.

Community Portal examples (general patterns only, not a spec — we are not bound to it): `../community-portal/src/Kentico.Community.Portal.Admin/Client/src/features/reports/ContactActivityStatsLayoutTemplate.tsx` and server `../community-portal/src/Kentico.Community.Portal.Admin/Features/ContactManagement/ContactActivityStatsPage.cs`. That portal uses a copied shadcn `Card` — we should use the npm components instead.

## Goal

Shareable across real customer projects: no DancingGoat-specific code in `src/`, no hardcoded IDs, SQL parameterized, works with empty data.

## Done when

- `npm run typecheck` and `npm run build` pass in `src/Kentico.Xperience.Labs.SimpleStats.Admin/Client`.
- `dotnet build` of `Kentico.Xperience.Labs.SimpleStats.slnx` and `dotnet test` pass.
- DancingGoat admin shows "Simple Stats (Labs)" app with the report; filters reload data; chart/table toggle and CSV work. Visual check if the app can be run (see `docs/Contributing-Setup.md`, `.vscode/tasks.json`); otherwise say it was not checked.
- `docs/Usage-Guide.md` updated briefly.
