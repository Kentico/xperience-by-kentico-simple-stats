# Plan: Report 03 – New contacts over time

## Goal

Third report. Adds shared **time series** (server) and **share/donut chart** (client) infra that later reports reuse: form submissions over time, email summary (open/click ratio), consent agreements vs revocations. Read `.agent-resources/simple-stats/PLAN.md`, `.agent-resources/report-01-activity-counts/`, `.agent-resources/report-02-top-pages/` first. Build on report 01/02 code and patterns; do not duplicate them.

## Research

### Why this report next

- DancingGoat "Sample data generator" `ContactsGenerator` inserts contacts with `ContactEmail` and `ContactCreated` spread over the last 90 days. Site visits create anonymous contacts (no email). → visible result right away, both segments.
- One SQL aggregate over `OM_Contact`: group by day of `ContactCreated`, split identified (has email) vs anonymous. Low complexity.
- Uses range + grouping filters (no channel: contacts have no channel ID).
- Remaining first-release reports ranked: top referrers (generator writes no referrers → empty chart), form submissions (dynamic per-form tables, more complex), email summary (needs email stats data, more complex). This one is the best ratio of visible result to effort.

## Slices

1. New contacts over time (`implementation-spec-1.md`)

## Slice log

- 2026-09-29 – Slice 1: report + shared time series and donut infra (`65a3e4a`).
- 2026-11-02 – Previous-period comparison added for the Overview (`2b45409`, report 23).
