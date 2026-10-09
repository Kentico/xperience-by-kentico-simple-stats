# Report 05: Content inventory

Fifth report. First **current-state (snapshot)** report: no date range. Audience: administrators and content leads (PLAN "Later candidates → Content"). Adds shared **snapshot** infra (server + client) that later current-state reports reuse (contact group sizes, reusable items with no usages, stale content, recipient lists). Read `PLAN.md` and `REPORT-01..04-*.md` first. Build on existing code; do not duplicate.

## Why this report next

Checked local DancingGoat DB (`mssql2022` docker, DB `xperience-by-kentico-simple-stats`, creds in `examples/DancingGoat/appsettings.json`; from Git Bash prefix `MSYS_NO_PATHCONV=1 docker exec mssql2022 /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P ... -d xperience-by-kentico-simple-stats -W -Q "..."`):

- Remaining first-release reports need seeding before anything shows: `EmailLibrary_EmailStatistics` = 0 rows (email summary), `ActivityURLReferrer` = 3 rows (top referrers).
- Content has real data now, no seeding: 185 `CMS_ContentItem` rows over 29 content types (`CMS_Class.ClassContentTypeType`: Reusable 111, Website 74 incl. Email 7), 195 `CMS_ContentItemLanguageMetadata` rows, languages `en` (185) and `es` (10) → missing translations are visible right away. Statuses: 182 published, 2 draft, 1 in a workflow step (1 workflow exists). 2 channels (website + email).
- One aggregate query per dimension. Low complexity.

## Current repo state (after reports 01–04)

- Server `src/Kentico.Xperience.Labs.SimpleStats.Admin/`: `UIPages/` pages (copy `NewContactsPage.cs`), `Shared/` (`StatsFilter`/`StatsQuery`, `StatsGrouping`, `StatsPeriods`, `StatsLoadRequest`, `StatsCache`, `StatsChannelOptions`, `StatsRanked` (`StatsRankedBuilder`, opt-in keep zeros), `StatsTimeSeries`, `StatsComparison`, `StatsAdminLinks` (`IStatsAdminLinks.GetPath<TPage>`)), `Reports/<Name>/` Models / Repository / ReportBuilder / Service (+ `FormSubmissionsSql`). DI in `SimpleStatsWebAdminModule.cs`.
- Client `Client/src/shared/`: `StatsFilterBar` (`showGrouping`, `showChannel`, refresh, `updatedAt`), `StatsTile`, `StackedColumnChart`, `RankedBarChart`, `RankedTable` (admin links), `DonutChart`, `ShareTable`, `TimeSeriesTable`, `adminLinks.ts`, `chartTheme.ts`, `csv.ts`, `table.ts`, `timeSeries.ts`, `format.ts`, `dates.ts`, `types.ts`, `useStatsCommand.ts`, `DataRetentionNote`, `stats.css`. Templates in `activity-counts/`, `top-pages/`, `new-contacts/`, `form-submissions/`; export each in `entry.tsx`.
- Tests: `tests/Kentico.Xperience.Labs.SimpleStats.Admin.Tests` (NUnit), `TestDoubles.cs`.

## Scope

### Server (C#)

1. **Page** `ContentInventoryPage`, slug `content-inventory`, name "Content inventory", order 500, content-like icon (verify in `Icons`), template `@kentico/xperience-admin-labs-simple-stats/ContentInventory`. Same permission pattern as other pages.
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
- DancingGoat admin: nav shows 5 reports; content report renders real data (no seeding); kind filter, toggles, CSV work; reports 01–04 unchanged. Admin client uses Proxy mode (port 3009): `npm run watch` in Client must run, or rebuild + restart for client changes. Visual check if the app can be run (`docs/Contributing-Setup.md`, `.vscode/tasks.json`); otherwise say not checked.
- `docs/Usage-Guide.md` updated briefly.
- Do not commit. Report: files changed, SQL shape, column/enum findings, snapshot request decision, admin link approach (or why skipped), what was / was not visually checked.

## Round 2 (user feedback after first build)

Overall approved. Changes:

1. **Kind + channel filters pair badly.** Selecting a website channel leaves only one kind with data. Fix: channel depends on kind.
   - All / Reusable → no channel select (reusable items have no channel).
   - Pages → website channels only; Emails → email channels only; Headless → headless channels only (extend channel options provider without changing reports 01–04 output).
   - Changing kind resets channel when it no longer fits. Server `Normalize` drops a channel that does not match the kind (cache key stays clean). Tests.
