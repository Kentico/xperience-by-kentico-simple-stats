# Plan: Report 09 – Member registrations

## Goal

Ninth report. Audience: marketers, site owners with member areas. simple-stats PLAN "Later candidates → Other → Member registrations over time". Time-range report. Read `.agent-resources/simple-stats/PLAN.md`, `.agent-resources/report-08-customers/` (closest: growth combo chart, cumulative totals, ratio KPIs) and `.agent-resources/nav-sections/` first; skim 01–07 as needed. Build on existing code; do not duplicate. Keep complexity low.

## Research

### Local data (checked 2026-09-30)

DB: `mssql2022` docker, DB `xperience-by-kentico-simple-stats`, creds in `examples/DancingGoat/appsettings.json`. Run SQL from Git Bash: `MSYS_NO_PATHCONV=1 docker exec -i mssql2022 /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P '<password>' -d xperience-by-kentico-simple-stats -W < file.sql`. Package 31.9.0.

- `CMS_Member` (147 rows, seeded by `.agent-resources/report-09-members/seed-members.sql`, re-runnable): `MemberID`, `MemberEmail` (unique, NULL), `MemberName` (unique, NULL), `MemberEnabled` bit, `MemberCreated` datetime2 NOT NULL, `MemberGuid`, `MemberIsExternal` bit, `MemberPassword`, `MemberSecurityStamp`. Created Jul 5 – Sep 29; 12 disabled, 17 external.
- `CMS_MemberRole` / `CMS_MemberRoleMember`: 0 rows (see Seeding).
- `OM_Activity` type `memberregistration` (171 rows, 98 seeded): contact-level registration events. Not the source of truth for member counts (activities can be deleted by cleanup / contact deletion); members table is.
- Every seeded member is a contact with the same email (user decision: membership sites need tracking). **Profiles** (`OM_Profile`, `OM_ProfileReference`) are a preview feature, off in DancingGoat → do not use them.
- Verify public Info classes (`MemberInfo`, `MemberRoleInfo`, `MemberRoleMemberInfo`, probably `CMS.Membership`) and reference them in SQL doc comments like earlier reports.
- Native app: Members, `Kentico.Xperience.Application.Members`, slug `members`, icon `xp-user-frame` (decompiled). Link via `IStatsAdminLinks`; per-member link only if the member edit page type/route is public and simple (verify), else skip + say why.

## Slices

1. Member registrations (`implementation-spec-1.md`)

## Slice log

- 2026-09-30 – Slice 1: Member registrations report (`604635b`).
