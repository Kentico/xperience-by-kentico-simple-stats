# Plan: Report 13 – Publishing calendar

## Goal

Audience: content marketers and editors. Answers "what goes live, comes down or is sent soon, and what was published recently?" simple-stats PLAN "Later candidates → Content management": publishing calendar + ending soon. Current-state report in the **Content** section, after Content inventory. Read `.agent-resources/simple-stats/PLAN.md`, `.agent-resources/report-05-content-inventory/` (snapshot infra, kind/channel filter, item links) and `.agent-resources/nav-sections/` first. Build on existing code; do not duplicate.

## Research

### Data (checked in local DB 2026-10-06)

All columns are already read by `ContentInventorySql`:

- `CMS_ContentItemLanguageMetadata`: `...ScheduledPublishWhen`, `...ScheduledUnpublishWhen`, `...LatestVersionStatus`, `...DisplayName`, `...ModifiedByUserID`, `...ContentLanguageID`, `...ContentItemID`. One row per item and language.
- `CMS_ContentItemCommonData.ContentItemCommonDataLastPublishedWhen` for "recently published" (latest version row, `ContentItemCommonDataIsLatest = 1`).
- Item, content type, kind and channel filter: same joins as `ContentInventorySql` (`ItemsWhere`, `VariantsFrom`). Item admin links: same `LinkColumns` / `LinkApply` + `StatsChannelItemPaths`.

Product behavior (docs: Content items → Scheduled publishing, Unpublish):

- The system checks for scheduled items once per minute. Large batches can take several minutes.
- A scheduled item cannot be edited. Editing it cancels the schedule.
- An unpublish can be scheduled while a publish is also scheduled (unpublish time is after publish time).

Local data: 0 scheduled publishes, 0 scheduled unpublishes. See Local data in `implementation-spec-1.md`.

## Design rules

### Definitions

- **Scheduled publish / unpublish**: a language variant with `ScheduledPublishWhen` / `ScheduledUnpublishWhen` set.
- **Upcoming**: scheduled time from now to now + window. Window: 7, 30 or 90 days (default 30).
- **Scheduled send**: a regular email with a send configuration in status Scheduled (see `implementation-spec-2.md`).
- **Recently published**: `LastPublishedWhen` in the last 7 days (fixed, server constant).
- Times follow the same server-time rule as the other reports.

## Slices

1. Publishing calendar (`implementation-spec-1.md`)
2. Round 2 (user feedback 2026-10-06) (`implementation-spec-2.md`)

## Slice log

- 2026-10-06 – Slices 1–2: Publishing calendar report, scheduled sends, missed schedules removed, no headless, content hub channel label (`f1b2e63`).
- 2026-10-09 – Perf: events read once into `@Events` (`a6a0f90`); SQL integration tests (`af1f353`); style (`3972a8e`).
