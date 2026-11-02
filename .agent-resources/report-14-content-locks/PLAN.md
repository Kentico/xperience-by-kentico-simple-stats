# Plan: Report 14 – Content locks

## Goal

Audience: content leads and administrators. Answers "who is holding which items, and for how long?" so a stuck lock can be resolved before it blocks publishing. simple-stats PLAN "Later candidates → Content management": locked content. Current-state report in the **Content** section. Read `.agent-resources/simple-stats/PLAN.md`, `.agent-resources/report-05-content-inventory/` (snapshot infra, kind/channel filter, item links, `AgedItemTable`) and `.agent-resources/nav-sections/` first. Build on existing code; do not duplicate.

## Research

### Data (checked in local DB 2026-10-06)

- `CMS_ContentItemLanguageMetadata.ContentItemLanguageMetadataLockedByUserID` and `...LockedWhen`. One lock per language variant.
- Setting key `CMSEnableContentLocking` (`CMS_SettingsKey`; `False` locally). Read it with the settings API (`ISettingsService` / `SettingsKeyInfoProvider`, whichever the library already uses), not SQL.
- User names from `CMS_User` (same way as the Event log report's top users).
- Item, content type, kind and channel filter and item admin links: as `ContentInventorySql`.

Product behavior (docs: Content locking, Work with content locking; feature since 31.6):

- Pages, content items and headless items are locked while edited; only the lock holder can edit.
- Released automatically on publish, schedule, revert, move to another workflow step, sending an email, moving to the recycle bin, and when the holder's account is disabled. Released manually with **Release lock** (saves changes first).
- Admins and roles with **Override content lock** (per application and workspace) can **Unlock**, which discards the holder's unsaved changes.
- A lock blocks editing, not viewing. Locked items cannot be published through cascade publishing.

Local data: locking disabled, 0 locks. See Local data in `implementation-spec-1.md`.

## Design rules

### Definitions

- **Locked variant**: `LockedByUserID` is set.
- **Lock age**: now − `LockedWhen`.
- **Old lock**: lock age over 3 days (server constant). Highlighted.

## Slices

1. Content locks (`implementation-spec-1.md`)

## Slice log

- 2026-10-07 – Slice 1: Content locks report (`a8f41b2`; spec `253afb9`).
