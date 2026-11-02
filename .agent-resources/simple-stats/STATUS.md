# Status

## Current slice

Reports 01–22, nav sections and export events done and on `main`. Report 23 Phase 1 (Overview) done on branch `feat/overview-dashboard` (`2b45409`), not on `main`. Report 24 (GA4) not started.

## Components

| Component | State |
| --- | --- |
| Per-report permissions + Export permission | done (`94ff144`, `fca0826`); verified with test users 2026-09-30 |
| `.agent-resources/nav-sections/` | done |
| `.agent-resources/export-events/` | done |
| `.agent-resources/report-01-activity-counts/` … `report-22-contact-stats/` | done (see each `STATUS.md`) |
| Web page Stats (Labs) tab | done (`b7b69cf`; campaign sources part in report 21) |
| `.agent-resources/report-23-overview/` | Phase 1 done, not on `main` |
| `.agent-resources/report-24-google-analytics/` | not started |
| Top referrers (first-release table) | not started |
| Other "Later candidates" without a feature folder | not started |
| Show/hide tiles per user | not started (optional) |

## Open checks

- Report 22: tag interests language fallback (`.agent-resources/report-22-contact-stats/STATUS.md`).
- e2e tests (`tests/e2e/`) are uncommitted on this branch.

## Pending decisions

- Overview placement: page in Simple Stats or own application (`.agent-resources/report-23-overview/STATUS.md`).
- Next after Overview Phase 1: GA4 (report 24) or Phase 3 data sources; user decides.
- GA4: client library or REST (`.agent-resources/report-24-google-analytics/STATUS.md`).
