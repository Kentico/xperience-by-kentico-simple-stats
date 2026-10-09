# Report 21: Campaign sources (UTM)

Audience: content marketers. Answers "which campaigns bring visitors to this page?" Uses the UTM values that the optional Dancing Goat UTM capture sample stores on landing page activities (`docs/Usage-Guide.md` → "UTM capture (optional)", commit `566fdf1`). Two phases, with a decision in between:

1. **Phase 1:** a campaign sources section in the web page **Stats** tab, plus renaming the tab to **Stats (Labs)**.
2. **Phase 2 (only if Phase 1 shows value; user decides):** a global "Campaign sources" report in the Simple Stats (Labs) app.

Read `PLAN.md`, `REPORT-02-TOP-PAGES.md`, `REPORT-15-PAGE-FRESHNESS.md` (page visit matching) and the web page Stats tab code first (`UIPages/WebPageStatsPage.cs`, `Reports/WebPageStats/`, `Client/src/web-page-stats/WebPageStatsTemplate.tsx`, Usage Guide "Web page stats"). Build on existing code; do not duplicate.

## Data (checked in local DB 2026-10-08)

- `OM_Activity.ActivityUTMSource` / `ActivityUTMContent`: `nvarchar(200)`. Xperience does not fill them; only sites with the UTM capture sample (or similar code) have values. Locally 1 row (activity 5331: `newsletter` / `hero-banner`).
- Only **landing page** activities (`PredefinedActivityType.LANDING_PAGE`) carry UTM values. All 40 local landing page activities have `ActivityWebPageItemGUID` and `ActivityLanguageID`, so they match a page variant the same way the Stats tab matches other activities.
- A landing page activity is logged for the first page of a browsing session (new one after 20 minutes without page views). So "landings" ≈ sessions that started on the page.
- `utm_medium` / `utm_campaign` are not stored (no column).

## Definitions

- **Landings**: landing page activities of the page variant in the range.
- **Campaign landings**: landings with a non-empty `ActivityUTMSource`. **Campaign share**: campaign landings / landings.
- **Source**: `ActivityUTMSource` (trimmed; case as stored; empty = "(none)"). **Content**: `ActivityUTMContent` (empty = "(none)").
- **Visitors**: distinct contacts.

## Phase 1: web page Stats tab

### Rename

- Tab name "Stats" → **"Stats (Labs)"** (`UIPage` name in `WebPageStatsPage.cs`). Keep the slug `simple-stats`, permission and order. Update doc comments, tests and `docs/Usage-Guide.md` mentions of the tab name ("Web page stats" section, Page freshness reference).

### Server

- Extend the existing Stats tab query batch (one round trip, same range filter, same page/language match by `ActivityWebPageItemGUID` + `ActivityLanguageID`):
  - Totals: landings, campaign landings, distinct visitors from campaigns.
  - Per source: landings, visitors. Ranked, `TOP (@Limit)`.
  - Per source + content: landings. Ranked, `TOP (@Limit)`.
  - Campaign landings per period (range grouping), optional: only if cheap; stacked by top 5 sources + "Other".
- Add to `WebPageStatsResult` (e.g. `Campaigns { Landings, CampaignLandings, CampaignVisitors, BySource, BySourceContent, HasAnyUtmData }`). `HasAnyUtmData`: any activity in the DB with `ActivityUTMSource` set (cheap `EXISTS`, cached), to tell "no UTM capture on this site" apart from "no campaign visits to this page".
- Aggregates only, no contact details (same rule as the tab).
- Tests: no landings; landings without UTM; several sources and contents; other language not counted; range edges; `HasAnyUtmData` false vs true.

### Client

- New section in `WebPageStatsTemplate.tsx` below "Activities on this page":
  - KPI row (or extra cards in the existing row if they fit without wrapping): landings, campaign landings (share).
  - Tile "Campaign sources": `RankedBarChart` by landings / `RankedTable` (source, landings, visitors). CSV.
  - Tile "Source and content": table (source, content, landings). CSV. Merge into the first tile's table view if two tiles feel heavy; say which.
  - Empty state when `HasAnyUtmData` is false: "No UTM values are stored on this site. See UTM capture in the usage guide." (plain text, no link needed unless one exists already). When true but none for this page: "No campaign landings on this page in the selected range."
- Export names: `web-page-stats-campaign-sources`, `web-page-stats-campaign-content`.
- `docs/Usage-Guide.md` "Web page stats": the new section, definitions, that values come from the UTM capture sample, export names.

### Local data

Only 1 UTM landing exists. Seed in the **local dev DB only**: set `ActivityUTMSource` / `ActivityUTMContent` on ~30 existing landing page activities across 3–4 pages (sources like `newsletter`, `linkedin`, `google`, contents like `hero-banner`, `footer`), saved as `.agent-resources/seed-utm-landings.sql` (re-runnable). Never put seeding in `src/`.

### Done when

