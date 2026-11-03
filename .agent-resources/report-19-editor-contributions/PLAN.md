# Plan: Report 19 – Editor contributions

## Goal

Audience: content leads and administrators. Answers "who creates content and who last worked on it?" simple-stats PLAN "Later candidates → Content management → Publishing speed": editor contributions, own permission (per-user data). Time-range report in the **Content** section. Read `.agent-resources/simple-stats/PLAN.md`, `.agent-resources/report-06-event-log/` (top users, user links), `.agent-resources/report-17-publishing-activity/` (variant CTE, filters) first. Build on existing code; do not duplicate.

## Research

### Data (checked in local DB 2026-10-07)

- `CMS_ContentItemLanguageMetadata`: `...CreatedByUserID` + `CreatedWhen`, `...ModifiedByUserID` + `ModifiedWhen`. Only the **latest** modifier is stored.
- Locally 3 users: `administrator` (53), `kentico-system-service` (118, system user for imports/automation), `test-user@kentico.local` (120).
- "Published by" is not stored without content versioning (`CMS_ContentItemVersion`, 0 rows). **Out of scope.**
- User names and user admin links: as the Event log report's top users. Deleted users → one "Unknown user" row (as Content locks).
- Type, kind, channel filter, folders excluded: as `ContentInventorySql`.

## Design rules

### Definitions

- **Created**: variants created by the user in the range.
- **Last modified**: variants whose latest change (`ModifiedWhen`) is in the range and was made by the user. Earlier edits by other users are overwritten, so this undercounts. Hint says so.
- **System user**: users flagged as system/service accounts (check `CMS_User` columns / `UserInfo` API, e.g. `UserIsSystem` or similar; verify). Shown as their own row, not hidden.

## Slices

1. Editor contributions (`implementation-spec-1.md`)

## Slice log

- 2026-10-08 – Slice 1: Editor contributions report (`4947515`; spec `4d1d850`).
