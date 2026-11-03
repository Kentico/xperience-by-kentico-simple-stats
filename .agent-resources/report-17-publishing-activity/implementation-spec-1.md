# Slice 1 – Publishing activity

Goal, research, definitions and decisions: `PLAN.md`.

## Scope

### Server

1. **Page** `PublishingActivityPage`, slug `publishing-activity`, name "Publishing activity", parent `StatsContentSection`, order 550, icon from `Icons` (pick an existing member), template `@kentico/xperience-labs-simple-stats-admin/PublishingActivity`, derived from `StatsReportPage<>`.
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

- Same build, test, format and visual check rules as `.agent-resources/report-12-email-summary/implementation-spec-1.md`.
- Created count for one content type and range matches a SQL count by hand.
- `docs/Usage-Guide.md`: report section, permission row, navigation table, export names.
- Nothing DancingGoat-specific in `src/`. Do not commit. Report back concise.
