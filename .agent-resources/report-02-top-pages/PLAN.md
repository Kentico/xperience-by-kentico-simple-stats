# Plan: Report 02 – Top pages by visits

## Goal

Second report. Adds the shared **ranked list** infra (server + client) that later reports reuse: top referrers, forms ranked by submissions, email top/bottom performers. Read `.agent-resources/simple-stats/PLAN.md` and `.agent-resources/report-01-activity-counts/` first. Build on report 01 code and patterns; do not duplicate them.

## Research

### Why this report next

- DancingGoat sample data generator already writes `pagevisit` activities with `ActivityURL` (Home, Coffee Samples, Coffee Beverages Explained) → visible result right away.
- One SQL aggregate over `OM_Activity`: `ActivityType = 'pagevisit'`, date range, optional `ActivityChannelID`, group by URL, top N. Low complexity.
- New shared infra that 3+ later reports need: ranked bar chart, ranked table, ranked result model, filter bar without grouping.
- Top referrers is NOT in scope (generator writes no referrers → empty). It should become a ~1 hour follow-up on top of this infra. Design the ranked pieces so that is true.

## Slices

1. Top pages by visits (`implementation-spec-1.md`)

## Slice log

- 2026-09-29 – Slice 1: report + shared ranked list infra (`63cdcd2`).
