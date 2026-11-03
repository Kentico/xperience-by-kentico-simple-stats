# Plan: Report 01 – Activity counts by type over time

## Goal

First report. Also builds the shared server + client infrastructure that later reports reuse. Read `.agent-resources/simple-stats/PLAN.md` first.

## Research

### Why this report first

- DancingGoat has a sample data generator (`examples/DancingGoat/Helpers/Generators/DigitalMarketing/ActivitiesGenerator.cs`, admin app "Sample data generator") → data to see right away.
- One SQL aggregate over `OM_Activity` grouped by type + period. Low complexity.
- Uses all 3 shared filters: date range, grouping (day/week/month), channel (`ActivityChannelID`).
- Needs every shared piece: app + permissions, page template, page command, filter bar, tile with chart/table toggle, CSV export, KPI row.

## Slices

1. Activity counts by type over time (`implementation-spec-1.md`)

## Slice log

- 2026-09-29 – Slice 1: report + shared stats infra (app, filter, cache, filter bar, tile, chart, CSV) (`9c6623b`).
- 2026-11-02 – Previous-period comparison added for the Overview (`2b45409`, report 23).
