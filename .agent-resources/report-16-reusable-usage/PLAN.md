# Plan: Report 16 – Reusable content usage

## Goal

Audience: content marketers and editors. Answers "which reusable items are used the most, and where?" so editors know which items affect many pages and emails before changing or deleting them. The opposite of the "Unused reusable items" tile in Content inventory. simple-stats PLAN "Later candidates → Content management": most-used reusable items. Current-state report in the **Content** section. Read `.agent-resources/simple-stats/PLAN.md`, `.agent-resources/report-05-content-inventory/` (`implementation-spec-2.md`, `implementation-spec-3.md`: usage definition, item links) first. Build on existing code; do not duplicate.

## Research

### Data (checked in local DB 2026-10-06)

- `CMS_ContentItemReference`: `ContentItemReferenceSourceCommonDataID` (→ `CMS_ContentItemCommonData.ContentItemCommonDataID` → `...ContentItemID` = the referencing item), `ContentItemReferenceTargetItemID` (the referenced item). 222 rows locally.
- Usage definition: the same as the "Unused reusable items" tile (any language and version of the referencing item; content item selector, rich text, Page/Email Builder properties, custom components with a reference extractor). Reuse the wording from `docs/Usage-Guide.md`.
- Referencing kind: the referencing item's `CMS_Class.ClassContentTypeType` (website / reusable / email / headless).
- Targets: reusable items only (`ClassContentTypeType` = reusable, `ClassType` content type), as `UnusedWhere` in `ContentInventorySql`.
- Item links: reusable items open in the Content hub (existing workspace link).

Product behavior (docs: Content items → Delete content items, Track usage of content items; Content versioning):

- Each item has a **Used in** tab and an **In use** column in the Content hub. This report ranks all items at once.
- Changing a reusable item does not change emails that were already delivered.

## Design rules

### Definitions

- **Usages**: distinct referencing content items (not references, not language variants). Split by referencing kind: pages, emails, reusable items, headless items.
- **Used once**: exactly 1 usage (single use, may not need to be reusable).

## Slices

1. Reusable content usage (`implementation-spec-1.md`)

## Slice log

- 2026-10-07 – Slice 1: Reusable content usage report (`6a83fe9`; spec `253afb9`).