2. **Stale content** ("Content not modified in 6 or 12 months"). Use `ContentItemLanguageMetadataModifiedWhen` (verify it means last edit of latest version). Tile: age buckets (e.g. < 3 mo, 3–6, 6–12, > 12 mo) + list of oldest items (ranked by days since modified, TOP N in SQL) with admin link if simple. KPI: items not modified in 12 months. Respects kind/channel filter.
3. **Reusable items with no usages.** Use `IInfoProvider<ContentItemReferenceInfo>` (table `CMS_ContentItemReference`, 167 rows locally). Reference: `../xperience-by-kentico-content-model-graph/src/Kentico.Xperience.ContentModelGraph/ContentItemRelationshipGraphBuilder.cs` (~line 73). Do it as SQL aggregate (NOT EXISTS against the reference table's target column) rather than loading all references in memory, if the column names are clear; verify. Tile/KPI: unused reusable items count + list (by content type, oldest first). Note what counts as a usage (content item references only; e.g. rich text/Page Builder widget references may not be tracked — verify and state in a hint).
4. **Items in a workflow step for a long time ("action needed").** Combine workflow step + age. Find the best "entered step" time: check whether any table/column records it (version history, workflow step change time); if not, use `ModifiedWhen` as a proxy and say so in a hint. Tile: items in workflow steps with step name, days in step, sorted oldest first; highlight > N days (server constant, e.g. 14). Replace or extend the existing "Items in workflow steps" tile.

Local data note: all content was modified today (seeded DB), so stale/age tiles will be mostly empty. For visual testing, it is OK to back-date `ContentItemLanguageMetadataModifiedWhen` on a few items in the **local dev DB only**; save the script as `.agent-resources/seed-content-age.sql`. Never put seeding in `src/`.

Page layout: keep it scannable. If the page gets long, order tiles: action needed → stale → unused reusable → type → status → language coverage, or group visually. Pick what looks native.

## Round 3 (user feedback)

1. **Usages:** rich text links are counted in `CMS_ContentItemReference`, plus any component with a registered reference extractor (see Kentico docs). Update hint + usage guide: no "rich text not verified" caveat.
2. **Content item admin links:** use the API in `../xperience-by-kentico-content-model-graph/src/Kentico.Xperience.ContentModelGraph/ContentItemRelationshipGraphBuilder.cs` for generating admin UI links, and `AdminUrlHelper` (used in `ContentModelGraphBuilder.cs` ~L768) that prepares generated URLs for client-side use. Compare with our `StatsAdminLinks` + `adminLinks.ts`; reuse/align rather than duplicate. Add links for items in oldest content, unused reusable, action needed lists.
3. **Layout:** "Content age" bar chart and "Status" donut use predictable, limited space → stack both vertically in a right column next to "Oldest content" (left, wider). Stack on narrow screens.

## Round 4: Forgotten edits (2026-10-08)

New tile in Content inventory. PLAN "Content management → Needs attention": forgotten edits. Answers "which live items have unpublished changes that nobody finished?" (the live site differs from what editors see). Decided against a new page: same audience, filters, links and `AgedItemTable` as "Action needed". Per-user breakdown ("drafts by last modifier") is **out of scope** here (per-user data belongs behind the Editor contributions permission).

### Data (checked in local DB 2026-10-08)

- A variant has a **newer draft of published content** when it has a `CMS_ContentItemCommonData` row with `VersionStatus` = Published and `IsLatest` = 0. The latest row is the draft; `CMS_ContentItemLanguageMetadata.LatestVersionStatus` is Draft. Map statuses with the product `VersionStatus` enum (as `ContentInventoryReportBuilder` does); no magic numbers.
- Locally 18 variants: 16 not in a workflow step (oldest draft change 2025-09-10), 2 in a workflow step.
- **Draft since**: `ContentItemLanguageMetadataModifiedWhen` (last change of the draft). **Live since**: `ContentItemCommonDataLastPublishedWhen` of the published row.
- Initial drafts (never published) are not forgotten edits; they are already in the Status tile.
- Drafts with `ScheduledPublishWhen` set are planned, not forgotten; left out (Publishing calendar shows them, overdue included). Locally 10 of the 18 (user feedback 2026-10-08).
- Content version history does not help here (it records publish actions, not draft saves).

### Definitions

- **Forgotten edit**: newer draft of published content whose draft was last changed more than 14 days ago. Reuse the `OverdueDays` constant (or a sibling constant with the same value; say which).
- Items in a workflow step are included, with the step name in the detail column (they also show in "Action needed"; the tile hint says so).

### Scope

- Server: add to the existing Content inventory query batch (one round trip): count of newer drafts of published content, count over the threshold, list oldest draft first `TOP (@Limit)` with window count before `TOP` (as `OverdueCount`). Row: name, content type, language, channel, draft since, live since, workflow step (if any), link columns. Kind/channel filter as the rest of the report. Add to `ContentInventoryResult` (e.g. `PendingDrafts`, `ForgottenEditCount`, `ForgottenEdits`).
- Client: tile "Forgotten edits: unpublished changes" placed after "Action needed". `AgedItemTable` by days since the draft changed (Since = draft since; detail = "Live since <date>" plus the step name when set), bars over the threshold highlighted, item links. CSV export name `content-inventory-forgotten-edits`. Hint: "Published items with a newer draft. Visitors see the published version until the draft is published. Items in workflow steps also appear in Action needed." Empty message when none.
- Optional KPI in the existing KPI row only if it fits without wrapping; otherwise skip and say so.
- Tests: published + newer draft (in list); initial draft only (not in list); published without draft (not in list); draft within threshold (counted as pending, not forgotten); item in workflow step (included with step); several languages; kind/channel filter.
- `docs/Usage-Guide.md`: Content inventory section (tile + definition), export names.

### Local data

18 variants exist; 5 over the threshold (checked 2026-10-08). No seeding needed.

### Done when

- Same build, test, format and visual check rules as `REPORT-12-EMAIL-SUMMARY.md`.
- For 2 items, the Content hub shows a draft of a published item and the dates match.
- Nothing DancingGoat-specific in `src/`. Do not commit. Report back concise.
