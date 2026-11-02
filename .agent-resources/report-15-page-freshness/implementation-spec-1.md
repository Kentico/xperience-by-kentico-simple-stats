# Slice 1 – Page freshness

Goal, research, definitions and decisions: `PLAN.md`.

## Scope

### Server

1. **Page** `PageFreshnessPage`, slug `page-freshness`, name "Page freshness", parent `StatsContentSection`, order 530, icon from `Icons` (pick an existing member), template `@kentico/xperience-labs-simple-stats-admin/PageFreshness`, derived from `StatsReportPage<>`.
   - Permission `StatsPermissions.PAGE_FRESHNESS` = `SimpleStats.PageFreshness`, "Page freshness".
2. **Filter**: `StatsFilter` range (no grouping) + website channel (existing channel filter, website channels only). Do not change other reports' cache keys.
3. **Queries** (one round trip, parameterized, aggregate visits in SQL once per variant, `TOP (@Limit)` on lists):
   - Visits per published page variant in the range (CTE): web page item ID, language, visits, visitors.
   - Age buckets of published variants (< 3, 3–6, 6–12, > 12 months), each with variant count and visits. Reuse the bucket parameters of `ContentInventorySql`.
   - Stale but popular list: name, channel, language, URL path (tree path is fine; say which), last modified, visits, visitors, link.
   - No visits list: name, channel, language, first published, last modified, link. Plus total count (window count before `TOP`).
4. **Result** (suggested): `PageFreshnessResult { From, To, ChannelId, PublishedPages, StalePages, StaleVisitShare, NoVisitPages, AgeBuckets, StalePopular (rows), NoVisits (rows), ChannelOptions, UpdatedAt }`.
5. **Service**: interface + `StatsCache`. Tests: no activities; page with visits in another language only; page first published inside the range (not in "no visits"); folders excluded; stale threshold edge; channel filter.

### Client

- Template `page-freshness/PageFreshnessTemplate.tsx`, exported in `entry.tsx` with the export-permission wrapper.
- Filter bar: range + website channel, refresh, updated at. No grouping.
- KPI row: published pages, stale pages (share), share of visits that went to stale pages, pages with no visits.
- Tile "Stale but popular" (wide): `RankedBarChart` by visits / table (page, language, last modified, visits, visitors), links. CSV.
- Tile "Visits by page age": column chart of age buckets (variants and visits) / table. CSV.
- Tile "Published pages with no visits": table with links. CSV.
- `DataRetentionNote` (activity cleanup). Hint: visits need activity tracking and cookie consent; a page with no visits may only be missing tracking.
- Export names: `page-freshness-stale-popular`, `page-freshness-age`, `page-freshness-no-visits`.

## Local data

Visits exist (seeded by the Sample data generator) but all content was modified recently. Reuse `.agent-resources/report-05-content-inventory/seed-content-age.sql` to back-date `ModifiedWhen` on a few visited pages and check the stale list. For "no visits", back-date `ContentItemCommonDataFirstPublishedWhen` on 2–3 unvisited pages in the same script (local dev DB only, re-runnable). Never put seeding in `src/`.

## Done when

- Same build, test, format and visual check rules as `.agent-resources/report-12-email-summary/implementation-spec-1.md`.
- Visits for 2 pages match their **Stats** tab (Web page stats) for the same range and language.
- `docs/Usage-Guide.md`: report section, permission row, navigation table, export names.
- Nothing DancingGoat-specific in `src/`. Do not commit. Report back concise.
