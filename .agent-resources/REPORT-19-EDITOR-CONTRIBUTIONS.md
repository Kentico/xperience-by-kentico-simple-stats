# Report 19: Editor contributions

Audience: content leads and administrators. Answers "who creates content and who last worked on it?" PLAN "Later candidates → Content management → Publishing speed": editor contributions, own permission (per-user data). Time-range report in the **Content** section. Read `PLAN.md`, `REPORT-06-EVENT-LOG.md` (top users, user links), `REPORT-17-PUBLISHING-ACTIVITY.md` (variant CTE, filters) first. Build on existing code; do not duplicate.

## Data (checked in local DB 2026-10-07)

- `CMS_ContentItemLanguageMetadata`: `...CreatedByUserID` + `CreatedWhen`, `...ModifiedByUserID` + `ModifiedWhen`. Only the **latest** modifier is stored.
- Locally 3 users: `administrator` (53), `kentico-system-service` (118, system user for imports/automation), `test-user@kentico.local` (120).
- "Published by" is not stored without content versioning (`CMS_ContentItemVersion`, 0 rows). **Out of scope.**
- User names and user admin links: as the Event log report's top users. Deleted users → one "Unknown user" row (as Content locks).
- Type, kind, channel filter, folders excluded: as `ContentInventorySql`.

## Definitions

- **Created**: variants created by the user in the range.
- **Last modified**: variants whose latest change (`ModifiedWhen`) is in the range and was made by the user. Earlier edits by other users are overwritten, so this undercounts. Hint says so.
- **System user**: users flagged as system/service accounts (check `CMS_User` columns / `UserInfo` API, e.g. `UserIsSystem` or similar; verify). Shown as their own row, not hidden.

## Scope

### Server

1. **Page** `EditorContributionsPage`, slug `editor-contributions`, name "Editor contributions", parent `StatsContentSection`, order 570, icon from `Icons` (pick an existing member), template `@kentico/xperience-admin-labs-simple-stats/EditorContributions`, derived from `StatsReportPage<>`.
   - Permission `StatsPermissions.EDITOR_CONTRIBUTIONS` = `SimpleStats.EditorContributions`, "Editor contributions".
2. **Filter**: `StatsFilter` range + grouping + kind + channel (same as report 17).
3. **Queries** (one round trip, parameterized):
   - Per user: created, last modified, distinct content types touched. Ranked by created + last modified, `TOP (@Limit)`.
   - Created per period, top 5 users as series + "Other" (reuse stacked series helpers from Activity counts).
   - Active editors: distinct users with any created or last modified in the range, and in the previous period (`StatsComparison`).
4. **Result** (suggested): `EditorContributionsResult { From, To, Grouping, Kind, ChannelId, ActiveEditors, Created, LastModified, Comparison, ByUser (ranked, with user link), Series, ChannelOptions, UpdatedAt }`.
5. **Service**: interface + `StatsCache`. Tests: deleted user; system user row; created and modified by different users; range edges; kind/channel filter.

### Client

- Template `editor-contributions/EditorContributionsTemplate.tsx`, exported in `entry.tsx` with the export-permission wrapper.
- Filter bar: range, grouping, kind, channel, refresh, updated at.
- KPI row: active editors, created, last modified (each with change vs previous period).
- Tile "By editor" (wide): `RankedBarChart` / `RankedTable` (user, created, last modified, content types), user links to **Users**. CSV.
- Tile "Created over time": stacked column chart by top users / `TimeSeriesTable`. CSV.
- Hint: only the last change per item is stored, so earlier edits by others are not counted; publishing is not tracked per user.
- Export names: `editor-contributions-users`, `editor-contributions-series`.

## Local data

Few users. For a useful chart, re-assign `CreatedByUserID` / `ModifiedByUserID` on ~30 variants to 2–3 existing test users in the **local dev DB only**, saved as `.agent-resources/seed-editor-contributions.sql` (re-runnable; only existing user IDs). Never put seeding in `src/`.

## Done when

- Same build, test, format and visual check rules as `REPORT-12-EMAIL-SUMMARY.md`.
- Created count for one user and range matches a SQL count by hand.
- Usage guide notes the per-user data and that the permission should go to leads/admins only.
- `docs/Usage-Guide.md`: report section, permission row, navigation table, export names.
- Nothing DancingGoat-specific in `src/`. Do not commit. Report back concise.
