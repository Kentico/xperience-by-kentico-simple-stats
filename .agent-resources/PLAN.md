# Xperience by Kentico Labs: Simple Stats

Planning notes for a Kentico Labs extension that shows basic charts and tables about Xperience by Kentico data in the administration UI.

## Naming

- **Title:** Xperience by Kentico Labs: Simple Stats
- **Repo:** `xperience-by-kentico-simple-stats`
- **NuGet package:** `Kentico.Xperience.Labs.SimpleStats.Admin`
- **Admin application name:** Simple Stats (Labs)

## Scope

This project is a Labs example for basic admin charts. It is not Kentico's plan for reporting in Xperience by Kentico. Native reporting, reporting through AIRA, or data exposed to external agents may come later in the product.

Suggested first line of the README:

> This project is a Kentico Labs example that shows basic charts about your Xperience by Kentico data in the administration. It is not Kentico's plan for reporting in Xperience by Kentico.

Design goals:

- Keep the reports mostly static, with a small set of shared filters.
- Use data that XbK already stores with timestamps.
- No extension points for other libraries or custom reports.

## Reports and data

### Recommended for the first release

| Report                              | Data source                         | Notes                                                                          |
| ----------------------------------- | ----------------------------------- | ------------------------------------------------------------------------------ |
| Activity counts by type over time   | Contact activities                  | Page visits, form submissions, email opens and clicks, custom activities       |
| Top pages by visits                 | Contact activities (URL field)      | For a selected date range                                                      |
| Top referrers                       | Contact activities (referrer field) | Only where a referrer was recorded                                             |
| New contacts over time              | Contacts (created date)             | Include the ratio of identified contacts (with email) to anonymous contacts    |
| Form submissions per form over time | Form data tables                    | One query per form; ranked list of most and least used forms                   |
| Email summary across all emails     | Email statistics                    | Sends, open rate, click rate, bounces, unsubscribes; top and bottom performers. Spec: `.agent-resources/REPORT-12-EMAIL-SUMMARY.md` |

### Later candidates

**Contacts and contact groups**

- Contact group sizes over time. Membership is current state only, so this needs a scheduled task that saves daily counts to a custom table.

**Emails**

- Recipient list growth and unsubscribes over time. Spec: `.agent-resources/REPORT-11-RECIPIENT-LISTS.md`.

**Content** (useful for administrators and content leads)

- Content items by content type, by workflow step, and by language.
- Items in a workflow step for a long time ("action needed").
- Content not modified in 6 or 12 months.
- Items missing translations for a given language.
- Reusable items with no usages. _Uncertain: not yet checked whether usage tracking data is easy to query._

**Content management** (daily work of content marketers; researched 2026-10-06 against Kentico docs and local DB)

Top picks: publishing calendar, forgotten edits, stale but popular, tag coverage, locked content.

_Needs attention (current state)_

- Publishing calendar: scheduled publish/unpublish in next 7/30 days per day; "overdue" items (scheduled time passed, not published). Source: `ContentItemLanguageMetadataScheduledPublishWhen` / `...ScheduledUnpublishWhen`. Spec: `.agent-resources/REPORT-13-PUBLISHING-CALENDAR.md` (includes ending soon).
- Ending soon: scheduled unpublish in next N days (campaign content).
- Locked content: locks per user, lock age, highlight old locks (helps admins decide on override). Source: `ContentItemLanguageMetadataLockedByUserID` / `LockedWhen` (content locking, 31.6). Spec: `.agent-resources/REPORT-14-CONTENT-LOCKS.md`.
- Forgotten edits: published items with a newer draft unchanged > N days (live differs from edit); drafts and workflow items by last modifier (`ModifiedByUserID`). Verified 2026-10-08: published `CommonData` row with `IsLatest` = 0 = newer draft of published content. Spec (tile in Content inventory, no per-user part): `.agent-resources/REPORT-05-CONTENT-INVENTORY.md` Round 4.
- Published content linking unpublished items: linked drafts show only in preview, so live content is missing parts. Source: `CMS_ContentItemReference` + status.

_Publishing speed (date range)_

- Published over time: first publishes vs updates per period, by type / kind / channel. Source: `ContentItemCommonDataFirstPublishedWhen` / `LastPublishedWhen`. Full history needs `CMS_ContentItemVersion` (only with content versioning enabled; 0 rows locally). Spec (with created over time, time to publish, updates from version history): `.agent-resources/REPORT-17-PUBLISHING-ACTIVITY.md`.
- Created over time by content type (`CreatedWhen`).
- Time to publish: median days from created to first published, per content type.
- Editor contributions: created / modified / published per admin user. Own permission (per-user data). Spec: `.agent-resources/REPORT-19-EDITOR-CONTRIBUTIONS.md`.

_Quality and governance_

