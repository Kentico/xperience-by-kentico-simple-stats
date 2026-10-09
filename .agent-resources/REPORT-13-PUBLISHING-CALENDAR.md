# Report 13: Publishing calendar

Audience: content marketers and editors. Answers "what goes live, comes down or is sent soon, and what was published recently?" PLAN "Later candidates → Content management": publishing calendar + ending soon. Current-state report in the **Content** section, after Content inventory. Read `PLAN.md`, `REPORT-05-CONTENT-INVENTORY.md` (snapshot infra, kind/channel filter, item links) and `NAV-SECTIONS.md` first. Build on existing code; do not duplicate.

## Data (checked in local DB 2026-10-06)

All columns are already read by `ContentInventorySql`:

- `CMS_ContentItemLanguageMetadata`: `...ScheduledPublishWhen`, `...ScheduledUnpublishWhen`, `...LatestVersionStatus`, `...DisplayName`, `...ModifiedByUserID`, `...ContentLanguageID`, `...ContentItemID`. One row per item and language.
- `CMS_ContentItemCommonData.ContentItemCommonDataLastPublishedWhen` for "recently published" (latest version row, `ContentItemCommonDataIsLatest = 1`).
- Item, content type, kind and channel filter: same joins as `ContentInventorySql` (`ItemsWhere`, `VariantsFrom`). Item admin links: same `LinkColumns` / `LinkApply` + `StatsChannelItemPaths`.

Product behavior (docs: Content items → Scheduled publishing, Unpublish):

- The system checks for scheduled items once per minute. Large batches can take several minutes.
- A scheduled item cannot be edited. Editing it cancels the schedule.
- An unpublish can be scheduled while a publish is also scheduled (unpublish time is after publish time).

Local data: 0 scheduled publishes, 0 scheduled unpublishes. See Local data below.

## Definitions

- **Scheduled publish / unpublish**: a language variant with `ScheduledPublishWhen` / `ScheduledUnpublishWhen` set.
- **Upcoming**: scheduled time from now to now + window. Window: 7, 30 or 90 days (default 30).
- **Scheduled send**: a regular email with a send configuration in status Scheduled (see Round 2).
- **Recently published**: `LastPublishedWhen` in the last 7 days (fixed, server constant).
- Times follow the same server-time rule as the other reports.

## Scope

### Server

1. **Page** `PublishingCalendarPage`, slug `publishing-calendar`, name "Publishing calendar", parent `StatsContentSection`, order 510, a calendar icon from `Icons` (pick an existing member), template `@kentico/xperience-admin-labs-simple-stats/PublishingCalendar`, derived from `StatsReportPage<>`.
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

- Same build, test, format and visual check rules as `REPORT-12-EMAIL-SUMMARY.md` ("Look and feel, goal, done when").
- Scheduled items in the native Content hub / channel apps match the report rows.
- `docs/Usage-Guide.md`: report section, permission row, navigation table, export names.
- Nothing DancingGoat-specific in `src/`. Do not commit. Report back concise: files, SQL shape, shared changes, checks, not checked.

## Round 2 (user feedback 2026-10-06)

1. **Visibility**: types are internal unless a public signature, the page command API or client serialization needs them. Internal: `ContentItemLink`, `ContentItemLocation`, `StatsContentKinds`, repository data and row records of both content reports (`Content*Row`, `UnusedItemRow`, `ContentInventoryData`, `Publishing*Row`, `PublishingCalendarData`). Public: result types, `PublishingAction`, `PublishingCalendarQuery` (in `IPublishingCalendarService`), service interfaces, pages and client properties.
2. **Scheduled sends** as a third action, "Send": `EmailLibrary_SendConfiguration` with `SendConfigurationStatus` = `SendConfigurationStatus.Scheduled`, time `SendConfigurationScheduledTime`, joined to `EmailLibrary_EmailConfiguration` with purpose `EmailPurpose.Regular`. Name, language and channel as in report 12 (email channel's primary language first, then lowest metadata ID). Link: the email (existing email path). Counts toward the per-day chart ("Send" series), the upcoming list and its own KPI (scheduled sends). Kind/channel filter through the email's content item: All and Emails include sends, Pages/Reusable/Headless exclude them (the send part is not queried). The send part is appended to the batch after an availability check; without the email tables it is empty and the content part still works. Counts are per email, not per variant. Local data: `DancingGoatRegular_EmailBuilder-aydyp6ht_14`, scheduled 2026-10-23 08:45.
3. **Missed schedules removed** (first "overdue", then "missed": a schedule whose time passed but is still set). The user does not think this can happen in Xperience, and if it did, it would be a system problem, not something content marketers should look for. Removed everywhere: the missed result sets and counts of the content and send SQL, `@MissedBefore`, `MissedMinutes`, `GetMissedBefore`, the result fields, the client tile, the `publishing-calendar-missed` export and the usage guide. The send part now reads only sends in the upcoming window.
4. **Bug**: `PublishingAction` reached the client as numbers (no `JsonStringEnumConverter`), so the Action column showed "–" or empty. It now has the converter; a test checks the serialized names.

5. **No headless**: headless items have no scheduled publish or unpublish in Xperience, so the report does not include them. The kind filter has no Headless option and the channel filter no headless channels; server `Normalize` turns a Headless kind or a headless channel into All. Content inventory keeps Headless; the shared SQL and link handling stay generic.

6. **Channel of reusable items**: in every list (upcoming, recently published) and their CSVs, reusable items (`CMS_ContentItem.ContentItemIsReusable`) show "Content hub - <workspace display name>" in the Channel column (`CMS_ContentItem.ContentItemWorkspaceID` → `CMS_Workspace.WorkspaceDisplayName`, left join), or "Content hub" without a workspace. Other items without a channel (and sends) stay empty. The SQL returns `IsReusable` and `WorkspaceDisplayName`; the report builder builds the text (`PublishingCalendarReportBuilder.ContentHubLabel`, the only place of "Content hub" on the server); the client shows the text as sent.