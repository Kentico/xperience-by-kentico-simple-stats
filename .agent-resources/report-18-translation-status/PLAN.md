# Plan: Report 18 – Translation status

## Goal

Audience: content leads and translators. Answers "which translations are missing or behind the default language?" simple-stats PLAN "Later candidates → Content management → Quality and governance": outdated translations (extends Content inventory language coverage). Current-state report in the **Content** section. Read `.agent-resources/simple-stats/PLAN.md`, `.agent-resources/report-05-content-inventory/` (language coverage tile, kind/channel filter, item links) first. Build on existing code; do not duplicate.

## Research

### Data (checked in local DB 2026-10-07)

- Languages: `CMS_ContentLanguage` (`ContentLanguageIsDefault`, `ContentLanguageFallbackContentLanguageID`). Locally `en` (default) and `es` (fallback `en`).
- Variants: `CMS_ContentItemLanguageMetadata` per item + language, `ContentItemLanguageMetadataModifiedWhen`. Locally 10 `es` variants.
- Seeding writes both variants within a few ms of each other (e.g. `en` 13:35:00.604, `es` 13:35:00.598). Item 88: `en` modified 2026-10-06, `es` 2026-09-29 → really outdated.
- AIRA translation tasks (`CMS_TranslationTask`): 0 rows. **Out of scope.**
- Type, kind, channel filter, folders excluded, item links: as `ContentInventorySql`.

## Design rules

### Definitions

- **Default variant**: the item's variant in the default language. Items without one are left out of "outdated" (nothing to compare with) but counted in coverage.
- **Outdated**: a non-default variant whose `ModifiedWhen` is older than the default variant's `ModifiedWhen` by more than a tolerance (server constant, 1 hour; covers seeding and bulk saves).
- **Days behind**: default `ModifiedWhen` − variant `ModifiedWhen`, in days.
- **Missing**: the item has a default variant but no variant in the language (same as the Content inventory tile; reuse its query or shared SQL).
- `ModifiedWhen` changes on any save (including drafts that were never published). Say so in a hint; it is a proxy, not a content diff.

## Slices

1. Translation status (`implementation-spec-1.md`)

## Slice log

- 2026-10-07 – Slice 1: Translation status report (`8883715`; spec `4d1d850`).
