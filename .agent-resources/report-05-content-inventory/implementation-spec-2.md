# Slice 2 – Round 2 (user feedback after first build)

Overall approved. Changes:

1. **Kind + channel filters pair badly.** Selecting a website channel leaves only one kind with data. Fix: channel depends on kind.
   - All / Reusable → no channel select (reusable items have no channel).
   - Pages → website channels only; Emails → email channels only; Headless → headless channels only (extend channel options provider without changing reports 01–04 output).
   - Changing kind resets channel when it no longer fits. Server `Normalize` drops a channel that does not match the kind (cache key stays clean). Tests.
2. **Stale content** ("Content not modified in 6 or 12 months"). Use `ContentItemLanguageMetadataModifiedWhen` (verify it means last edit of latest version). Tile: age buckets (e.g. < 3 mo, 3–6, 6–12, > 12 mo) + list of oldest items (ranked by days since modified, TOP N in SQL) with admin link if simple. KPI: items not modified in 12 months. Respects kind/channel filter.
3. **Reusable items with no usages.** Use `IInfoProvider<ContentItemReferenceInfo>` (table `CMS_ContentItemReference`, 167 rows locally). Reference: `../xperience-by-kentico-content-model-graph/src/Kentico.Xperience.ContentModelGraph/ContentItemRelationshipGraphBuilder.cs` (~line 73). Do it as SQL aggregate (NOT EXISTS against the reference table's target column) rather than loading all references in memory, if the column names are clear; verify. Tile/KPI: unused reusable items count + list (by content type, oldest first). Note what counts as a usage (content item references only; e.g. rich text/Page Builder widget references may not be tracked — verify and state in a hint).
4. **Items in a workflow step for a long time ("action needed").** Combine workflow step + age. Find the best "entered step" time: check whether any table/column records it (version history, workflow step change time); if not, use `ModifiedWhen` as a proxy and say so in a hint. Tile: items in workflow steps with step name, days in step, sorted oldest first; highlight > N days (server constant, e.g. 14). Replace or extend the existing "Items in workflow steps" tile.

Local data note: all content was modified today (seeded DB), so stale/age tiles will be mostly empty. For visual testing, it is OK to back-date `ContentItemLanguageMetadataModifiedWhen` on a few items in the **local dev DB only**; save the script as `.agent-resources/report-05-content-inventory/seed-content-age.sql`. Never put seeding in `src/`.

Page layout: keep it scannable. If the page gets long, order tiles: action needed → stale → unused reusable → type → status → language coverage, or group visually. Pick what looks native.