- Tag coverage: untagged share per taxonomy field, top tags, unused tags. Smart folders often filter by tags. Source: `CMS_ContentItemTag` (155 rows locally), `CMS_Tag`, `CMS_Taxonomy`. Spec: `.agent-resources/REPORT-20-TAG-USAGE.md`.
- Outdated translations: language variants last modified before the default language variant (extends language coverage). Optional: AIRA translation task status (`CMS_TranslationTask`, 0 rows locally). Spec (with missing; no AIRA tasks): `.agent-resources/REPORT-18-TRANSLATION-STATUS.md`.
- SEO fields / image descriptions missing. _Uncertain: fields are project-specific (e.g. DancingGoat `SEOFields*`), would need configuration, conflicts with "no extension points"._

_Content performance (content + activities)_

- Stale but popular: high-traffic pages not modified in 12 months (what to refresh first). Spec (with pages with no visits): `.agent-resources/REPORT-15-PAGE-FRESHNESS.md`.
- Pages with no visits: published pages with 0 page visits in 90 days. Needs data retention note.
- Performance by content type or tag: page visits rolled up via `ActivityWebPageItemGUID`.
- Campaign sources (UTM): landing page activities by `ActivityUTMSource` / `ActivityUTMContent` (filled only by the optional Dancing Goat UTM capture sample). Phase 1 in the web page Stats tab (renamed "Stats (Labs)"), phase 2 global "Campaign sources" report (Contacts section): top landing pages per source and content, sources, trend. Spec: `.agent-resources/REPORT-21-CAMPAIGN-SOURCES.md`.
- Most-used reusable items: usage count and where used (pages, emails); risky to edit. Inverse of unused reusable items. Source: `CMS_ContentItemReference`. Spec: `.agent-resources/REPORT-16-REUSABLE-USAGE.md`.
- Personalization and widget/template usage: pages with personalized widgets, variant counts, widget and template counts. _Uncertain: parses `ContentItemCommonDataVisualBuilderWidgets` JSON; cost on large sites._

_Housekeeping_

- Recycle bin: deleted items per period by user and type; items permanently deleted soon (retention default 30 days). Source: `CMS_RecycleBinContentItem`.
- Redirects and former URLs over time; redirects from unpublished/deleted pages. Source: `CMS_WebPageFormerUrlPath`.
- Content sync health: synchronizations by status, failures. Source: `CMS_Synchronization`. Completed/failed are cleaned daily, so little history.
- Workspaces and folders: items per workspace and content folder, items in no folder. _Verify item → workspace column._

Notes: version history does not record workflow step moves (only publish, unpublish, scheduling), so "days in step" stays a `ModifiedWhen` proxy. Versions, recycle bin, former URLs and translation tasks have 0 rows locally; seed before visual checks.

**Consents**

- Agreements and revocations per consent over time.

**Other**

- Digital commerce orders and revenue. _Uncertain: not yet checked which commerce tables are stable enough to depend on._
- Customer growth over time, broken down by billing or shipping country and state; rank top customers by revenue, order count, and item quantity purchased.
- Member registrations over time.
- Admin user sign-in activity.

**Event log** (useful for administrators and developers)

Data source: `CMS_EventLog` (`EventType`, `EventTime`, `Source`, `EventCode`, `UserID`, `UserName`, `EventDescription`, `EventUrl`, `EventMachineName`).

- Event types: Information (`I`), Warning (`W`), Error (`E`).
- Events can come from any admin UI user (`UserID` / `UserName`) or from the system (no user).
- `Source` and `EventCode` have a small, fairly fixed set of values, so they group well.

Report ideas:

- Events by type over time (stacked, by day, week, or month), with a filter for event type.
- Totals per type compared with the previous period (reuse `StatsComparison`), for example "Errors +40% vs previous 30 days".
- Top sources and top event codes (ranked), each with a change vs the previous period.
- Top users by event count, with system events shown as their own row.
- Optional: link to the native Event log application (via `IStatsAdminLinks`).

Notes:

- The event log is trimmed by a size limit setting (verify the setting name), so old events disappear. Add a data retention note.
- The table can be large on busy sites. Aggregate in SQL and use `TOP N`.
- Local DancingGoat DB (2026-09-29): 582 events (532 I, 44 W, 6 E), 28 sources, 24 codes, 2 users, all from one day. Trends over time need seeded data.

### Data limitations

- Contact and activity cleanup tasks delete old data. Long-range trends may show drops that are really deletions. Add a note on the dashboard about data retention.
- The activity table can be large. Aggregate in SQL and cache results, or precompute with a scheduled task.
- Data that is current state only (for example contact group membership) needs periodic snapshots to show trends.

## Filter options

| Filter     | Details                                                                              |
| ---------- | ------------------------------------------------------------------------------------ |
| Date range | Presets for 7, 30, and 90 days, plus a custom range. Applies to the whole dashboard. |
| Channel    | Website channel or email channel, where the data has a channel ID.                   |
| Grouping   | Day, week, or month for trend charts.                                                |

