# Slice 1 – Phase 1: campaign sources on the web page Stats (Labs) tab

Goal, research, definitions and decisions: `PLAN.md`.

## Rename

- Tab name "Stats" → **"Stats (Labs)"** (`UIPage` name in `WebPageStatsPage.cs`). Keep the slug `simple-stats`, permission and order. Update doc comments, tests and `docs/Usage-Guide.md` mentions of the tab name ("Web page stats" section, Page freshness reference).

## Server

- Extend the existing Stats tab query batch (one round trip, same range filter, same page/language match by `ActivityWebPageItemGUID` + `ActivityLanguageID`):
  - Totals: landings, campaign landings, distinct visitors from campaigns.
  - Per source: landings, visitors. Ranked, `TOP (@Limit)`.
  - Per source + content: landings. Ranked, `TOP (@Limit)`.
  - Campaign landings per period (range grouping), optional: only if cheap; stacked by top 5 sources + "Other".
- Add to `WebPageStatsResult` (e.g. `Campaigns { Landings, CampaignLandings, CampaignVisitors, BySource, BySourceContent, HasAnyUtmData }`). `HasAnyUtmData`: any activity in the DB with `ActivityUTMSource` set (cheap `EXISTS`, cached), to tell "no UTM capture on this site" apart from "no campaign visits to this page".
- Aggregates only, no contact details (same rule as the tab).
- Tests: no landings; landings without UTM; several sources and contents; other language not counted; range edges; `HasAnyUtmData` false vs true.

## Client

- New section in `WebPageStatsTemplate.tsx` below "Activities on this page":
  - KPI row (or extra cards in the existing row if they fit without wrapping): landings, campaign landings (share).
  - Tile "Campaign sources": `RankedBarChart` by landings / `RankedTable` (source, landings, visitors). CSV.
  - Tile "Source and content": table (source, content, landings). CSV. Merge into the first tile's table view if two tiles feel heavy; say which.
  - Empty state when `HasAnyUtmData` is false: "No UTM values are stored on this site. See UTM capture in the usage guide." (plain text, no link needed unless one exists already). When true but none for this page: "No campaign landings on this page in the selected range."
- Export names: `web-page-stats-campaign-sources`, `web-page-stats-campaign-content`.
- `docs/Usage-Guide.md` "Web page stats": the new section, definitions, that values come from the UTM capture sample, export names.

## Local data

Only 1 UTM landing exists. Seed in the **local dev DB only**: set `ActivityUTMSource` / `ActivityUTMContent` on ~30 existing landing page activities across 3–4 pages (sources like `newsletter`, `linkedin`, `google`, contents like `hero-banner`, `footer`), saved as `.agent-resources/report-21-campaign-sources/seed-utm-landings.sql` (re-runnable). Never put seeding in `src/`.

## Done when

- Same build, test, format and visual check rules as `.agent-resources/report-12-email-summary/implementation-spec-1.md`.
- Activity 5331's page (`/store`, `en`) shows 1 campaign landing from `newsletter` / `hero-banner` before seeding.
- Tab shows "Stats (Labs)"; existing tab behavior unchanged.
- Nothing DancingGoat-specific in `src/`. Do not commit. Report back concise, including a short note on whether a global report looks useful with the seeded data.
