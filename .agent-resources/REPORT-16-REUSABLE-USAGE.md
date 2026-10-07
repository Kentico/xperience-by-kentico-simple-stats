# Report 16: Reusable content usage

Audience: content marketers and editors. Answers "which reusable items are used the most, and where?" so editors know which items affect many pages and emails before changing or deleting them. The opposite of the "Unused reusable items" tile in Content inventory. PLAN "Later candidates → Content management": most-used reusable items. Current-state report in the **Content** section. Read `PLAN.md`, `REPORT-05-CONTENT-INVENTORY.md` (Round 2/3: usage definition, item links) first. Build on existing code; do not duplicate.

## Data (checked in local DB 2026-10-06)

- `CMS_ContentItemReference`: `ContentItemReferenceSourceCommonDataID` (→ `CMS_ContentItemCommonData.ContentItemCommonDataID` → `...ContentItemID` = the referencing item), `ContentItemReferenceTargetItemID` (the referenced item). 222 rows locally.
- Usage definition: the same as the "Unused reusable items" tile (any language and version of the referencing item; content item selector, rich text, Page/Email Builder properties, custom components with a reference extractor). Reuse the wording from `docs/Usage-Guide.md`.
- Referencing kind: the referencing item's `CMS_Class.ClassContentTypeType` (website / reusable / email / headless).
- Targets: reusable items only (`ClassContentTypeType` = reusable, `ClassType` content type), as `UnusedWhere` in `ContentInventorySql`.
- Item links: reusable items open in the Content hub (existing workspace link).

Product behavior (docs: Content items → Delete content items, Track usage of content items; Content versioning):

- Each item has a **Used in** tab and an **In use** column in the Content hub. This report ranks all items at once.
- Changing a reusable item does not change emails that were already delivered.

## Definitions

- **Usages**: distinct referencing content items (not references, not language variants). Split by referencing kind: pages, emails, reusable items, headless items.
- **Used once**: exactly 1 usage (single use, may not need to be reusable).

## Scope

### Server

1. **Page** `ReusableUsagePage`, slug `reusable-usage`, name "Reusable content usage", parent `StatsContentSection`, order 540, icon from `Icons` (pick an existing member), template `@kentico/xperience-admin-labs-simple-stats/ReusableUsage`, derived from `StatsReportPage<>`.
   - Permission `StatsPermissions.REUSABLE_USAGE` = `SimpleStats.ReusableUsage`, "Reusable content usage".
2. **Filter**: snapshot request + **content type** of the target (all reusable content types, or one). Options from the reusable content types with items. Unknown type → all.
3. **Queries** (one round trip, parameterized):
   - Usage per target (CTE): target item ID, distinct referencing items in total and per kind.
   - Most-used items, `TOP (@Limit)` by total usages: name (most recently modified variant, as `UnusedItemsQuery`), content type, usages total and per kind, last modified, workspace link.
   - Usage distribution: reusable items with 0, 1, 2–5, 6–20, > 20 usages.
   - Usage per content type: items, used items, total usages.
4. **Result** (suggested): `ReusableUsageResult { ContentTypeId, ReusableItems, UsedItems, UsedOnce, Unused, MostUsed (rows), Distribution, ByContentType (ranked), ContentTypeOptions, UpdatedAt }`.
5. **Service**: interface + `StatsCache`. Tests: no references; one item referenced by several language variants and versions of the same source (counts 1); self-reference; references from each kind; content type filter.

### Client

- Template `reusable-usage/ReusableUsageTemplate.tsx`, exported in `entry.tsx` with the export-permission wrapper.
- Filter bar: `SnapshotFilterBar` with a content type select, refresh, updated at.
- KPI row: reusable items, used items (share), used once, unused (links to Content inventory, which lists them).
- Tile "Most used items" (wide): `RankedBarChart` stacked or plain by total usages / table (item, type, pages, emails, reusable, headless, total, last modified), links. CSV.
- Tile "Usage distribution": column chart of the buckets / table. CSV.
- Tile "By content type": `RankedTable` (type, items, used, usages). CSV.
- Hint: what counts as a usage (copy from the Unused tile); open the item's **Used in** tab for the full list.
- Export names: `reusable-usage-items`, `reusable-usage-distribution`, `reusable-usage-types`.

## Local data

222 references exist; no seeding needed.

## Done when

- Same build, test, format and visual check rules as `REPORT-12-EMAIL-SUMMARY.md`.
- For 3 items, usages match their **Used in** tab (count distinct items, not rows).
- Total reusable items = used + unused, and unused matches the Content inventory KPI with the same filter.
- `docs/Usage-Guide.md`: report section, permission row, navigation table, export names.
- Nothing DancingGoat-specific in `src/`. Do not commit. Report back concise.
