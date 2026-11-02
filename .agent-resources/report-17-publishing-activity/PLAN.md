# Plan: Report 17 – Publishing activity

## Goal

Audience: content leads. Answers "how much new content do we create and publish, and how long does it take to go live?" simple-stats PLAN "Later candidates → Content management → Publishing speed": published over time, created over time by content type, time to publish. Time-range report in the **Content** section. Read `.agent-resources/simple-stats/PLAN.md`, `.agent-resources/report-03-new-contacts/` (time series), `.agent-resources/report-05-content-inventory/` (kind/channel filter, item links) first. Build on existing code; do not duplicate.

## Research

### Data (checked in local DB 2026-10-07)

- Created: `CMS_ContentItemLanguageMetadata.ContentItemLanguageMetadataCreatedWhen` (per language variant). 195 variants, Jun 2023 – Oct 2026.
- First published: `CMS_ContentItemCommonData.ContentItemCommonDataFirstPublishedWhen`. Several `CommonData` rows per variant (published + newer draft, `...IsLatest`). Take `MIN` per variant (content item + language). Values exist 2024-09 – 2026-10.
- 29 published rows have no first published date (migrated / seeded content). Count them as "published, date unknown", never as unpublished.
- No row has first published before created locally, but guard it: negative durations are left out of time to publish.
- `LastPublishedWhen` holds only the latest publish. **Updates** (republishes) come from content version history (checked 2026-10-07, after the user enabled it):
  - `CMS_ContentItemVersion`: `ContentItemVersionContentItemID`, `...ContentLanguageID`, `...Action` (int), `...CreatedWhen`, `...CreatedByUserID`, `...ActionScheduledWhen`, `...Data`. Locally 13 rows, 7 items, all `Action` = 0, created 2026-10-07 by user 53.
  - Map `Action` with the product enum (find it in `CMS.ContentEngine`; do not hardcode). Verify which value is publish vs unpublish vs scheduled, and whether a first publish also writes a version row.
  - Settings `CMSContentVersionHistoryEnable` (True locally) and `CMSContentVersionHistoryLength` (20 = versions kept per variant; older trimmed). Read with the settings API.
  - **Update** = a publish version row that is not the variant's first publish. Count per period as a third series.
  - When history is disabled: hide the updates series/KPI and show a hint. Always hint that history only starts when it was enabled and is trimmed to N versions per item.
- Content type, kind, channel filter, page folders excluded, item links: as `ContentInventorySql`.

## Design rules

### Definitions

- **Created**: variants with `CreatedWhen` in the range.
- **First published**: variants with first published in the range (the item went live for the first time in that language).
- **Updates**: publish version rows in the range that are not first publishes (see Data). Only while version history is enabled.
- **Time to publish**: days from `CreatedWhen` to first published, for variants first published in the range. Median and 90th percentile (SQL `PERCENTILE_CONT`, or in C# if simpler; say which).

## Slices

1. Publishing activity (`implementation-spec-1.md`)

## Slice log

- 2026-10-07 – Slice 1: Publishing activity report (`65291f8`; spec `4d1d850`).
- 2026-10-09 – Perf: updates limited to range rows (`80d524c`); SQL integration tests (`af1f353`).
