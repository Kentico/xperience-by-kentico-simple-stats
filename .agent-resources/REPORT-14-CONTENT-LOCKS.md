# Report 14: Content locks

Audience: content leads and administrators. Answers "who is holding which items, and for how long?" so a stuck lock can be resolved before it blocks publishing. PLAN "Later candidates → Content management": locked content. Current-state report in the **Content** section. Read `PLAN.md`, `REPORT-05-CONTENT-INVENTORY.md` (snapshot infra, kind/channel filter, item links, `AgedItemTable`) and `NAV-SECTIONS.md` first. Build on existing code; do not duplicate.

## Data (checked in local DB 2026-10-06)

- `CMS_ContentItemLanguageMetadata.ContentItemLanguageMetadataLockedByUserID` and `...LockedWhen`. One lock per language variant.
- Setting key `CMSEnableContentLocking` (`CMS_SettingsKey`; `False` locally). Read it with the settings API (`ISettingsService` / `SettingsKeyInfoProvider`, whichever the library already uses), not SQL.
- User names from `CMS_User` (same way as the Event log report's top users).
- Item, content type, kind and channel filter and item admin links: as `ContentInventorySql`.

Product behavior (docs: Content locking, Work with content locking; feature since 31.6):

- Pages, content items and headless items are locked while edited; only the lock holder can edit.
- Released automatically on publish, schedule, revert, move to another workflow step, sending an email, moving to the recycle bin, and when the holder's account is disabled. Released manually with **Release lock** (saves changes first).
- Admins and roles with **Override content lock** (per application and workspace) can **Unlock**, which discards the holder's unsaved changes.
- A lock blocks editing, not viewing. Locked items cannot be published through cascade publishing.

Local data: locking disabled, 0 locks. See Local data below.

## Definitions

- **Locked variant**: `LockedByUserID` is set.
- **Lock age**: now − `LockedWhen`.
- **Old lock**: lock age over 3 days (server constant). Highlighted.

## Scope

### Server

1. **Page** `ContentLocksPage`, slug `content-locks`, name "Content locks", parent `StatsContentSection`, order 520, a lock icon from `Icons` (pick an existing member), template `@kentico/xperience-admin-labs-simple-stats/ContentLocks`, derived from `StatsReportPage<>`.
   - Permission `StatsPermissions.CONTENT_LOCKS` = `SimpleStats.ContentLocks`, "Content locks".
2. **Filter**: snapshot request, kind + channel as Content inventory (reuse).
3. **Queries** (one round trip, parameterized):
   - Locks per user: user ID, user display name, lock count, oldest `LockedWhen`. Users that no longer exist count as one "Unknown user" row.
   - Locked variants, oldest lock first, `TOP (@Limit)`: name, content type, language, channel, user, `LockedWhen`, `ModifiedWhen`, link columns. Window count of old locks before `TOP` (as `OverdueCount` in `ContentInventorySql`).
4. **Result** (suggested): `ContentLocksResult { LockingEnabled, Kind, ChannelId, LockedCount, UserCount, OldLockCount, OldestLockedWhen, ByUser (ranked, with user admin link), Items (aged rows), ChannelOptions, UpdatedAt }`.
5. **Service**: interface + `StatsCache`. Tests: locking disabled with and without existing locks (show the locks, plus the hint); no locks; deleted user; old-lock threshold; kind/channel filter.

### Client

- Template `content-locks/ContentLocksTemplate.tsx`, exported in `entry.tsx` with the export-permission wrapper.
- Filter bar: `SnapshotFilterBar` with kind + channel, refresh, updated at.
- Hint when locking is disabled: "Content locking is not enabled (Settings → Content)." Still show existing rows.
- KPI row: locked items, users holding locks, old locks (highlighted when > 0), oldest lock (age).
- Tile "Locked items": `AgedItemTable` / bar chart by lock age (bars over 3 days highlighted), item link, user. CSV.
- Tile "Locks by user": `RankedBarChart` / `RankedTable`, user name links to the **Users** application (like Event log top users). CSV.
- Hint (short): unlocking discards the holder's unsaved changes; ask them to **Release lock** first. Disabling a user releases their locks.
- Export names: `content-locks-items`, `content-locks-users`.

## Local data

No seed SQL. **Manual step (user)**: enable **Settings → Content → Content locking** (find the exact category in the UI) in DancingGoat. As two admin users, start editing ~6 items (pages, reusable items, one headless item if a headless channel exists) and leave them locked. For the old-lock highlight, back-date `LockedWhen` on 2 rows in the **local dev DB only**, saved as `.agent-resources/seed-content-locks.sql` (re-runnable; touches only `LockedWhen` of currently locked rows).

## Done when

- Same build, test, format and visual check rules as `REPORT-12-EMAIL-SUMMARY.md`.
- The report matches the lock banners shown on the items. After **Release lock** + Refresh, the row is gone.
- `docs/Usage-Guide.md`: report section, permission row, navigation table, export names.
- Nothing DancingGoat-specific in `src/`. Do not commit. Report back concise.
