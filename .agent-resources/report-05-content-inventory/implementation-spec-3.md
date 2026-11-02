# Slice 3 – Round 3 (user feedback)

1. **Usages:** rich text links are counted in `CMS_ContentItemReference`, plus any component with a registered reference extractor (see Kentico docs). Update hint + usage guide: no "rich text not verified" caveat.
2. **Content item admin links:** use the API in `../xperience-by-kentico-content-model-graph/src/Kentico.Xperience.ContentModelGraph/ContentItemRelationshipGraphBuilder.cs` for generating admin UI links, and `AdminUrlHelper` (used in `ContentModelGraphBuilder.cs` ~L768) that prepares generated URLs for client-side use. Compare with our `StatsAdminLinks` + `adminLinks.ts`; reuse/align rather than duplicate. Add links for items in oldest content, unused reusable, action needed lists.
3. **Layout:** "Content age" bar chart and "Status" donut use predictable, limited space → stack both vertically in a right column next to "Oldest content" (left, wider). Stack on narrow screens.