## Library features

| Feature                       | Details                                                                                              |
| ----------------------------- | ---------------------------------------------------------------------------------------------------- |
| Chart and table toggle        | Each tile can switch between the chart and a table of the exact numbers.                             |
| CSV export                    | Per report. Guarded by one app-wide Export permission (see Implementation notes).                    |
| Links to existing admin pages | For example, a form links to its submissions and an email links to its statistics.                   |
| Permission per report         | Uses the standard XbK role and UI permission model, so marketers and admins can see different tiles. |
| Show/hide tiles per user      | Stored in a small custom table. Moderate effort; optional for the first release.                     |

## Implementation notes

- Build as a custom admin application with a custom React page template.
- Use amCharts for charts. It ships with Xperience by Kentico and is available to admin React components, so there is no extra bundle size or licensing question.
- Load data through page commands.
- C# types are `internal` by default. Make a type `public` only when a public signature, the admin page/command API, or client serialization needs it (e.g. page classes, result types, service interfaces). Repositories, SQL builders, report builders, data/row records and helpers stay `internal`; tests see them through `InternalsVisibleTo`.
- Reference: the Community Portal reporting admin UI (`CommunityStatsLayoutTemplate.tsx` in the `Kentico/community-portal` repo), linked from the Admin Design Components README.
- Stats should have their own application permissions to help administrators limit who has access to the information
- **Permission per report page.** Today `StatsApplicationPage` declares only `SystemPermissions.VIEW`, and every report page checks VIEW. Change to one custom permission per report page:
  - Declare each permission on the application page with `[UIPermission("<name>", "<display name>")]`, for example `SimpleStats.ActivityCounts` / "Activity counts". These show up in **Role management** for the Simple Stats (Labs) application.
  - Keep `[UIPermission(SystemPermissions.VIEW)]` for access to the application itself.
  - Restrict each report page with `[UIEvaluatePermission("<name>")]`. It must be one of the permissions declared on the application, or it cannot be assigned to roles.
  - Set the same permission on each page's `LOAD` command (`[PageCommand(Permission = "<name>")]`) so the data can't be read without it.
  - Keep permission names as constants in one class (e.g. `StatsPermissions`), with a stable `Kentico.Xperience.Labs.SimpleStats.` prefix.
  - Verify: roles without a report permission get 403 on that page. Reports are grouped in section pages (Contacts, Content, Commerce, System; `.agent-resources/NAV-SECTIONS.md`). Decompiled 31.9 code shows the product does not filter nav or default routes by permission, so `StatsNavigation` hides denied reports and empty sections and lands on the first allowed report, or on the hidden "No reports available" page. Verified in DancingGoat 2026-09-30 with test users: View only; View + New contacts; View + New contacts + Customers. Opening a denied report by URL shows the product's "Access Denied" page. Export permission hides "Export CSV" in all reports (verified same day).
  - Update `docs/Usage-Guide.md` with the permission list and how to assign them in Role management.
  - **Export permission.** One app-wide permission guards "Export CSV" in every report, separate from the report permissions:
    - `StatsPermissions.EXPORT` = `SimpleStats.Export`, "Export", declared on `StatsApplicationPage` with `[UIPermission]`.
    - Each report page checks it server side (`Page.UIPermissionEvaluator` / `IUIPermissionEvaluator.Evaluate(StatsPermissions.EXPORT)`) and sends a `CanExport` flag in its client properties. Put this in one shared place (base page class or helper), not per report.
    - Client: `StatsTile` hides "Export CSV" when `CanExport` is false (shared, e.g. context or prop from each template). No disabled button; just hidden.
    - Caveat: CSV is built client side from data the user can already see, so this is a UI guard, not data protection. Say so in `docs/Usage-Guide.md`. Server-side export (a page command returning CSV with `Permission = EXPORT`) only if we later need real protection.
    - **Export events** (best effort, user decision 2026-10-01): every export raises `AfterExportStatsEvent` through a shared `LOG_EXPORT` command, for audit handlers. The product `AfterExportListingEvent` cannot be raised by libraries. Spec: `.agent-resources/EXPORT-EVENTS.md`.
    - Tests: flag true/false per page; permission declared on the app. Usage guide permission table gets the row.
  - Reference: [UI page permission checks](https://docs.kentico.com/documentation/developers-and-admins/customization/extend-the-administration-interface/ui-pages/ui-page-permission-checks) (define with `UIPermission` on the `ApplicationPage`, evaluate with `UIEvaluatePermission`, `PageCommand.Permission`, `IUIPermissionEvaluator` for client-side flags).

## Out of scope

- Extension points for other integrations or custom reports.
- Ad hoc report builder or query designer.
- Arbitrary SQL input (also a security risk).
- Free drag-and-drop layouts.
- Scheduled report emails.
- Real-time updates.
