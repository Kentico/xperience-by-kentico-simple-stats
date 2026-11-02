# Slice 1 – Tag usage

Goal, research, definitions and decisions: `PLAN.md`.

## Scope

### Server

1. **Page** `TagUsagePage`, slug `tag-usage`, name "Tag usage", parent `StatsContentSection`, order 580, icon from `Icons` (pick an existing member), template `@kentico/xperience-labs-simple-stats-admin/TagUsage`, derived from `StatsReportPage<>`.
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

- Same build, test, format and visual check rules as `.agent-resources/report-12-email-summary/implementation-spec-1.md`.
- For 2 tags, uses match a Content hub filter by that tag (count language variants; say if the hub counts items).
- Report back how taxonomy fields per content type were resolved (API used, schema fields covered or not).
- `docs/Usage-Guide.md`: report section, permission row, navigation table, export names.
- Nothing DancingGoat-specific in `src/`. Do not commit. Report back concise.
