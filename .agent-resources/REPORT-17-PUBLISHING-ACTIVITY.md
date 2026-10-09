# Report 17: Publishing activity

Audience: content leads. Answers "how much new content do we create and publish, and how long does it take to go live?" PLAN "Later candidates → Content management → Publishing speed": published over time, created over time by content type, time to publish. Time-range report in the **Content** section. Read `PLAN.md`, `REPORT-03-NEW-CONTACTS.md` (time series), `REPORT-05-CONTENT-INVENTORY.md` (kind/channel filter, item links) first. Build on existing code; do not duplicate.

## Data (checked in local DB 2026-10-07)

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

## Definitions

- **Created**: variants with `CreatedWhen` in the range.
- **First published**: variants with first published in the range (the item went live for the first time in that language).
- **Updates**: publish version rows in the range that are not first publishes (see Data). Only while version history is enabled.
- **Time to publish**: days from `CreatedWhen` to first published, for variants first published in the range. Median and 90th percentile (SQL `PERCENTILE_CONT`, or in C# if simpler; say which).

## Scope

### Server

1. **Page** `PublishingActivityPage`, slug `publishing-activity`, name "Publishing activity", parent `StatsContentSection`, order 550, icon from `Icons` (pick an existing member), template `@kentico/xperience-admin-labs-simple-stats/PublishingActivity`, derived from `StatsReportPage<>`.
   - Permission `StatsPermissions.PUBLISHING_ACTIVITY` = `SimpleStats.PublishingActivity`, "Publishing activity".
2. **Filter**: `StatsFilter` range + grouping + kind + channel (reuse what Content inventory / Page freshness use). Do not change other reports' cache keys.
3. **Queries** (one round trip, parameterized):
   - Per variant CTE: item, language, type, kind, created, first published (`MIN`).
   - Created, first published and updates per period (grouping), as three series (updates only when history is enabled).
   - Per content type in the range: created, first published, updates, median days to publish.
   - Slowest to publish, `TOP (@Limit)`: name, type, language, created, first published, days. Links.
   - Totals for the previous period of the same length (reuse `StatsComparison`).
4. **Result** (suggested): `PublishingActivityResult { From, To, Grouping, Kind, ChannelId, Created, FirstPublished, Updates, VersionHistoryEnabled, MedianDaysToPublish, PublishedDateUnknown, Comparison, Series, ByContentType, Slowest, ChannelOptions, UpdatedAt }`.
5. **Service**: interface + `StatsCache`. Tests: history disabled (no updates series); first publish not counted as update; several `CommonData` rows per variant (MIN wins, counted once); published without date (not in series, counted in unknown); first published before created (left out of duration); range edges; kind/channel filter; folders excluded.

### Client

- Template `publishing-activity/PublishingActivityTemplate.tsx`, exported in `entry.tsx` with the export-permission wrapper.
- Filter bar: range, grouping, kind, channel, refresh, updated at.
- KPI row: created, first published, updates (each with change vs previous period), median days to publish.
- Tile "Created and published" (wide): column chart, created / first published / updates / `TimeSeriesTable`. CSV.
- Tile "By content type": `RankedTable` (type, created, first published, updates, median days). CSV.
- Tile "Slowest to publish": table with links. CSV.
- Hint: updates come from content version history (only since it was enabled; trimmed to N versions per item). Published items with no first publish date (migrated content) are shown as a count.
- Export names: `publishing-activity-series`, `publishing-activity-types`, `publishing-activity-slowest`.

## Local data

Created/published data exists across 3 years; use a 12-month or custom range. Version history: user enabled it and created versions of several items on 2026-10-07 (13 rows); use a 7-day range for updates. No seeding needed.

## Done when

- Same build, test, format and visual check rules as `REPORT-12-EMAIL-SUMMARY.md`.
- Created count for one content type and range matches a SQL count by hand.
- `docs/Usage-Guide.md`: report section, permission row, navigation table, export names.
- Nothing DancingGoat-specific in `src/`. Do not commit. Report back concise.
