# Report 20: Tag usage

Audience: content leads and taxonomy owners. Answers "which tags do we use, which are never used, and how much content is untagged?" Smart folders and listings often filter by tags, so untagged content is hard to find. PLAN "Later candidates → Content management → Quality and governance": tag coverage. Current-state report in the **Content** section. Read `PLAN.md`, `REPORT-05-CONTENT-INVENTORY.md` (kind/channel filter, item links) first. Build on existing code; do not duplicate.

## Data (checked in local DB 2026-10-07)

- `CMS_Taxonomy` (7), `CMS_Tag` (32: `TagGUID`, `TagTaxonomyID`, `TagParentID`, `TagTitle`), `CMS_ContentItemTag` (155: `ContentItemTagContentItemLanguageMetadataID`, `ContentItemTagFieldGUID`, `ContentItemTagTagGUID`).
- Tags are stored per language variant and per field (field GUID). Locally 9 fields in use; e.g. Image tags: 7 tags, 89 uses.
- Taxonomy fields: `columntype="taxonomy"` in `CMS_Class.ClassFormDefinition`. Content types with own fields (e.g. `DancingGoat.Cafe`, `DancingGoat.Image`) have them directly. Fields from **reusable field schemas** are defined in the `CMS.ContentItemCommonData` form definition, and content types reference schemas with `<schema guid=...>` (e.g. `DancingGoat.ProductGrinder`).
- **Unknown (verify first):** the API to list taxonomy fields per content type including schema fields. Try `FormInfo` from `DataClassInfo` + `IReusableFieldSchemaManager` (or how the product builds the content type form). Field caption + taxonomy from the field settings. Do this in C# (cached), not SQL XML parsing. If it can't be done cleanly, drop untagged share and say so.

## Definitions

- **Tag uses**: distinct language variants with the tag (any field).
- **Unused tag**: no `CMS_ContentItemTag` row. Parent tags count only their own uses (not children); say so.
- **Untagged** (per taxonomy field): variants of content types that have the field, with no tag in that field.

## Scope

### Server

1. **Page** `TagUsagePage`, slug `tag-usage`, name "Tag usage", parent `StatsContentSection`, order 580, icon from `Icons` (pick an existing member), template `@kentico/xperience-admin-labs-simple-stats/TagUsage`, derived from `StatsReportPage<>`.
   - Permission `StatsPermissions.TAG_USAGE` = `SimpleStats.TagUsage`, "Tag usage".
2. **Filter**: snapshot request + **taxonomy** (all or one) + kind (reuse). Unknown taxonomy → all.
3. **Queries** (one round trip, parameterized):
   - Per taxonomy: tags, used tags, tag uses.
   - Top tags `TOP (@Limit)`: tag title, taxonomy, uses.
   - Unused tags: tag title, taxonomy, parent tag title. Count before `TOP`.
   - Per field GUID: variants with at least one tag. Combine in C# with the field list (field → content types, caption, taxonomy) and variant counts per content type → untagged share.
4. **Result** (suggested): `TagUsageResult { TaxonomyId, Kind, Taxonomies, TagCount, UnusedTagCount, TopTags, UnusedTags, Fields (field, content types, variants, tagged, untagged, share), TaxonomyOptions, UpdatedAt }`.
5. **Service**: interface + `StatsCache`. Tests: no taxonomies (empty state); tag used in two fields; tag used in two languages of one item (2 uses); parent tag with used child; schema field shared by several content types; taxonomy filter.

### Client

- Template `tag-usage/TagUsageTemplate.tsx`, exported in `entry.tsx` with the export-permission wrapper.
- Filter bar: `SnapshotFilterBar` with taxonomy + kind, refresh, updated at.
- KPI row: taxonomies, tags, unused tags, untagged share (overall, across fields).
- Tile "Untagged content by field" (wide): bar chart of untagged share / table (field, content types, variants, untagged). CSV.
- Tile "Top tags": `RankedBarChart` / `RankedTable`. CSV.
- Tile "Unused tags": table (tag, taxonomy, parent). CSV.
- Optional: link taxonomy names to the **Taxonomies** application (only if `IStatsAdminLinks` can build it without hardcoded paths).
- Export names: `tag-usage-fields`, `tag-usage-top`, `tag-usage-unused`.

## Local data

155 tag uses and unused tags exist; no seeding needed.

## Done when

- Same build, test, format and visual check rules as `REPORT-12-EMAIL-SUMMARY.md`.
- For 2 tags, uses match a Content hub filter by that tag (count language variants; say if the hub counts items).
- Report back how taxonomy fields per content type were resolved (API used, schema fields covered or not).
- `docs/Usage-Guide.md`: report section, permission row, navigation table, export names.
- Nothing DancingGoat-specific in `src/`. Do not commit. Report back concise.
