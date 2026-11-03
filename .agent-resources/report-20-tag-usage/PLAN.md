# Plan: Report 20 – Tag usage

## Goal

Audience: content leads and taxonomy owners. Answers "which tags do we use, which are never used, and how much content is untagged?" Smart folders and listings often filter by tags, so untagged content is hard to find. simple-stats PLAN "Later candidates → Content management → Quality and governance": tag coverage. Current-state report in the **Content** section. Read `.agent-resources/simple-stats/PLAN.md`, `.agent-resources/report-05-content-inventory/` (kind/channel filter, item links) first. Build on existing code; do not duplicate.

## Research

### Data (checked in local DB 2026-10-07)

- `CMS_Taxonomy` (7), `CMS_Tag` (32: `TagGUID`, `TagTaxonomyID`, `TagParentID`, `TagTitle`), `CMS_ContentItemTag` (155: `ContentItemTagContentItemLanguageMetadataID`, `ContentItemTagFieldGUID`, `ContentItemTagTagGUID`).
- Tags are stored per language variant and per field (field GUID). Locally 9 fields in use; e.g. Image tags: 7 tags, 89 uses.
- Taxonomy fields: `columntype="taxonomy"` in `CMS_Class.ClassFormDefinition`. Content types with own fields (e.g. `DancingGoat.Cafe`, `DancingGoat.Image`) have them directly. Fields from **reusable field schemas** are defined in the `CMS.ContentItemCommonData` form definition, and content types reference schemas with `<schema guid=...>` (e.g. `DancingGoat.ProductGrinder`).
- **Unknown (verify first):** the API to list taxonomy fields per content type including schema fields. Try `FormInfo` from `DataClassInfo` + `IReusableFieldSchemaManager` (or how the product builds the content type form). Field caption + taxonomy from the field settings. Do this in C# (cached), not SQL XML parsing. If it can't be done cleanly, drop untagged share and say so.

## Design rules

### Definitions

- **Tag uses**: distinct language variants with the tag (any field).
- **Unused tag**: no `CMS_ContentItemTag` row. Parent tags count only their own uses (not children); say so.
- **Untagged** (per taxonomy field): variants of content types that have the field, with no tag in that field.

## Slices

1. Tag usage (`implementation-spec-1.md`)

## Slice log

- 2026-10-08 – Slice 1: Tag usage report (`3355f1a`; spec `4d1d850`); fix: coverage chart 100% label, labels and hint (`d58891a`).
