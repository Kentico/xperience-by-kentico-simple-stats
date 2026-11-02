# Slice 1 – Phase 1: Overview page

Goal, research, definitions and decisions: `PLAN.md`.

## Server

- Page `OverviewPage`, slug `overview`, name "Overview", parent `StatsApplicationPage`, order 50 (before Contacts at 100), a dashboard-style icon from `Icons` (pick an existing member), template `@kentico/xperience-labs-simple-stats-admin/Overview`, derived from `StatsReportPage<>`.
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
- Cost: each command calls a full report service, which runs that report's whole query batch. The 5-minute `StatsCache` makes repeat loads cheap. Measure the first load on a large database (Community Portal, see `report-12-email-summary` production check). Add lightweight summary queries only if it is slow; say so in the report-back.
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

- Same build, test, format and visual check rules as `.agent-resources/report-12-email-summary/implementation-spec-1.md` ("Look and feel, goal, done when").
- Every Overview number matches the full report for the same range and filter.
- Test users: Overview only (page with no tiles and a message explaining that tile permissions are needed); Overview + 2 report permissions (only those tiles); no Overview permission (lands on the first report as today).
- First load time on a large database written in the report-back.
- `docs/Usage-Guide.md`: Overview section, permission row (and that tiles need their report permissions), navigation table.
- Nothing DancingGoat-specific in `src/`. Do not commit.

## Report back

Concise: files, command list, shared changes, load time, permission checks, checked, not checked, open questions.