- Same build, test, format and visual check rules as `REPORT-12-EMAIL-SUMMARY.md`.
- Activity 5331's page (`/store`, `en`) shows 1 campaign landing from `newsletter` / `hero-banner` before seeding.
- Tab shows "Stats (Labs)"; existing tab behavior unchanged.
- Nothing DancingGoat-specific in `src/`. Do not commit. Report back concise, including a short note on whether a global report looks useful with the seeded data.

## Phase 2: global Campaign sources report (built 2026-10-08)

User decision after Phase 1: build it. Main question (user): **which pages get the most traffic from a specific UTM source (and content)**.

### Page

- `UIPages/CampaignSourcesPage.cs`, slug `campaign-sources`, "Campaign sources", **Contacts** section, order 250 (after Top pages 200), icon `Icons.Campaign`, template `@kentico/xperience-admin-labs-simple-stats/CampaignSources`.
- Permission `StatsPermissions.CAMPAIGN_SOURCES` = `SimpleStats.CampaignSources`, "Campaign sources"; on the page, `LOAD` and the application page.

### Filters

- `CampaignSourcesFilter { Range (StatsFilter), Source, Content }` (wraps `StatsFilter`, like Publishing activity). Range, grouping, website channel (`ActivityChannelID`; non-website channel dropped).
- **Source**: all, or one (options: sources with campaign landings in the range, up to 100, plus the selected one). Trimmed, max 200 chars.
- **Content**: only with a source. `null` = all, `""` = "(none)" (no content), else one value. Options: contents of the source in the range (up to 25) plus the selected one. Changing the source clears the content.
- Client: new shared `TextSelect` (free-text values, also empty) in `filterControls.tsx`.

### What follows the filter

| Part | Source filter | Content filter |
| --- | --- | --- |
| KPI landings, sources | no | no |
| KPI campaign landings, share, visitors | yes | yes |
| Top landing pages | yes | yes |
| Over time | yes | yes |
| Top sources | no (it is the option list) | no |
| Source and content | yes | no (it is the content breakdown) |

### Server

- `Reports/CampaignSources/` Models, Sql, Repository, ReportBuilder, Service (Publishing activity split). One batch: landings from the previous period start to the range end copied once into `@Landings` (contact IDs stay there; aggregates only), then 5 result sets: totals, daily selected campaign landings by top 5 sources (rest folded in SQL as `NULL` = "Other"), sources (TOP 100, with previous period), pages (TOP 25, page name / language / channel joined via `CMS_WebPageItem` GUID, `CMS_ContentItemLanguageMetadata`, `CMS_WebsiteChannel`, `CMS_Channel`; one URL without query string), source + content pairs (TOP 25).
- Cache: daily aggregate per range, channel, source, content (length-prefixed); grouping does not split the cache.
- KPIs: landings, campaign landings, sources each a `StatsComparison`; campaign share + visitors (range only).
- Page links: label = page name in the landing's language (URL when the page is gone, else "(unknown page)"), secondary label "Channel · Language", link = public URL (new tab), same as Top pages (`TopPagesReportBuilder.GetPublicUrl`). No admin link (`RankedTable` shows one link per row).
- Shared with Phase 1 (`Shared/StatsUtm.cs`): trimmed source/content SQL expressions, "(none)" label, source+content pair key, `HasAnyUtmData` query + site-wide cached check (`IStatsUtmDataRepository`, moved out of `IWebPageStatsRepository`). `StatsSql.ActivityUrlCutApply` / `ActivityUrlWithoutQuery` shared with Top pages. Client: `shared/campaigns.ts` (empty-state text, "(none)", shown-count text, usage guide link).

### Client

- `campaign-sources/CampaignSourcesTemplate.tsx`, exported in `entry.tsx` with the export wrapper.
- KPI row: Landings, Campaign landings (comparison), Campaign share (visitors), Sources (comparison).
- Tiles: "Top landing pages" (full width, first: the main question; bar chart / table with channel and language column); halves "Top sources" (with previous period and change) and "Source and content" (table); "Campaign landings over time" (stacked, top 5 + Other).
- Exports: `campaign-sources-pages`, `campaign-sources-sources`, `campaign-sources-content`, `campaign-sources-series`.
- Hints: "About campaign data" callout (UTM values only with UTM capture, link to the usage guide section; only `utm_source` / `utm_content` stored, put the campaign name in `utm_content`; landings ≈ sessions; no conversions) + `DataRetentionNote`. Empty state wording same as Phase 1 when the site has no UTM data.

### Out of scope

Conversions / attribution after a campaign landing.

### Local data

`.agent-resources/seed-campaign-sources.sql` (local dev DB only): inserts ~570 landing page activities (450 campaign from 6 sources, 120 without UTM) over the last 2-89 days, each with the page visit the product logs right after it, on 10 page variants (incl. Home in Spanish), source-specific page mixes and trends. Marker `ActivityComment = 'seed:campaign-sources'` (unused by landing/page visit rows); re-run deletes marked rows first.
