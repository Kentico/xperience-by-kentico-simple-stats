# Slice 1 – Contact stats tab

Goal, research, definitions and decisions: `PLAN.md`.

## Scope

### Server

1. **Page** `ContactStatsPage` (parent `ContactEditSection`, order 1001, name "Stats (Labs)", slug e.g. `simple-stats`), template `@kentico/xperience-labs-simple-stats-admin/ContactStats`. Use the same page-extender approach as `WebPageStatsNavigationExtender` to hide the tab without the permission.
2. **Filter**: range (with All time) + grouping + **activity types** (multi-select; empty = all types). No channel filter (one contact).
   - Type options: the activity types this contact has (any date), with display names from `OM_ActivityType`. Unknown types in the request are ignored, unless none is known: then the request's types are kept (trimmed, `|` replaced, distinct, sorted, max 20 types of 100 chars) so the result is empty, not all types (code review fix 5, option A).
   - Pass the selected types as a parameterized list (table-valued parameter, `STRING_SPLIT` on a parameter, or one parameter per type, whichever the repo's SQL helpers already support; no string concatenation of values).
   - The type filter applies to every query (totals, series, pages, forms, emails, campaigns, interests, heatmap, insights). Sorted type codes are part of the cache key, so `[a,b]` and `[b,a]` share an entry.
   - KPIs/tiles that cannot apply to the selected types (e.g. "Forms submitted" when form submissions are not selected) show the tile's empty message, not a hidden tile, so the layout stays stable.
3. **Queries** (one round trip, parameterized, `@ContactID`):
   - Totals: activities, sessions, page visits, form submissions, email clicks, active days, first / last seen. Previous-period totals.
   - Activities per period by type (stacked series, as Activity counts).
   - **Not in the main batch:** activity by weekday and hour (7×24 counts) for "when is this contact active" is a separate page command (e.g. `LOAD_HEATMAP`, same permission as `LOAD`), run only when the user asks for it (see Client). Same filter (range + types). Own cache entry. Server time zone, as other reports; say so.
   - Top pages visited: page visits by web page + language, with page name and channel. Link as in Top pages (public URL), plus the admin page link if `StatsChannelItemPaths` makes it cheap.
   - Forms submitted: form name, submissions, last submitted.
   - Emails clicked: email name, clicks, last clicked.
   - Campaign sources: sessions by UTM source / content (reuse `Shared/StatsUtm.cs`). Hidden when there is no UTM data (reuse the site-wide check).
   - Content interests: page visits rolled up by content type of the visited page, and by tag (user decision 2026-10-09, see `implementation-spec-2.md`).
4. **Insights** (server-built short text lines, no AI). Show only the lines that apply:
   - "Active on N of the last M days" / "Not seen for N days" (when last seen > 30 days, server constant).
   - "Activity up/down X% vs previous period".
   - "Most visited: <page>" and "Top interest: <content type>".
   - "Came from <source> in N of M sessions" (with UTM data).
   - "Submitted <form> N times" (latest form).
5. **Result** (suggested): `ContactStatsResult { ContactId, From, To, Grouping, Totals, Comparison, Series, TopPages, Forms, Emails, Campaigns, Interests, Insights, UpdatedAt }`.
6. **Service**: interface + `StatsCache`, keyed by contact and filter. Tests: contact with no activities (empty state); All time range; deleted form / email (fallback label); activities in another channel; previous-period comparison; insight rules (each line on and off); permission flag; type filter (one type, several, unknown type ignored, only unknown types give an empty result not all types, cache key order-independent); heatmap command (separate query, same filter, not run by `LOAD`). Heatmap result (suggested): `ContactHeatmapResult { Cells (weekday, hour, count), Max, UpdatedAt }`.

### Client

- Template `contact-stats/ContactStatsTemplate.tsx`, exported in `entry.tsx` with the export-permission wrapper.
- Filter bar: range (All time default) + grouping + activity types (`MultiSelect` from `@kentico/xperience-admin-components`; add it to `shared/filterControls.tsx` as a reusable control), refresh, updated at.
- **Insights** callout at the top (bullets from the server).
- KPI row: activities (change), sessions, active days, last seen.
- Tile "Activity over time" (wide): stacked columns by type / `TimeSeriesTable`. CSV.
- Tile "When active": **lazy-loaded**. Initially shows a short description and a **Generate heatmap** button; no data is requested. Clicking it calls the heatmap command and shows a loading state, then the weekday × hour heatmap (amCharts heatmap if it fits the chart theme; otherwise a simple table with shaded cells). CSV once loaded. When the filter changes after generating, keep the tile generated and reload it with the new filter (the user already asked for it); Refresh reloads it too. Reloading the page resets it to the button.
- Tile "Pages visited": `RankedBarChart` / `RankedTable`, links. CSV.
- Tiles "Forms submitted" and "Emails clicked": `RankedTable` each, side by side. CSV.
- Tile "Campaign sources" (only with UTM data): ranked by sessions. CSV.
- Tile "Interests": "Tags / Content types" toggle in the tile header (one full-width tile, so the layout stays stable when Campaign sources is hidden); Tags is the default view, with a taxonomy select (all or one; shown when there is more than one option). CSV per view.
- Link to the product **Activities** tab for the full list.
- `DataRetentionNote` (activity cleanup).
- Export names: `contact-stats-series`, `contact-stats-heatmap`, `contact-stats-pages`, `contact-stats-forms`, `contact-stats-emails`, `contact-stats-campaigns`, `contact-stats-interests`, `contact-stats-tags`.

### Privacy

- The tab shows one contact's data that Contact management users can already see in the Activities tab, but aggregated. No other contacts' details.
- Usage guide: the permission shows per-contact behavior data. Grant it like other contact data.

## Local data

Contact 454 (104 activities, 3 days) and contact 9 (16 activities, 5 types) cover most tiles. For a longer trend, add a local-only seed `.agent-resources/report-22-contact-stats/seed-contact-stats.sql`. It inserts about 150 activities for one existing contact over 120 days (page visits, landings with UTM, form submissions, an email click), marked `ActivityComment = 'seed:contact-stats'` (re-runnable: delete marked rows first). Say which contact. Never put seeding in `src/`.

## Done when

- Same build, test, format and visual check rules as `.agent-resources/report-12-email-summary/implementation-spec-1.md`.
- For contact 9, totals per type match the product **Activities** tab count.
- The tab appears after the contact's other tabs, is hidden without the permission, and shows the product "Access denied" page when opened by URL.
- `docs/Usage-Guide.md`: tab section, permission row, export names.
- Nothing DancingGoat-specific in `src/`. Do not commit. Report back concise.
