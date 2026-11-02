# Slice 1 – Consents

Goal, research, definitions and decisions: `PLAN.md`.

## Seeding (local dev DB only, `.agent-resources/report-10-consents/`)

Extend `seed-consent-agreements.sql` (keep it re-runnable) so the "older text" tile has data: add one `CMS_ConsentArchive` row for Tracking (an older text + its hash, marked for cleanup, e.g. by a fixed GUID), and make ~25% of Tracking agreements older than ~45 days use that older hash. Check NOT NULL columns + FKs first. Never in `src/`.

## Scope

### Server

1. **Page** `ConsentsPage`, slug `consents`, name "Consents", icon matching `xp-doc-user` (verify enum member), template `@kentico/xperience-labs-simple-stats-admin/Consents`. Parent: **Contacts** section, order after Member registrations. Derive from `StatsReportPage<>`. Permission `StatsPermissions.CONSENTS` = `SimpleStats.Consents`, "Consents", same pattern as other pages; covered by `StatsNavigation` tests.
2. **Filter**: wrap `StatsFilter` (range + grouping) like `OrdersRevenueFilter`, plus optional **consent** (All or one consent ID; options from `CMS_Consent` by display name, sent as filter options; unknown ID → All). Channel normalized to null. Do not change other reports' cache keys.
3. **Definitions** (UI hints + usage guide):
   - *Agreement* / *revocation*: an agree / revoke event (row) in the range. Re-agreeing (for example to a new text) counts again.
   - *Agreed contacts* at date D: contacts whose latest row for the consent on or before D is not revoked. With All consents: distinct contacts agreed to at least one consent (say so), or sum per consent — pick one, say why.
   - *Revocation rate*: revocations ÷ agreements in the range (ratio, `null` when 0 agreements; pp change).
   - *Older text*: currently agreed contacts whose latest agreement hash ≠ the consent's current `ConsentHash`.
4. **Queries** (one round trip, parameterized, aggregate in SQL, current + previous period; missing tables → empty result + hint):
   - Daily agreements and revocations, previous period + range (consent filter applied).
   - Daily net change of agreed contacts (state transitions via `LAG` over (contact, consent) ordered by time: not agreed → agreed = +1, agreed → revoked = −1, agree while agreed = 0) plus agreed count at range start → point-in-time series with `BuildCumulativeSeries` (same idea as report 08 active customers). Totals at range end and previous period end.
   - Per consent (ignores consent filter so the table always compares consents; say so in a hint): agreements, revocations, agreed contacts now, change of agreements vs previous period, older-text count.
   - Older text: agreed contacts on current vs older text (consent filter applied).
5. **Shared infra**: reuse `BuildCumulativeSeries`, `StatsValueComparison` (Count / Ratio), ranked (secondary / tertiary values), `StatsCoverage` + `CoverageBarChart` (report 05) for current vs older text, `ComboChart`/`StackedColumnChart`. Extract a shared filter-option select if the consent select would copy the order status select.
6. **Result** (suggested): `ConsentsResult { From, To, Grouping, ConsentId, Periods, Events (series: agreements, revocations), AgreedContacts (point-in-time series), Totals { Agreements, Revocations, RevocationRate, AgreedContacts }, ByConsent, TextVersions (coverage), ConsentOptions, DataProtectionAppUrl, Available, UpdatedAt }`.
7. **Service**: interface + `StatsCache`. Tests: builder, service, SQL (no consents, no agreements, missing tables, agree → revoke → agree transitions, agree twice = one agreed contact, revoke without prior agree = no negative, consent filter, unknown consent → All, ratio null, older-text hash compare).

### Client

- Template `consents/ConsentsTemplate.tsx`, export in `entry.tsx` (export-permission wrapper).
- Filter bar: range + grouping + consent select, refresh, updated at.
- KPI row (4): Agreements, Revocations, Revocation rate (pp), Agreed contacts (at range end vs previous end).
- Tile "Agreements and revocations": `StackedColumnChart` (two series) or `ComboChart` columns — pick what reads better; table toggle; CSV.
- Tile "Agreed contacts over time": line (`ComboChart` line-only, as report 08 active customers); table (no total column, point-in-time); CSV.
- Tile "Consents" (full width): `RankedTable` primary (agreements, revocations, agreed now, change, older text), chart toggle (`RankedBarChart` of agreed now); link per consent; CSV.
- Tile "Consent text version": `CoverageBarChart` / `CoverageTable` (current vs older text among agreed contacts), hint: contacts on an older text may need to agree again.
- Link "Open data protection" to the native app.
- Hints / `DataRetentionNote`: definitions; agreements of deleted contacts disappear; the report reads stored agreements only (no consent text diff).
- Empty state when there are no consents or no agreements.

## Look and feel, goal, done when

Same as `.agent-resources/report-08-customers/implementation-spec-1.md` ("Look and feel — must feel native", "Goal", "Done when"), with: nav shows it under Contacts; a role with only this permission lands on it; Export flag hides CSV. `docs/Usage-Guide.md`: report section, permission row, navigation table. Follow `dotnet format` (uncovered switch case → throw `ArgumentOutOfRangeException` or handled error state, never a silent default). Do not commit. Report back concise: files, SQL shape, definitions chosen (esp. agreed contacts with All consents), shared changes, links (or why skipped), seeding, checks, not checked, open questions.
