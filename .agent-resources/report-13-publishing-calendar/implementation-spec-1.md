# Slice 1 – Publishing calendar

Goal, research, definitions and decisions: `PLAN.md`.

## Scope

### Server

1. **Page** `PublishingCalendarPage`, slug `publishing-calendar`, name "Publishing calendar", parent `StatsContentSection`, order 510, a calendar icon from `Icons` (pick an existing member), template `@kentico/xperience-labs-simple-stats-admin/PublishingCalendar`, derived from `StatsReportPage<>`.
   - Permission `StatsPermissions.PUBLISHING_CALENDAR` = `SimpleStats.PublishingCalendar`, "Publishing calendar".
   - `StatsNavigation` tests as for other reports.
2. **Filter**: snapshot request (`StatsSnapshot`), kind + channel exactly as Content inventory (reuse, do not copy), plus **window** (7 / 30 / 90). The window is part of the cache key; normalize unknown values to 30.
3. **Queries** (one round trip, parameterized, `TOP (@Limit)` on lists):
   - Upcoming events per day: day, publish count, unpublish count (window only).
   - Upcoming list: one row per event (a variant with both a publish and an unpublish gives two rows), ordered by time: time, action (publish / unpublish), name, content type, language, channel, last modified by (user display name), link columns.
   - Recently published list: name, type, language, channel, `LastPublishedWhen`, link columns. Ordered newest first.
   - Counts for KPIs (not limited by `TOP`).
4. **Result**: `PublishingCalendarResult { Window, Kind, ChannelId, UpcomingPublish, UpcomingUnpublish, UpcomingSend, RecentlyPublished, RecentDays, Days (publish/unpublish/send per day), Upcoming, Recent, UpdatedAt }`. Channel options are in the client properties (as Content inventory).
5. **Service**: interface + `StatsCache`, same rules as other reports. Tests: builder, service, SQL. Cover: no schedules; publish + unpublish on the same variant; window edges (exactly now, exactly now + window); kind/channel filter; days with no events filled with 0.

### Client

- Template `publishing-calendar/PublishingCalendarTemplate.tsx`, exported in `entry.tsx` with the export-permission wrapper.
- Filter bar: `SnapshotFilterBar` with kind + channel (as Content inventory) and a window toggle (7 / 30 / 90 days), refresh, updated at.
- KPI row: scheduled to publish, scheduled to unpublish, scheduled sends, recently published (last 7 days).
- Tile "Upcoming": `StackedColumnChart` per day (publish / unpublish) or table of events (time, action, item, type, language, channel, last modified by) with links. CSV.
- Tile "Recently published": table with links. CSV.
- Hints: the scheduler runs every minute; editing a scheduled item cancels its schedule; counts are per language variant.
- Export names: `publishing-calendar-upcoming`, `publishing-calendar-recent`. Add to the usage guide table.

## Local data

No seed SQL: scheduling sets workflow state that SQL should not fake. **Manual step (user)**: in DancingGoat, schedule ~5 publishes and ~3 unpublishes over the next 30 days across pages, reusable items and one email, and include one variant with both.

## Done when

- Same build, test, format and visual check rules as `.agent-resources/report-12-email-summary/implementation-spec-1.md` ("Look and feel, goal, done when").
- Scheduled items in the native Content hub / channel apps match the report rows.
- `docs/Usage-Guide.md`: report section, permission row, navigation table, export names.
- Nothing DancingGoat-specific in `src/`. Do not commit. Report back concise: files, SQL shape, shared changes, checks, not checked.
