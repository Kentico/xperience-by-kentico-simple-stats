# Slice 3 – Round 3 (user feedback)

Most top sources are native `CMS.*` sources (for example `CMS.ContentEngine.ContentItemAssetVariantCacheCleaner`) and crowd the list.

1. **Source toggle on the "Top sources" tile:** All / Xperience / Custom. Tile-level (in the tile header next to the chart/table toggle), not a page filter; affects only Top sources.
2. **Xperience sources** = sources starting with `CMS.` or `Kentico.` (any `Kentico.*`; one server constant list). Everything else is custom (for example `Microsoft.*` and `WebFarmMonitor` in the local DB; `KenticoFoo` or `MyCompany.Kentico` are custom too). Must work for customer projects: custom sources rarely start with these prefixes.
3. All three lists come in the same SQL batch (TOP N each, current + previous counts, total count), so switching is instant and the cache key does not change. Prefix match with parameterized `LIKE` patterns (wildcards escaped).
4. Empty state per option (for example no custom sources yet). CSV exports the selected list and names the option in the file name.
5. Tests for classification (incl. `Kentico.Xperience.*` and other `Kentico.*` sources), `LIKE` escaping, SQL and builder. Usage guide updated.
