# Slice 1 – Form submissions per form

Goal, research, definitions and decisions: `PLAN.md`.

## Current repo state (after reports 01–03)

- Server `src/Kentico.Xperience.Labs.SimpleStats.Admin/`: `UIPages/` pages (copy `NewContactsPage.cs`), `Shared/` (`StatsFilter`, `StatsGrouping`, `StatsPeriods`, `StatsLoadRequest`, `StatsCache`, `StatsChannelOptions`, `StatsRanked` (`StatsRankedBuilder` drops value <= 0), `StatsTimeSeries` (`StatsTimeSeriesBuilder.BuildFixed`, `StatsDailyCount`, `StatsSeriesDefinition`)), `Reports/<Name>/` Models / Repository / ReportBuilder / Service. DI in `SimpleStatsWebAdminModule.cs`.
- Client `Client/src/shared/`: `StatsFilterBar` (`showGrouping`, `showChannel`), `StatsTile`, `StackedColumnChart`, `RankedBarChart`, `RankedTable`, `DonutChart`, `ShareTable`, `TimeSeriesTable`, `chartTheme.ts`, `csv.ts`, `table.ts`, `timeSeries.ts`, `format.ts`, `dates.ts`, `types.ts`, `useStatsCommand.ts`, `DataRetentionNote`, `stats.css`. Templates in `activity-counts/`, `top-pages/`, `new-contacts/`; export in `entry.tsx`.
- Tests: `tests/Kentico.Xperience.Labs.SimpleStats.Admin.Tests` (NUnit), `TestDoubles.cs`.

## Scope

### Server (C#)

1. **Page** `FormSubmissionsPage`, slug `form-submissions`, name "Form submissions", order 400, form-like icon (verify in `Icons`), template `@kentico/xperience-labs-simple-stats-admin/FormSubmissions`. Same permission pattern as other pages. No channel (form tables have none) → normalize channel to null for cache key.
2. **Query** (repository), one DB round trip:
   - Read forms: `CMS_Form` join `CMS_Class` → FormID, FormName, FormDisplayName, ClassTableName. Prefer `IInfoProvider<BizFormInfo>` / `DataClassInfoProvider` if simple; verify names.
   - Build one `UNION ALL` statement: per form `SELECT @formIdN AS FormID, CAST(FormInserted AS date) AS [Date], COUNT(*) FROM <table> WHERE FormInserted >= @from AND FormInserted < @toExclusive GROUP BY CAST(FormInserted AS date)`.
   - Table names are identifiers, not parameters: take them only from class metadata, wrap in `QUOTENAME` (or validate `^[A-Za-z0-9_]+$` + bracket), skip tables that do not exist (`OBJECT_ID`) or lack `FormInserted`. Values stay parameters. Put this identifier handling in a small, unit-tested helper.
   - Verify `FormInserted` name/type/nullability in the DB. Say what you found.
   - Many forms: fine for tens of forms. If > ~100, note it; do not over-engineer.
3. **Shared dynamic time series**: add `StatsTimeSeriesBuilder.BuildDynamic` (name is a suggestion) — series from data keys + display names, ordered by total desc, optional top N with the rest folded into an "Other" series. Do not change `BuildFixed` behavior or reports 01/03 output. Unit tests.
4. **Shared ranked with zeros**: opt-in way for `StatsRankedBuilder` to keep value 0 items (for "least used"). Default behavior unchanged; report 02 output identical.
5. **Shared admin links**: a helper to build a native admin URL for an object (here: a form's submissions listing). Use the product's page link generation (e.g. `IPageLinkGenerator` / `IPageUrlGenerator` with the Forms app page types — find the right public API in `Kentico.Xperience.Admin.*` assemblies or docs via the `kentico-docs` MCP). No hardcoded `/admin/...` strings. If no public API works, skip links and say why. `StatsRankedItem.Url` is documented as absolute + new tab; add a separate field (e.g. `AdminUrl`) or a link-kind rather than changing the meaning.
6. **Result** (suggested): `FormSubmissionsResult { TimeSeries (top 5 forms + Other), Forms (ranked, all forms incl. 0, with admin link), Total, FormCount, FormsWithSubmissions, UpdatedAt }`.
7. **Service**: interface + `StatsCache`, same rules as other reports. Builder + service + identifier helper tests.

### Client (React/TS)

- Template `form-submissions/FormSubmissionsTemplate.tsx`: filter bar (range + grouping, no channel); KPI row: total submissions, forms with submissions ("2 of 3"), top form, forms with no submissions.
- Tile 1 "Submissions over time": `StackedColumnChart` (series per form, top 5 + Other) / `TimeSeriesTable` toggle, CSV.
- Tile 2 "Forms by submissions": `RankedBarChart` / `RankedTable` toggle, CSV. Table lists all forms incl. 0 (least used at bottom); form name links to its submissions page in the admin (same tab, SPA navigation if the admin exposes a way — check `@kentico/xperience-admin-base` for navigation helpers/`Link`; plain `<a href>` is fine otherwise). Chart may show only forms with > 0.
- Extend shared pieces (`RankedTable` admin link, types) without changing reports 01–03 look.
- `DataRetentionNote`: submissions deleted by editors or erasers disappear from trends.
- Usage hint: counts come from form data tables (all submissions), not activities.

## Look and feel — must feel native

Same rules as report 01. References: `../xperience-by-kentico-admin-design-components` (`AGENTS.md`, `src/components/Charts/*`, `src/components/Table`, `src/templates/DashboardTemplate`, `previews/*.png`, `src/styles/tokens.css`), `../community-portal/src/Kentico.Community.Portal.Admin/Client/src/features/reports/` (amCharts examples only). Prefer `@kentico/xperience-admin-components` exports; verify props in `node_modules/.../dist/*.d.ts`. CSS only via design tokens. Keep admin `Table` cells plain single-line (earlier CSS overrides looked wrong).

## Goal

Shareable across customer projects: nothing DancingGoat-specific in `src/`, no hardcoded IDs/URLs, values parameterized, identifiers only from metadata and quoted, works with 0 forms and empty tables.

## Done when

- `npm run typecheck` + `npm run build` pass in `src/Kentico.Xperience.Labs.SimpleStats.Admin/Client`.
- `dotnet build` of `Kentico.Xperience.Labs.SimpleStats.slnx` + `dotnet test` pass; new tests; reports 01–03 tests still pass.
- DancingGoat admin: nav shows 4 reports; form report renders seeded data; filters, toggles, CSV, admin links work; reports 01–03 unchanged. Admin client uses Proxy mode (port 3050): `npm run watch` in Client must run, or rebuild + restart for client changes. Visual check if the app can be run (`docs/Contributing-Setup.md`, `.vscode/tasks.json`); otherwise say not checked.
- `docs/Usage-Guide.md` updated briefly.
- Do not commit. Report: files changed, SQL shape, column findings, admin link approach (or why skipped), seed script, what was / was not visually checked.
