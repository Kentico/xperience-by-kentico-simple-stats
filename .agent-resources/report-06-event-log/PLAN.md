# Plan: Report 06 – Event log

## Goal

Sixth report. Audience: administrators and developers (simple-stats PLAN "Later candidates → Event log"). Time-range report like 01–04. Read `.agent-resources/simple-stats/PLAN.md` (Event log section + permission notes) and `.agent-resources/report-01-*` to `report-05-*` first. Build on existing code; do not duplicate.

## Research

### Local data (checked 2026-09-29)

DB: `mssql2022` docker, DB `xperience-by-kentico-simple-stats`, creds in `examples/DancingGoat/appsettings.json`. From Git Bash: `MSYS_NO_PATHCONV=1 docker exec mssql2022 /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P ... -d xperience-by-kentico-simple-stats -W -Q "..."`.

- `CMS_EventLog`: 918 rows, all on 2026-09-29 (I 832, W 74, E 12). ~28 sources, ~24 codes, 2 users. Top source `CMS.ContentEngine.ContentItemAssetVariantCacheCleaner`.
- Setting `CMSLogSize` = 10000 (event log size limit; verify meaning = max rows kept, and whether 0 = unlimited, via kentico-docs MCP). Use it in the retention note (read the value, don't hardcode).
- Trends need seeded data. Seed **local dev DB only**: insert copies of existing rows with `EventTime` spread over the last ~120 days (uneven, some error spikes, a few days with no events; keep some system events with no user). Stay under `CMSLogSize` or the product may trim. Check `NOT NULL` columns first. Save as `.agent-resources/report-06-event-log/seed-event-log.sql`. Never put seeding in `src/`.

## Slices

1. Event log (`implementation-spec-1.md`)
2. Round 2 (user feedback) (`implementation-spec-2.md`)
3. Round 3 (user feedback) (`implementation-spec-3.md`)
4. Round 4 (user feedback) (`implementation-spec-4.md`)

## Slice log

- 2026-09-30 – Slices 1–4: report, per-report permissions, top users change, stack order, source toggle, `WebFarmMonitor`, chart re-render fix (`94ff144`; spec only committed there, so all rounds are in it).
