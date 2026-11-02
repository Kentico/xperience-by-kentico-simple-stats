# Plan: Report 21 – Campaign sources (UTM)

## Goal

Audience: content marketers. Answers "which campaigns bring visitors to this page?" Uses the UTM values that the optional Dancing Goat UTM capture sample stores on landing page activities (`docs/Usage-Guide.md` → "UTM capture (optional)", commit `566fdf1`). Two phases, with a decision in between:

1. **Phase 1:** a campaign sources section in the web page **Stats** tab, plus renaming the tab to **Stats (Labs)**.
2. **Phase 2 (only if Phase 1 shows value; user decides):** a global "Campaign sources" report in the Simple Stats (Labs) app.

Read `.agent-resources/simple-stats/PLAN.md`, `.agent-resources/report-02-top-pages/`, `.agent-resources/report-15-page-freshness/` (page visit matching) and the web page Stats tab code first (`UIPages/WebPageStatsPage.cs`, `Reports/WebPageStats/`, `Client/src/web-page-stats/WebPageStatsTemplate.tsx`, Usage Guide "Web page stats"). Build on existing code; do not duplicate.

## Research

### Data (checked in local DB 2026-10-08)

- `OM_Activity.ActivityUTMSource` / `ActivityUTMContent`: `nvarchar(200)`. Xperience does not fill them; only sites with the UTM capture sample (or similar code) have values. Locally 1 row (activity 5331: `newsletter` / `hero-banner`).
- Only **landing page** activities (`PredefinedActivityType.LANDING_PAGE`) carry UTM values. All 40 local landing page activities have `ActivityWebPageItemGUID` and `ActivityLanguageID`, so they match a page variant the same way the Stats tab matches other activities.
- A landing page activity is logged for the first page of a browsing session (new one after 20 minutes without page views). So "landings" ≈ sessions that started on the page.
- `utm_medium` / `utm_campaign` are not stored (no column).

## Design rules

### Definitions

- **Landings**: landing page activities of the page variant in the range.
- **Campaign landings**: landings with a non-empty `ActivityUTMSource`. **Campaign share**: campaign landings / landings.
- **Source**: `ActivityUTMSource` (trimmed; case as stored; empty = "(none)"). **Content**: `ActivityUTMContent` (empty = "(none)").
- **Visitors**: distinct contacts.

## Slices

1. Phase 1: campaign sources on the web page Stats (Labs) tab (`implementation-spec-1.md`)
2. Phase 2: global Campaign sources report (`implementation-spec-2.md`)

## Decisions

- 2026-10-08 (user): after Phase 1, build Phase 2. Main question: which pages get the most traffic from a specific UTM source (and content).

## Slice log

- 2026-10-08 – UTM capture sample in DancingGoat (`566fdf1`).
- 2026-10-08 – Slices 1–2: Stats tab renamed "Stats (Labs)" with campaign sources; global Campaign sources report (`4cac184`).
- 2026-10-09 – Fixes: web page stats SQL host (`964161b`); bounded source/content cache keys (`2592366`); SQL integration tests (`af1f353`).
