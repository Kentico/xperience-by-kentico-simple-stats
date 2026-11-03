# Slice 1 – Translation status

Goal, research, definitions and decisions: `PLAN.md`.

## Scope

### Server

1. **Page** `TranslationStatusPage`, slug `translation-status`, name "Translation status", parent `StatsContentSection`, order 560, icon from `Icons` (pick an existing member), template `@kentico/xperience-labs-simple-stats-admin/TranslationStatus`, derived from `StatsReportPage<>`.
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

1 outdated variant exists (item 88). For more, back-date `ContentItemLanguageMetadataModifiedWhen` of 3–4 `es` variants in the **local dev DB only**, saved as `.agent-resources/report-18-translation-status/seed-translation-status.sql` (re-runnable). Never put seeding in `src/`.

## Done when

- Same build, test, format and visual check rules as `.agent-resources/report-12-email-summary/implementation-spec-1.md`.
- Missing count per language matches the Content inventory language coverage tile with the same filter.
- `docs/Usage-Guide.md`: report section, permission row, navigation table, export names.
- Nothing DancingGoat-specific in `src/`. Do not commit. Report back concise.
