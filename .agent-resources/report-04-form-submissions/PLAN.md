# Plan: Report 04 – Form submissions per form

## Goal

Fourth report. Adds shared infra that later reports reuse: **dynamic series** time series (series set comes from data, not fixed), **ranked list with zero items** (least used), **links to native admin pages** (email summary will link emails to their stats the same way), and a **safe multi-table aggregate** pattern. Read `.agent-resources/simple-stats/PLAN.md` and `.agent-resources/report-01-*`, `report-02-*`, `report-03-*` first. Build on existing code; do not duplicate.

## Research

### Why this report next

Checked the local DancingGoat DB (`mssql2022` docker container, DB `xperience-by-kentico-simple-stats`, creds in `examples/DancingGoat/appsettings.json`; `docker exec mssql2022 /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P ... -d xperience-by-kentico-simple-stats -Q "..."`):

- 3 forms (`CMS_Form` → `CMS_Class.ClassTableName`): `Form_DancingGoat_CoffeeSampleList`, `Form_Form_2023_09_12_17_45` (Contact Us), `Form_Form_2023_09_15_10_28` (Subscription). **All form data tables have 0 rows.**
- `OM_Activity` has 570 `bizformsubmit` rows over the last 30 days (`ActivityItemID` = `CMS_Form.FormID`, `ActivityChannelID` set). Generator writes activities only, not form data.
- `EmailLibrary_EmailStatistics` = 0 rows → email summary has nothing to show yet. Referrers: 3 rows total → top referrers near empty.

So forms is the best remaining first-release report: visible, medium-low effort, most new shared infra.

## Slices

1. Form submissions per form (`implementation-spec-1.md`)

## Decisions

### Data source decision

simple-stats PLAN says "Form data tables". Keep that as the source of truth: activities only exist for tracked contacts (consent, activity logging on), so they undercount. Form data tables hold every submission (`FormInserted` system column).

For visual testing, **seed the local dev DB only** (never code in `src/`): insert rows into the 3 form tables with `FormInserted` spread over the last 90 days, uneven per form, and leave one form with 0 rows in part of the range so "least used" shows. Easiest: copy from the `bizformsubmit` activities (`INSERT ... SELECT ActivityCreated ... FROM OM_Activity WHERE ActivityItemID = @formId`) plus some Contact Us rows. Check each table's `NOT NULL` columns first. Save the seed script as `.agent-resources/report-04-form-submissions/seed-form-submissions.sql` so the user can re-run it. Say in the report that seeding was done.

## Slice log

- 2026-09-29 – Slice 1: report + dynamic series, ranked with zeros, admin links, shared comparison infra (`20e8c3e`).
