# Report 18: Translation status

Audience: content leads and translators. Answers "which translations are missing or behind the default language?" PLAN "Later candidates → Content management → Quality and governance": outdated translations (extends Content inventory language coverage). Current-state report in the **Content** section. Read `PLAN.md`, `REPORT-05-CONTENT-INVENTORY.md` (language coverage tile, kind/channel filter, item links) first. Build on existing code; do not duplicate.

## Data (checked in local DB 2026-10-07)

- Languages: `CMS_ContentLanguage` (`ContentLanguageIsDefault`, `ContentLanguageFallbackContentLanguageID`). Locally `en` (default) and `es` (fallback `en`).
- Variants: `CMS_ContentItemLanguageMetadata` per item + language, `ContentItemLanguageMetadataModifiedWhen`. Locally 10 `es` variants.
- Seeding writes both variants within a few ms of each other (e.g. `en` 13:35:00.604, `es` 13:35:00.598). Item 88: `en` modified 2026-10-06, `es` 2026-09-29 → really outdated.
- AIRA translation tasks (`CMS_TranslationTask`): 0 rows. **Out of scope.**
- Type, kind, channel filter, folders excluded, item links: as `ContentInventorySql`.

## Definitions

- **Default variant**: the item's variant in the default language. Items without one are left out of "outdated" (nothing to compare with) but counted in coverage.
- **Outdated**: a non-default variant whose `ModifiedWhen` is older than the default variant's `ModifiedWhen` by more than a tolerance (server constant, 1 hour; covers seeding and bulk saves).
- **Days behind**: default `ModifiedWhen` − variant `ModifiedWhen`, in days.
- **Missing**: the item has a default variant but no variant in the language (same as the Content inventory tile; reuse its query or shared SQL).
- `ModifiedWhen` changes on any save (including drafts that were never published). Say so in a hint; it is a proxy, not a content diff.

## Scope

### Server

1. **Page** `TranslationStatusPage`, slug `translation-status`, name "Translation status", parent `StatsContentSection`, order 560, icon from `Icons` (pick an existing member), template `@kentico/xperience-admin-labs-simple-stats/TranslationStatus`, derived from `StatsReportPage<>`.
   - Permission `StatsPermissions.TRANSLATION_STATUS` = `SimpleStats.TranslationStatus`, "Translation status".
2. **Filter**: snapshot request + kind + channel (reuse) + **language** (non-default languages; all or one). Unknown language → all.
3. **Queries** (one round trip, parameterized):
   - Per non-default language: items with default variant, up to date, outdated, missing.
   - Outdated variants, most days behind first, `TOP (@Limit)`: name, type, language, default modified, variant modified, days behind, modified by (user name as Content locks). Window count before `TOP`.
   - Per content type: outdated and missing counts (ranked).
4. **Result** (suggested): `TranslationStatusResult { LanguageId, Kind, ChannelId, DefaultLanguage, Languages (coverage rows), OutdatedCount, MissingCount, Outdated (rows), ByContentType, LanguageOptions, ChannelOptions, UpdatedAt }`.
5. **Service**: interface + `StatsCache`. Tests: one language only (empty state with hint); variant within tolerance (up to date); item without default variant; missing variant; language filter; kind/channel filter.

### Client

- Template `translation-status/TranslationStatusTemplate.tsx`, exported in `entry.tsx` with the export-permission wrapper.
- Filter bar: `SnapshotFilterBar` with language, kind, channel, refresh, updated at.
- Empty state when there is only one language: "Only one language is set up."
- KPI row: translated variants, outdated (highlighted when > 0), missing.
- Tile "By language": stacked bar (up to date / outdated / missing) / table. CSV.
- Tile "Outdated translations" (wide): `AgedItemTable` by days behind, links. CSV.
- Tile "By content type": `RankedTable` (type, outdated, missing). CSV.
- Hint: outdated means the default language was saved later than the translation; any save counts.
- Export names: `translation-status-languages`, `translation-status-outdated`, `translation-status-types`.

## Local data

1 outdated variant exists (item 88). For more, back-date `ContentItemLanguageMetadataModifiedWhen` of 3–4 `es` variants in the **local dev DB only**, saved as `.agent-resources/seed-translation-status.sql` (re-runnable). Never put seeding in `src/`.

## Done when

- Same build, test, format and visual check rules as `REPORT-12-EMAIL-SUMMARY.md`.
- Missing count per language matches the Content inventory language coverage tile with the same filter.
- `docs/Usage-Guide.md`: report section, permission row, navigation table, export names.
- Nothing DancingGoat-specific in `src/`. Do not commit. Report back concise.
