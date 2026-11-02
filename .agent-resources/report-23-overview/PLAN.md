# Plan: Report 23 – Overview (operations dashboard)

## Goal

Source: Claude Docs "Report 23: Overview (operations dashboard) and GA4 traffic" (<https://claude.ai/artifact/9Tafh96N1o5EbDvDjz4hXS>, 2026-10-30). GA4 part split out to `.agent-resources/report-24-google-analytics/`.

Audience: a marketing director, or the CMO of a smaller organization. Answers "what happened recently, what is coming up, and what needs my attention?" in one page, without opening 8 to 10 reports. The existing reports answer "what is in the system"; this page answers "what changed".

Phases, with a user decision between each:

- Phase 1 (`implementation-spec-1.md`): an Overview page built from the existing reports. No new data sources.
- Phase 2: GA4 traffic. See `.agent-resources/report-24-google-analytics/`.
- Phase 3 (user picks which): new data sources for attention items. See "Later: new data sources" below.

Read `.agent-resources/simple-stats/PLAN.md`, `.agent-resources/nav-sections/`, `.agent-resources/report-05-content-inventory/`, `.agent-resources/report-13-publishing-calendar/`, `.agent-resources/report-12-email-summary/`, `.agent-resources/report-06-event-log/` and `.agent-resources/report-21-campaign-sources/` first. Build on existing code; do not duplicate.

Placement as a dedicated admin application is an open UX question (user, 2026-10-30). Build the Overview as a page in Simple Stats (Labs), but keep its code in its own folders (`UIPages/OverviewPage.cs`, `Reports/Overview/`, `Client/src/overview/`) so it can move to a second `UIApplication` in the same package later without a rewrite.

## Research

### Data (checked in code 2026-10-30, not in a database)

No new SQL. Every tile calls an existing public service:

| Tile                             | Service and fields                                                                                                                      | Permission            |
| -------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------- | --------------------- |
| Needs attention: workflow        | `IContentInventoryService` → `Workflow.Overdue`, `Workflow.OverdueDays`                                                                 | `CONTENT_INVENTORY`   |
| Needs attention: forgotten edits | `IContentInventoryService` → `ForgottenEdits.Forgotten`                                                                                 | `CONTENT_INVENTORY`   |
| Needs attention: old locks       | `IContentLocksService` → `LockingEnabled`, `OldLockCount`, `OldLockDays`                                                                | `CONTENT_LOCKS`       |
| Needs attention: stale popular   | `IPageFreshnessService` → `StalePopularPages`, `StaleMonths`                                                                            | `PAGE_FRESHNESS`      |
| Coming up                        | `IPublishingCalendarService` (window 7) → `Upcoming`, `UpcomingPublish`, `UpcomingUnpublish`, `UpcomingSend`                            | `PUBLISHING_CALENDAR` |
| Recently published               | `IPublishingCalendarService` → `Recent`, `RecentDays`                                                                                   | `PUBLISHING_CALENDAR` |
| Emails                           | `IEmailSummaryService` → `Totals`, `ByClickRate`, `Available`                                                                           | `EMAIL_SUMMARY`       |
| Forms                            | `IFormSubmissionsService` → `Total`, `TotalComparison`, `Forms`                                                                         | `FORM_SUBMISSIONS`    |
| Contacts                         | `INewContactsService` → `Identified`, `Anonymous`, `IdentifiedShare` (no comparison yet, see `implementation-spec-1.md` Shared changes) | `NEW_CONTACTS`        |
| Members                          | `IMembersService` → `Totals`, `Available`                                                                                               | `MEMBERS`             |
| Orders                           | `IOrdersRevenueService` → `Totals`, `CommerceAvailable`                                                                                 | `ORDERS_REVENUE`      |
| Errors                           | `IEventLogReportService` → `Totals` (`EventLogTypeTotal` per type E / W, each with a `StatsComparison`), `LogSizeLimit`                 | `EVENT_LOG`           |
| Tracked traffic                  | `IActivityCountsService` → `Total` (consented contacts only)                                                                            | `ACTIVITY_COUNTS`     |

_Field names were read from code on 2026-10-30. Verify each before use; rename in this spec if they changed._

Known limits that the hints must repeat:

- "Days in workflow step" is a `ModifiedWhen` proxy. Version history does not record workflow step moves (simple-stats PLAN, Content management notes).
- `PublishingCalendarResult.Recent` uses a fixed 7-day window (`RecentDays`), not the Overview range. Fine for v1; say so in the hint.
- Activity data covers consented contacts only, and cleanup tasks trim it. The event log is trimmed by size.
- All times are server time.

## Design rules

### Persona and design rules

- Read in 5 to 10 seconds. A quick check before a meeting, not an analysis session. Leave analysis to the full reports.
- Four questions, in this order on the page: needs attention, coming up, what happened, is it working.
- Short time windows: 7, 14 or 30 days (default 7). Every count over a time range shows the change against the previous period of the same length.
- Every tile and attention item links to the full report (or to the native app) for the details.
- No AI. Any summary text is built from templates and counts.
- Show how fresh the data is (`UpdatedAt`), and say when a source is not set up (no tracking, no commerce, no locking) instead of showing zeros.

### Definitions

- **Range:** last 7, 14 or 30 days ending today (`StatsFilter` with From = today - (n - 1), Grouping = Day). Default 7.
- **Previous period:** `StatsComparison.GetPreviousRange`.
- **Attention item:** a count above 0 for a condition that someone should act on, with a tone (`StatsTone.Caution` or `Problem`), a short text ("3 items in a workflow step for more than 14 days") and a link to the report. Thresholds reuse each report's existing constants. No new thresholds, except the error rule below.
- **Error increase:** event log errors in the range > previous period, and at least 5 errors in the range. Server constant `ErrorIncreaseMinimum = 5`. Tone Problem.

### Out of scope

- AI-written summaries or insights.
- Alerts, notifications or scheduled emails (simple-stats PLAN out of scope).
- Real-time updates.
- User-arranged layouts (simple-stats PLAN out of scope). Show/hide tiles per user stays the optional simple-stats PLAN feature.

## Later phases

### Later: new data sources (Phase 3, each needs its own report spec first)

| Source                          | Data (verify tables before speccing)                                                                                                                              | Overview attention item                                                                             |
| ------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------- |
| Recycle bin                     | `CMS_RecycleBinContentItem` (simple-stats PLAN, Housekeeping); retention setting (default 30 days)                                                                | Items permanently deleted within 3 days (Caution)                                                   |
| Automation                      | Process step statistics (passed, waiting); contacts list with current step. Tables not yet checked                                                                | Contacts stuck at an end step that is not a Finish step; Wait steps with growing counts (Caution)   |
| Campaigns and customer journeys | `om.campaign`, `om.customerjourney`, `om.customerjourneystage` (CI/CD names); stage KPI targets; campaign start and end. Preview feature: expect breaking changes | Active campaign behind its final-stage KPI pace (Caution); campaign ending in 7 days (in Coming up) |
| Admin sign-ins                  | Not stored by default. Needs a small table filled from `AdminIdentityOptions.AuthenticationOptions.CookieEventsOptions.OnSigningIn`                               | None; a "team activity" tile                                                                        |

## Slices

1. Phase 1: Overview page (`implementation-spec-1.md`)

## Slice log

- 2026-11-02 – Slice 1 (Phase 1): Overview page, `SimpleStats.Overview` permission, comparisons for New contacts and Activity counts (`2b45409`).
