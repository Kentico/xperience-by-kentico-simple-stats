# Slice 2 – Interests by tag (user decision 2026-10-09)

No content type or field knowledge needed (the tag field GUID is ignored):

- Chain: page visit → page (`ActivityWebPageItemGUID` + `ActivityLanguageID` → `CMS_WebPageItem`) → page content item → its tags, plus tags of the items it links to one level deep (`CMS_ContentItemReference`, source = the page's `CMS_ContentItemCommonData` row) → `CMS_ContentItemTag` → `CMS_Tag` → `CMS_Taxonomy`.
- References come from the page's **published** common data row (`VersionStatus.Published`) in the visit's language, so links that exist only in a draft do not count.
- Tags of an item (page or linked item) are read from its language variant in the visit's language, else from its variant with the lowest `ContentItemLanguageMetadataID`.
  - **Known issue (user, 2026-10-09; fix later):** lowest metadata ID is arbitrary. Fix: follow the visit language's fallback chain (`CMS_ContentLanguage.ContentLanguageFallbackContentLanguageID`, as the product resolves content), then the default language, then lowest ID as last resort. Add a test with a 3-language chain.
- A page visit counts once per distinct tag (a tag reached through the page and a linked item, or two linked items, counts once for that visit). Values do not add up, so the list has no share column.
- Taxonomy filter: `TaxonomyId` in the `LOAD` filter (0 / null = all). Options: taxonomies with tags reached by the contact's page visits in the range and types, plus the selected one. Tags and taxonomy options are their own batch (`ContactStatsSql.TagsQuery`, reads the page visits itself) and cache entry, keyed with the taxonomy; the main batch and its cache key have no taxonomy. So a taxonomy change reads only the tags; Refresh reloads both. The heatmap ignores the taxonomy, and the client does not reload it on a taxonomy-only change. Only existing taxonomies are used (site-wide cached list of `CMS_Taxonomy` IDs); an unknown ID means all, so a request cannot add cache entries at will. All taxonomies, not just the options, so a selected taxonomy stays selected in a range without its tags.
- Insight "Top interest" uses the top tag ("Top interest: Arabica (Coffee tastes)") when the visits reached any tag, else the top content type.
- Only page visits contribute, so the activity type filter applies; without page visits selected, the tile shows its empty message.
- Export: `contact-stats-tags`.
