# Slice 1 – Content inventory

Goal, research, definitions and decisions: `PLAN.md`.

## Current repo state (after reports 01–04)

- Server `src/Kentico.Xperience.Labs.SimpleStats.Admin/`: `UIPages/` pages (copy `NewContactsPage.cs`), `Shared/` (`StatsFilter`/`StatsQuery`, `StatsGrouping`, `StatsPeriods`, `StatsLoadRequest`, `StatsCache`, `StatsChannelOptions`, `StatsRanked` (`StatsRankedBuilder`, opt-in keep zeros), `StatsTimeSeries`, `StatsComparison`, `StatsAdminLinks` (`IStatsAdminLinks.GetPath<TPage>`)), `Reports/<Name>/` Models / Repository / ReportBuilder / Service (+ `FormSubmissionsSql`). DI in `SimpleStatsWebAdminModule.cs`.
- Client `Client/src/shared/`: `StatsFilterBar` (`showGrouping`, `showChannel`, refresh, `updatedAt`), `StatsTile`, `StackedColumnChart`, `RankedBarChart`, `RankedTable` (admin links), `DonutChart`, `ShareTable`, `TimeSeriesTable`, `adminLinks.ts`, `chartTheme.ts`, `csv.ts`, `table.ts`, `timeSeries.ts`, `format.ts`, `dates.ts`, `types.ts`, `useStatsCommand.ts`, `DataRetentionNote`, `stats.css`. Templates in `activity-counts/`, `top-pages/`, `new-contacts/`, `form-submissions/`; export each in `entry.tsx`.
- Tests: `tests/Kentico.Xperience.Labs.SimpleStats.Admin.Tests` (NUnit), `TestDoubles.cs`.

## Scope

### Server (C#)

1. **Page** `ContentInventoryPage`, slug `content-inventory`, name "Content inventory", order 500, content-like icon (verify in `Icons`), template `@kentico/xperience-labs-simple-stats-admin/ContentInventory`. Same permission pattern as other pages.
2. **Shared snapshot request** (names are suggestions): a small request/filter for reports without a date range, e.g. `StatsSnapshotFilter { ContentKind? / ChannelId? }` + `StatsSnapshotLoadRequest { Filter, Refresh }`, or reuse `StatsLoadRequest` and ignore range/grouping — pick whichever keeps the cache key clean (range must not split the cache for a snapshot report) and say why. Do not change `StatsFilter` behavior for reports 01–04.
3. **Filter**: content kind — All / Website / Reusable / Email / Headless (from `ClassContentTypeType`; verify the stored values and the `ClassContentTypeType` constants in the API). Optional channel filter only if it is a cheap join (website items → `CMS_WebPageItem.WebPageItemWebsiteChannelID` → channel; email items → email channel); otherwise skip and say so.
4. **Queries** (repository), parameterized SQL, one DB round trip (multiple result sets OK):
   - Items by content type: `CMS_ContentItem` join `CMS_Class` → type display name, code name, kind, count. Include types with 0 items only if cheap (for "unused content types").
   - Language coverage: per `CMS_ContentLanguage`, count of items with a language variant vs total items (in the kind filter) → missing = total − with variant. Default language first.
   - Status of latest versions: group `CMS_ContentItemLanguageMetadata` by `ContentItemLanguageMetadataLatestVersionStatus` and whether `ContentItemLanguageMetadataContentWorkflowStepID` is set; also scheduled publish/unpublish (`...ScheduledPublishWhen` / `...ScheduledUnpublishWhen` not null). Map status ints via the product enum (`VersionStatus` in `CMS.ContentEngine` — verify names and values; do not hardcode magic numbers without the enum). Workflow step display names from `CMS_ContentWorkflowStep` if a step breakdown is cheap.
   - Verify column names/types in the DB first. Say what you found.
