# Slice 2 – Phase 2: global Campaign sources report

User decision after Phase 1: build it. Main question (user): **which pages get the most traffic from a specific UTM source (and content)**.

## Page

- `UIPages/CampaignSourcesPage.cs`, slug `campaign-sources`, "Campaign sources", **Contacts** section, order 250 (after Top pages 200), icon `Icons.Campaign`, template `@kentico/xperience-labs-simple-stats-admin/CampaignSources`.
- Permission `StatsPermissions.CAMPAIGN_SOURCES` = `SimpleStats.CampaignSources`, "Campaign sources"; on the page, `LOAD` and the application page.

## Filters

- `CampaignSourcesFilter { Range (StatsFilter), Source, Content }` (wraps `StatsFilter`, like Publishing activity). Range, grouping, website channel (`ActivityChannelID`; non-website channel dropped).
- **Source**: all, or one (options: sources with campaign landings in the range, up to 100). Trimmed, max 200 chars.
- **Content**: only with a source. `null` = all, `""` = "(none)" (no content), else one value. Options: contents of the source in the range (up to 25), plus `""` when selected. Changing the source clears the content.
- Unknown values: the service accepts only a source in the source options and a content in that source's content options (case-insensitive; the stored value is used). Anything else means all, and the result's `Source` / `Content` say what was applied; the client shows them (e.g. a range without landings of the selected source resets the source to all). The options come from the unfiltered and source-only data, which are cached anyway. Reason: source and content are part of the cache key, so free-text values must not add cache entries and queries at will.
- Client: new shared `TextSelect` (free-text values, also empty) in `filterControls.tsx`.

## What follows the filter

| Part | Source filter | Content filter |
| --- | --- | --- |
| KPI landings, sources | no | no |
| KPI campaign landings, share, visitors | yes | yes |
| Top landing pages | yes | yes |
| Over time | yes | yes |
| Top sources | no (it is the option list) | no |
| Source and content | yes | no (it is the content breakdown) |

## Server

- `Reports/CampaignSources/` Models, Sql, Repository, ReportBuilder, Service (Publishing activity split). One batch: landings from the previous period start to the range end copied once into `@Landings` (contact IDs stay there; aggregates only), then 5 result sets: totals, daily selected campaign landings by top 5 sources (rest folded in SQL as `NULL` = "Other"), sources (TOP 100, with previous period), pages (TOP 25, page name / language / channel joined via `CMS_WebPageItem` GUID, `CMS_ContentItemLanguageMetadata`, `CMS_WebsiteChannel`, `CMS_Channel`; one URL without query string), source + content pairs (TOP 25).
- Cache: daily aggregate per range, channel, source, content (length-prefixed); grouping does not split the cache.
- KPIs: landings, campaign landings, sources each a `StatsComparison`; campaign share + visitors (range only).
- Page links: label = page name in the landing's language (URL when the page is gone, else "(unknown page)"), secondary label "Channel · Language", link = public URL (new tab), same as Top pages (`TopPagesReportBuilder.GetPublicUrl`). No admin link (`RankedTable` shows one link per row).
- Shared with Phase 1 (`Shared/StatsUtm.cs`): trimmed source/content SQL expressions, "(none)" label, source+content pair key, `HasAnyUtmData` query + site-wide cached check (`IStatsUtmDataRepository`, moved out of `IWebPageStatsRepository`). `StatsSql.ActivityUrlCutApply` / `ActivityUrlWithoutQuery` shared with Top pages. Client: `shared/campaigns.ts` (empty-state text, "(none)", shown-count text, usage guide link).

## Client

- `campaign-sources/CampaignSourcesTemplate.tsx`, exported in `entry.tsx` with the export wrapper.
- KPI row: Landings, Campaign landings (comparison), Campaign share (visitors), Sources (comparison).
- Tiles: "Top landing pages" (full width, first: the main question; bar chart / table with channel and language column); halves "Top sources" (with previous period and change) and "Source and content" (table); "Campaign landings over time" (stacked, top 5 + Other).
- Exports: `campaign-sources-pages`, `campaign-sources-sources`, `campaign-sources-content`, `campaign-sources-series`.
- Hints: "About campaign data" callout (UTM values only with UTM capture, link to the usage guide section; only `utm_source` / `utm_content` stored, put the campaign name in `utm_content`; landings ≈ sessions; no conversions) + `DataRetentionNote`. Empty state wording same as Phase 1 when the site has no UTM data.

## Out of scope

Conversions / attribution after a campaign landing.

## Local data

`.agent-resources/report-21-campaign-sources/seed-campaign-sources.sql` (local dev DB only): inserts ~570 landing page activities (450 campaign from 6 sources, 120 without UTM) over the last 2-89 days, each with the page visit the product logs right after it, on 10 page variants (incl. Home in Spanish), source-specific page mixes and trends. Marker `ActivityComment = 'seed:campaign-sources'` (unused by landing/page visit rows); re-run deletes marked rows first.
