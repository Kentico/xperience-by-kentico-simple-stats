# Slice 4 – Round 4: Forgotten edits (2026-10-08)

New tile in Content inventory. simple-stats PLAN "Content management → Needs attention": forgotten edits. Answers "which live items have unpublished changes that nobody finished?" (the live site differs from what editors see). Decided against a new page: same audience, filters, links and `AgedItemTable` as "Action needed". Per-user breakdown ("drafts by last modifier") is **out of scope** here (per-user data belongs behind the Editor contributions permission).

## Data (checked in local DB 2026-10-08)

- A variant has a **newer draft of published content** when it has a `CMS_ContentItemCommonData` row with `VersionStatus` = Published and `IsLatest` = 0. The latest row is the draft; `CMS_ContentItemLanguageMetadata.LatestVersionStatus` is Draft. Map statuses with the product `VersionStatus` enum (as `ContentInventoryReportBuilder` does); no magic numbers.
- Locally 18 variants: 16 not in a workflow step (oldest draft change 2025-09-10), 2 in a workflow step.
- **Draft since**: `ContentItemLanguageMetadataModifiedWhen` (last change of the draft). **Live since**: `ContentItemCommonDataLastPublishedWhen` of the published row.
- Initial drafts (never published) are not forgotten edits; they are already in the Status tile.
- Drafts with `ScheduledPublishWhen` set are planned, not forgotten; left out (Publishing calendar shows them, overdue included). Locally 10 of the 18 (user feedback 2026-10-08).
- Content version history does not help here (it records publish actions, not draft saves).

## Definitions

- **Forgotten edit**: newer draft of published content whose draft was last changed more than 14 days ago. Reuse the `OverdueDays` constant (or a sibling constant with the same value; say which).
- Items in a workflow step are included, with the step name in the detail column (they also show in "Action needed"; the tile hint says so).

## Scope

- Server: add to the existing Content inventory query batch (one round trip): count of newer drafts of published content, count over the threshold, list oldest draft first `TOP (@Limit)` with window count before `TOP` (as `OverdueCount`). Row: name, content type, language, channel, draft since, live since, workflow step (if any), link columns. Kind/channel filter as the rest of the report. Add to `ContentInventoryResult` (e.g. `PendingDrafts`, `ForgottenEditCount`, `ForgottenEdits`).
- Client: tile "Forgotten edits: unpublished changes" placed after "Action needed". `AgedItemTable` by days since the draft changed (Since = draft since; detail = "Live since <date>" plus the step name when set), bars over the threshold highlighted, item links. CSV export name `content-inventory-forgotten-edits`. Hint: "Published items with a newer draft. Visitors see the published version until the draft is published. Items in workflow steps also appear in Action needed." Empty message when none.
- Optional KPI in the existing KPI row only if it fits without wrapping; otherwise skip and say so.
- Tests: published + newer draft (in list); initial draft only (not in list); published without draft (not in list); draft within threshold (counted as pending, not forgotten); item in workflow step (included with step); several languages; kind/channel filter.
- `docs/Usage-Guide.md`: Content inventory section (tile + definition), export names.

## Local data

18 variants exist; 5 over the threshold (checked 2026-10-08). No seeding needed.

## Done when

- Same build, test, format and visual check rules as `.agent-resources/report-12-email-summary/implementation-spec-1.md`.
- For 2 items, the Content hub shows a draft of a published item and the dates match.
- Nothing DancingGoat-specific in `src/`. Do not commit. Report back concise.