5. **Shared snapshot result** pieces: reuse `StatsRankedItem` / `StatsRankedResult` for type and status breakdowns. Add a small shared **coverage** model if nothing fits (e.g. `StatsCoverageItem { Key, Label, Covered, Total, Share }`) — later reusable for consent/contact-group style "x of y" data.
6. **Result** (suggested): `ContentInventoryResult { Kind, TotalItems, ByKind (ranked), ByContentType (ranked, all types), ByStatus (ranked/shares), LanguageCoverage (coverage), ContentTypeCount, LanguageCount, UpdatedAt }`.
7. **Service**: interface + `StatsCache`, same rules as other reports (5 min, no cache dependencies, refresh drops key; key by normalized snapshot filter). Builder + service tests (empty DB, one language, kind filter, coverage math, status mapping).
8. **Admin links**: optional, via `IStatsAdminLinks` only. E.g. content type row → its edit page in the Content types app, if the page type is public and parameters are simple. Otherwise skip and say so.

### Client (React/TS)

Shared:

- `StatsFilterBar`: add `showRange?: boolean` (default true) so snapshot reports hide date presets/custom range but keep refresh + "updated at". Or a separate small `SnapshotFilterBar` if cleaner — no change to reports 01–04 look.
- Content kind control: `NameToggleButtons` (All / Website / Reusable / Email / Headless). If generic, make it a shared option-toggle prop on the filter bar (e.g. `options` + `value` + `onChange`) rather than content-specific code in `shared/`.
- `CoverageBar` / coverage table if a coverage model is added: amCharts horizontal 100% stacked bar (covered vs missing) or reuse `RankedBarChart` with share — pick what looks native; same lifecycle/theme as other charts, colors from tokens/palette only.
- Types in `types.ts`.

Template `content-inventory/ContentInventoryTemplate.tsx`:

- Filter bar: kind toggle + refresh + updated at; no range, grouping.
- KPI row: content items, content types in use, languages, not published / in workflow (drafts + workflow steps).
- Tile "Items by content type": `RankedBarChart` (top N) / `RankedTable` (all), CSV.
- Tile "Status": `DonutChart` (published / draft / in workflow / other) / `ShareTable`, CSV.
- Tile "Language coverage": coverage chart / table (language, items with variant, missing, %), CSV.
- Short hint: counts are current state (latest version per language), not trends.
- No `DataRetentionNote` (not activity/contact data) unless something applies.

## Look and feel — must feel native

Same rules as report 01. References: `../xperience-by-kentico-admin-design-components` (`AGENTS.md`, `src/components/Charts/*`, `src/components/Table`, `src/templates/DashboardTemplate`, `previews/*.png`, `src/styles/tokens.css`), `../community-portal/src/Kentico.Community.Portal.Admin/Client/src/features/reports/` (amCharts examples only, not a spec). Prefer `@kentico/xperience-admin-components` exports; verify props in `node_modules/@kentico/xperience-admin-components/dist/*.d.ts`. CSS only via design tokens. Keep admin `Table` cells plain single-line (earlier CSS overrides looked wrong). Tile layout like the other reports.

## Goal

Shareable across customer projects: nothing DancingGoat-specific in `src/`, no hardcoded IDs/URLs/language names, values parameterized, works with 0 items, 1 language, no workflows.

## Done when

- `npm run typecheck` + `npm run build` pass in `src/Kentico.Xperience.Labs.SimpleStats.Admin/Client`.
- `dotnet build` of `Kentico.Xperience.Labs.SimpleStats.slnx` + `dotnet test` pass; new tests; reports 01–04 tests still pass.
- DancingGoat admin: nav shows 5 reports; content report renders real data (no seeding); kind filter, toggles, CSV work; reports 01–04 unchanged. Admin client uses Proxy mode (port 3050): `npm run watch` in Client must run, or rebuild + restart for client changes. Visual check if the app can be run (`docs/Contributing-Setup.md`, `.vscode/tasks.json`); otherwise say not checked.
- `docs/Usage-Guide.md` updated briefly.
- Do not commit. Report: files changed, SQL shape, column/enum findings, snapshot request decision, admin link approach (or why skipped), what was / was not visually checked.
