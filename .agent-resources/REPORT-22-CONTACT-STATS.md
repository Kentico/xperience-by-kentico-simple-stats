# Report 22: Contact stats tab

Audience: marketers looking at one contact. Answers "how engaged is this contact, how has that changed, and what are they interested in?" The product's **Activities** tab (`Kentico.Xperience.Admin.DigitalMarketing.UIPages.ContactActivityList`) is a plain list with a text search and no filters. This tab shows trends and insights instead. Read `PLAN.md`, `REPORT-01-ACTIVITY-COUNTS.md`, `REPORT-21-CAMPAIGN-SOURCES.md` and the web page Stats tab code first (`UIPages/WebPageStatsPage.cs`, `Reports/WebPageStats/`, `Client/src/web-page-stats/`). It is the model for a tab on another application's page. Build on existing code; do not duplicate.

## Placement (user decision)

- New tab on each contact in the **Contact management** application, for example `/admin/contact-management/contacts/9/overview`.
- Parent: `Kentico.Xperience.Admin.DigitalMarketing.UIPages.ContactEditSection` (confirmed in `Kentico.Xperience.Admin.DigitalMarketing.dll`). Order **1001**. Name **"Stats (Labs)"**. Pick an existing `Icons` member.
- Read the contact ID from the parent's URL parameter the way the product's contact pages do (check `ContactActivityList` / `ContactEditSection` in the decompiled assembly).
- Permission: new `StatsPermissions.CONTACT_STATS` = `SimpleStats.ContactStats`, "Contact stats". It is evaluated through `StatsApplicationPermissionEvaluator` and the tab is hidden without it, as the web page Stats tab does. Contact management's own permissions still guard the parent.

## Data (checked in local DB 2026-10-08)

- `OM_Activity`: `ActivityContactID`, `ActivityCreated`, `ActivityType`, `ActivityItemID`, `ActivityTitle`, `ActivityValue`, `ActivityURL`, `ActivityWebPageItemGUID`, `ActivityLanguageID`, `ActivityChannelID`, `ActivityUTMSource`, `ActivityUTMContent`.
- Local types: `pagevisit` 1,941, `landingpage` 611, `bizformsubmit` 575, `memberregistration` 171, `emailclick` 92. These include the 1,140 synthetic rows from `seed-campaign-sources.sql`.
- 459 contacts have activities, about 7 each on average. The top contact (454) has 104 activities over 3 days. Contact 9 has 16 activities of 5 types and was created 2026-07-15.
- Form submissions: `ActivityItemID` = `CMS_Form.FormID` (for example 1 = "Coffee sample list"). Email clicks: `ActivityItemID` = the email (see how Email summary / Activity counts resolve it), `ActivityValue` = clicked URL.
- Activity type display names come from `OM_ActivityType` (as Activity counts does). Custom activity types appear the same way.
- Landing page activities mark the start of a browsing session (see REPORT-21), so landings ≈ sessions.

## Definitions

- **Range**: presets as the shared filter, plus **All time** (default; from the contact's `ContactCreated` or first activity, whichever is earlier).
- **Sessions**: landing page activities. **Active days**: distinct days with any activity.
- **Last seen**: newest activity. **First seen**: oldest activity.
- **Trend**: activities in the range compared with the previous period of the same length (`StatsComparison`). Hidden for All time.
- Aggregates over this contact only. No other contact's data is shown, except an optional cohort figure (see Scope).

## Scope

### Server

1. **Page** `ContactStatsPage` (parent `ContactEditSection`, order 1001, name "Stats (Labs)", slug e.g. `simple-stats`), template `@kentico/xperience-admin-labs-simple-stats/ContactStats`. Use the same page-extender approach as `WebPageStatsNavigationExtender` to hide the tab without the permission.
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
   - Content interests: page visits rolled up by content type of the visited page, and by tag (user decision 2026-10-09, see "Interests by tag").
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

Contact 454 (104 activities, 3 days) and contact 9 (16 activities, 5 types) cover most tiles. For a longer trend, add a local-only seed `.agent-resources/seed-contact-stats.sql`. It inserts about 150 activities for one existing contact over 120 days (page visits, landings with UTM, form submissions, an email click), marked `ActivityComment = 'seed:contact-stats'` (re-runnable: delete marked rows first). Say which contact. Never put seeding in `src/`.

## Done when

- Same build, test, format and visual check rules as `REPORT-12-EMAIL-SUMMARY.md`.
- For contact 9, totals per type match the product **Activities** tab count.
- The tab appears after the contact's other tabs, is hidden without the permission, and shows the product "Access denied" page when opened by URL.
- `docs/Usage-Guide.md`: tab section, permission row, export names.
- Nothing DancingGoat-specific in `src/`. Do not commit. Report back concise.

## Interests by tag (user decision 2026-10-09, built)

No content type or field knowledge needed (the tag field GUID is ignored):

- Chain: page visit → page (`ActivityWebPageItemGUID` + `ActivityLanguageID` → `CMS_WebPageItem`) → page content item → its tags, plus tags of the items it links to one level deep (`CMS_ContentItemReference`, source = the page's `CMS_ContentItemCommonData` row) → `CMS_ContentItemTag` → `CMS_Tag` → `CMS_Taxonomy`.
- References come from the page's **published** common data row (`VersionStatus.Published`) in the visit's language, so links that exist only in a draft do not count.
- Tags of an item (page or linked item) are read from its language variant in the visit's language, else from its variant with the lowest `ContentItemLanguageMetadataID`.
  - **Known issue (user, 2026-10-09; fix later):** lowest metadata ID is arbitrary. Fix: follow the visit language's fallback chain (`CMS_ContentLanguage.ContentLanguageFallbackContentLanguageID`, as the product resolves content), then the default language, then lowest ID as last resort. Add a test with a 3-language chain.
- A page visit counts once per distinct tag (a tag reached through the page and a linked item, or two linked items, counts once for that visit). Values do not add up, so the list has no share column.
- Taxonomy filter: `TaxonomyId` in the `LOAD` filter (0 / null = all). Options: taxonomies with tags reached by the contact's page visits in the range and types, plus the selected one. It is part of the main batch and its cache key (the batch is per contact, so cheap). Only existing taxonomies are used (site-wide cached list of `CMS_Taxonomy` IDs); an unknown ID means all, so a request cannot add cache entries at will. All taxonomies, not just the options, so a selected taxonomy stays selected in a range without its tags.
- Insight "Top interest" uses the top tag ("Top interest: Arabica (Coffee tastes)") when the visits reached any tag, else the top content type.
- Only page visits contribute, so the activity type filter applies; without page visits selected, the tile shows its empty message.
- Export: `contact-stats-tags`.
