# Plan: Report 05 – Content inventory

## Goal

Fifth report. First **current-state (snapshot)** report: no date range. Audience: administrators and content leads (simple-stats PLAN "Later candidates → Content"). Adds shared **snapshot** infra (server + client) that later current-state reports reuse (contact group sizes, reusable items with no usages, stale content, recipient lists). Read `.agent-resources/simple-stats/PLAN.md` and `.agent-resources/report-01-*` to `report-04-*` first. Build on existing code; do not duplicate.

## Research

### Why this report next

Checked local DancingGoat DB (`mssql2022` docker, DB `xperience-by-kentico-simple-stats`, creds in `examples/DancingGoat/appsettings.json`; from Git Bash prefix `MSYS_NO_PATHCONV=1 docker exec mssql2022 /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P ... -d xperience-by-kentico-simple-stats -W -Q "..."`):

- Remaining first-release reports need seeding before anything shows: `EmailLibrary_EmailStatistics` = 0 rows (email summary), `ActivityURLReferrer` = 3 rows (top referrers).
- Content has real data now, no seeding: 185 `CMS_ContentItem` rows over 29 content types (`CMS_Class.ClassContentTypeType`: Reusable 111, Website 74 incl. Email 7), 195 `CMS_ContentItemLanguageMetadata` rows, languages `en` (185) and `es` (10) → missing translations are visible right away. Statuses: 182 published, 2 draft, 1 in a workflow step (1 workflow exists). 2 channels (website + email).
- One aggregate query per dimension. Low complexity.

## Slices

1. Content inventory (`implementation-spec-1.md`)
2. Round 2 (user feedback after first build) (`implementation-spec-2.md`)
3. Round 3 (user feedback) (`implementation-spec-3.md`)
4. Round 4: Forgotten edits (2026-10-08) (`implementation-spec-4.md`)

## Slice log

- 2026-09-29 – Slices 1–3: report + shared snapshot infra; kind/channel filter, stale content, unused reusable items, action needed, content item links, layout (`5b2e6cb`).
- 2026-10-08 – Slice 4: Forgotten edits tile (`6444a18`).
