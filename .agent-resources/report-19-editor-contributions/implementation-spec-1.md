# Slice 1 – Editor contributions

Goal, research, definitions and decisions: `PLAN.md`.

## Scope

### Server

1. **Page** `EditorContributionsPage`, slug `editor-contributions`, name "Editor contributions", parent `StatsContentSection`, order 570, icon from `Icons` (pick an existing member), template `@kentico/xperience-labs-simple-stats-admin/EditorContributions`, derived from `StatsReportPage<>`.
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

Few users. For a useful chart, re-assign `CreatedByUserID` / `ModifiedByUserID` on ~30 variants to 2–3 existing test users in the **local dev DB only**, saved as `.agent-resources/report-19-editor-contributions/seed-editor-contributions.sql` (re-runnable; only existing user IDs). Never put seeding in `src/`.

## Done when

- Same build, test, format and visual check rules as `.agent-resources/report-12-email-summary/implementation-spec-1.md`.
- Created count for one user and range matches a SQL count by hand.
- Usage guide notes the per-user data and that the permission should go to leads/admins only.
- `docs/Usage-Guide.md`: report section, permission row, navigation table, export names.
- Nothing DancingGoat-specific in `src/`. Do not commit. Report back concise.
