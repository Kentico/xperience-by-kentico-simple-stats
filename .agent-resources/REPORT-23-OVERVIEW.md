# Report 23: Overview (operations dashboard)

Source: Claude Docs "Report 23: Overview (operations dashboard) and GA4 traffic" (https://claude.ai/artifact/9Tafh96N1o5EbDvDjz4hXS, 2026-10-30). GA4 part split out to `REPORT-24-GOOGLE-ANALYTICS.md`.

Audience: a marketing director, or the CMO of a smaller organization. Answers "what happened recently, what is coming up, and what needs my attention?" in one page, without opening 8 to 10 reports. The existing reports answer "what is in the system"; this page answers "what changed".

Phases, with a user decision between each:

- Phase 1 (this spec): an Overview page built from the existing reports. No new data sources.
- Phase 2: GA4 traffic. See `REPORT-24-GOOGLE-ANALYTICS.md`.
- Phase 3 (user picks which): new data sources for attention items. See "Later: new data sources" below.

Read `PLAN.md`, `NAV-SECTIONS.md`, `REPORT-05-CONTENT-INVENTORY.md`, `REPORT-13-PUBLISHING-CALENDAR.md`, `REPORT-12-EMAIL-SUMMARY.md`, `REPORT-06-EVENT-LOG.md` and `REPORT-21-CAMPAIGN-SOURCES.md` first. Build on existing code; do not duplicate.

Placement as a dedicated admin application is an open UX question (user, 2026-10-30). Build the Overview as a page in Simple Stats (Labs), but keep its code in its own folders (`UIPages/OverviewPage.cs`, `Reports/Overview/`, `Client/src/overview/`) so it can move to a second `UIApplication` in the same package later without a rewrite.

## Persona and design rules

- Read in 5 to 10 seconds. A quick check before a meeting, not an analysis session. Leave analysis to the full reports.
- Four questions, in this order on the page: needs attention, coming up, what happened, is it working.
- Short time windows: 7, 14 or 30 days (default 7). Every count over a time range shows the change against the previous period of the same length.
- Every tile and attention item links to the full report (or to the native app) for the details.
- No AI. Any summary text is built from templates and counts.
- Show how fresh the data is (`UpdatedAt`), and say when a source is not set up (no tracking, no commerce, no locking) instead of showing zeros.

## Data (checked in code 2026-10-30, not in a database)

No new SQL. Every tile calls an existing public service:

| Tile                              | Service and fields                                                                                   | Permission            |
| --------------------------------- | ---------------------------------------------------------------------------------------------------- | --------------------- |
| Needs attention: workflow         | `IContentInventoryService` → `Workflow.Overdue`, `Workflow.OverdueDays`                              | `CONTENT_INVENTORY`   |
| Needs attention: forgotten edits  | `IContentInventoryService` → `ForgottenEdits.Forgotten`                                              | `CONTENT_INVENTORY`   |
| Needs attention: old locks        | `IContentLocksService` → `LockingEnabled`, `OldLockCount`, `OldLockDays`                             | `CONTENT_LOCKS`       |
| Needs attention: stale popular    | `IPageFreshnessService` → `StalePopularPages`, `StaleMonths`                                         | `PAGE_FRESHNESS`      |
| Coming up                         | `IPublishingCalendarService` (window 7) → `Upcoming`, `UpcomingPublish`, `UpcomingUnpublish`, `UpcomingSend` | `PUBLISHING_CALENDAR` |
| Recently published                | `IPublishingCalendarService` → `Recent`, `RecentDays`                                                | `PUBLISHING_CALENDAR` |
| Emails                            | `IEmailSummaryService` → `Totals`, `ByClickRate`, `Available`                                        | `EMAIL_SUMMARY`       |
| Forms                             | `IFormSubmissionsService` → `Total`, `TotalComparison`, `Forms`                                      | `FORM_SUBMISSIONS`    |
| Contacts                          | `INewContactsService` → `Identified`, `Anonymous`, `IdentifiedShare` (no comparison yet, see Shared changes) | `NEW_CONTACTS`        |
| Members                           | `IMembersService` → `Totals`, `Available`                                                            | `MEMBERS`             |
| Orders                            | `IOrdersRevenueService` → `Totals`, `CommerceAvailable`                                              | `ORDERS_REVENUE`      |
| Errors                            | `IEventLogReportService` → `Totals` (`EventLogTypeTotal` per type E / W, each with a `StatsComparison`), `LogSizeLimit` | `EVENT_LOG`           |
| Tracked traffic                   | `IActivityCountsService` → `Total` (consented contacts only)                                         | `ACTIVITY_COUNTS`     |

_Field names were read from code on 2026-10-30. Verify each before use; rename in this spec if they changed._

Known limits that the hints must repeat:

- "Days in workflow step" is a `ModifiedWhen` proxy. Version history does not record workflow step moves (PLAN, Content management notes).
- `PublishingCalendarResult.Recent` uses a fixed 7-day window (`RecentDays`), not the Overview range. Fine for v1; say so in the hint.
- Activity data covers consented contacts only, and cleanup tasks trim it. The event log is trimmed by size.
- All times are server time.

## Definitions

- **Range:** last 7, 14 or 30 days ending today (`StatsFilter` with From = today - (n - 1), Grouping = Day). Default 7.
- **Previous period:** `StatsComparison.GetPreviousRange`.
- **Attention item:** a count above 0 for a condition that someone should act on, with a tone (`StatsTone.Caution` or `Problem`), a short text ("3 items in a workflow step for more than 14 days") and a link to the report. Thresholds reuse each report's existing constants. No new thresholds, except the error rule below.
- **Error increase:** event log errors in the range > previous period, and at least 5 errors in the range. Server constant `ErrorIncreaseMinimum = 5`. Tone Problem.

## Server

- Page `OverviewPage`, slug `overview`, name "Overview", parent `StatsApplicationPage`, order 50 (before Contacts at 100), a dashboard-style icon from `Icons` (pick an existing member), template `@kentico/xperience-admin-labs-simple-stats/Overview`, derived from `StatsReportPage<>`.
- Permission `StatsPermissions.OVERVIEW` = `SimpleStats.Overview`, "Overview". Declare it on `StatsApplicationPage`.
- `StatsNavigation` already lands on the first page the user may open, so users with the Overview permission land here. Add tests: user with Overview lands on Overview; user without it lands on the first allowed section as today.
- Client properties: the list of tiles the user may see (server-evaluated, one `IUIPermissionEvaluator` call per source permission), the range options (7 / 14 / 30), `PagePath`. No report data in the initial properties, so the page opens fast.
- Commands: one page command per tile group, each guarded by the source report's permission:
  - `LOAD_CONTENT` (`CONTENT_INVENTORY`), `LOAD_LOCKS` (`CONTENT_LOCKS`), `LOAD_FRESHNESS` (`PAGE_FRESHNESS`), `LOAD_CALENDAR` (`PUBLISHING_CALENDAR`), `LOAD_EMAILS` (`EMAIL_SUMMARY`), `LOAD_FORMS` (`FORM_SUBMISSIONS`), `LOAD_CONTACTS` (`NEW_CONTACTS`), `LOAD_MEMBERS` (`MEMBERS`), `LOAD_ORDERS` (`ORDERS_REVENUE`), `LOAD_EVENTS` (`EVENT_LOG`), `LOAD_TRAFFIC` (`ACTIVITY_COUNTS`).
  - The page's own `UIEvaluatePermission(OVERVIEW)` is checked before every command, so each command needs both permissions.
  - Input: `StatsLoadRequest` (range tiles) or `StatsSnapshotLoadRequest` (current-state tiles). Snapshot tiles use all kinds and all channels. The calendar uses window 7.
- Results: small Overview records, not the full report results. `Reports/Overview/OverviewReportBuilder.cs` maps each service result to an `OverviewTileResult { Kpis, Rows, Attention, ReportPath, Available, UpdatedAt }` (names are suggestions):
  - `Kpis`: caption, value (or `StatsComparison`), tooltip.
  - `Rows`: up to 5 list rows (label, detail, date, `AdminPath`), for example upcoming schedules or top forms.
  - `Attention`: zero or more `OverviewAttentionItem { Key, Text, Count, Tone, AdminPath }`. `AdminPath` opens the full report.
  - `Available`: false when the source is not set up (`EmailSummaryResult.Available`, `CommerceAvailable`, `LockingEnabled`, members `Available`). The client shows a short "not set up" note, not zeros.
  - `ReportPath`: path of the full report page (`IPageLinkGenerator.GetPath<T>()`).
- Cost: each command calls a full report service, which runs that report's whole query batch. The 5-minute `StatsCache` makes repeat loads cheap. Measure the first load on a large database (Community Portal, see REPORT-12 production check). Add lightweight summary queries only if it is slow; say so in the report-back.
- Tests: builder per tile (attention items appear and disappear at the thresholds; `Available` = false; empty data); page tests for permissions (tile hidden without the source permission; command returns 403 without it); error increase rule edges (4 vs 5 errors, equal to previous).

## Client

- Template `overview/OverviewTemplate.tsx`, exported in `entry.tsx` with the export-permission wrapper.
- Filter bar: range toggle (7 / 14 / 30 days), refresh (all tiles), updated at (oldest tile time). No channel or kind filter in v1.
- Load tiles independently and in parallel, one `useStatsCommand` per tile with its own command name. Each tile has its own loading, error and "not set up" state. A slow or failing tile does not block the others.
- Layout, top to bottom:
  1. **Needs attention:** one list built from the `Attention` items of all loaded tiles, Problem first, then Caution. Each row: tone color, text, link. Empty state: "Nothing needs attention." Fills in as tiles finish loading.
  2. **Summary line** (optional, after all range tiles load): template text such as "Last 7 days: 14 items published, 2 emails sent, form submissions up 18%." Built only from loaded numbers; parts for hidden or unavailable tiles are left out.
  3. **KPI row** (5 to 7 cards): published (from Recent count), form submissions, new contacts, email click rate, orders or revenue (only when commerce is available), errors. `ComparisonInfoCard` where a comparison exists.
  4. **Coming up** (next 7 days): upcoming schedules, up to 5 rows, link to Publishing calendar.
  5. **Recently published:** up to 5 rows, link to Publishing calendar.
  6. **Emails:** sends, click rate, best and worst email by click rate, link to Email summary.
  7. **Forms:** top 3 forms by submissions, link to Form submissions.
  8. **System:** errors and warnings in the range with change, link to Event log.
- `InfoCard` has no status color. The attention list needs tone colors: use the same tone-to-color mapping as the ranked charts (`StatsTone`), in a small custom list component. Check the admin components package for a native status element first; use it if one exists.
- No CSV export on the Overview: every tile links to a report that has one.
- Hints: data freshness per tile; "Days in step are based on the last change"; "Traffic counts only contacts who consented to tracking".

## Shared changes

- `NewContactsResult` has no comparison. Add `StatsComparison` for identified and anonymous contacts (and show it in the New contacts report too). Check `ActivityCountsResult`, `MembersTotals`, `OrdersRevenueTotals` and `EmailSummaryTotals` for comparisons and add only where missing.
- Keep thresholds in their reports (`ContentInventoryReportBuilder.OverdueDays`, the locks and freshness constants). The Overview reads them from the results; it does not copy them.

## Done when

- Same build, test, format and visual check rules as `REPORT-12-EMAIL-SUMMARY.md` ("Look and feel, goal, done when").
- Every Overview number matches the full report for the same range and filter.
- Test users: Overview only (page with no tiles and a message explaining that tile permissions are needed); Overview + 2 report permissions (only those tiles); no Overview permission (lands on the first report as today).
- First load time on a large database written in the report-back.
- `docs/Usage-Guide.md`: Overview section, permission row (and that tiles need their report permissions), navigation table.
- Nothing DancingGoat-specific in `src/`. Do not commit.

## Later: new data sources (Phase 3, each needs its own report spec first)

| Source                          | Data (verify tables before speccing)                                                                                                                       | Overview attention item                                                                                     |
| ------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------- |
| Recycle bin                     | `CMS_RecycleBinContentItem` (PLAN, Housekeeping); retention setting (default 30 days)                                                                      | Items permanently deleted within 3 days (Caution)                                                           |
| Automation                      | Process step statistics (passed, waiting); contacts list with current step. Tables not yet checked                                                         | Contacts stuck at an end step that is not a Finish step; Wait steps with growing counts (Caution)          |
| Campaigns and customer journeys | `om.campaign`, `om.customerjourney`, `om.customerjourneystage` (CI/CD names); stage KPI targets; campaign start and end. Preview feature: expect breaking changes | Active campaign behind its final-stage KPI pace (Caution); campaign ending in 7 days (in Coming up)         |
| Admin sign-ins                  | Not stored by default. Needs a small table filled from `AdminIdentityOptions.AuthenticationOptions.CookieEventsOptions.OnSigningIn`                         | None; a "team activity" tile                                                                                |

## Out of scope

- AI-written summaries or insights.
- Alerts, notifications or scheduled emails (PLAN out of scope).
- Real-time updates.
- User-arranged layouts (PLAN out of scope). Show/hide tiles per user stays the optional PLAN feature.

## Open questions

- Overview as a page in Simple Stats or its own application: decide after using Phase 1 (code kept movable, see top).
- Email attention items (high unsubscribe or bounce rate): thresholds are not defined, so none in Phase 1. User to decide.
- Default range 7 or 14 days.

## Report back

Concise: files, command list, shared changes, load time, permission checks, checked, not checked, open questions.
