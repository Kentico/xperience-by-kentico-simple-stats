import { Callout, CalloutPlacementType, CalloutType, InfoCard } from '@kentico/xperience-admin-components';
import React, { useCallback, useMemo, useState } from 'react';

import { toAdminHref } from '../shared/adminLinks';
import { AgedItemCaptions, AgedItemTable, toDaysRankedItems } from '../shared/AgedItemTable';
import { channelsForContentKind, contentKindOptions, fitContentChannel } from '../shared/contentKinds';
import { CoverageBarChart, CoverageCaptions } from '../shared/CoverageBarChart';
import { CoverageTable } from '../shared/CoverageTable';
import { toAgedCsv, toCoverageCsv, toRankedCsv } from '../shared/csv';
import { numberFormat } from '../shared/format';
import { RankedBarChart } from '../shared/RankedBarChart';
import { RankedTable } from '../shared/RankedTable';
import { SnapshotFilterBar } from '../shared/SnapshotFilterBar';
import { StatsTile } from '../shared/StatsTile';
import {
  StatsAgedItem,
  StatsChannelOption,
  StatsCoverageItem,
  StatsRankedCaptions,
  StatsRankedItem,
  StatsRankedResult,
  StatsSnapshotFilter,
} from '../shared/types';
import { useCsvExport } from '../shared/useCsvExport';
import { useStatsCommand } from '../shared/useStatsCommand';
import '../shared/stats.css';

/** Mirrors `TranslationLanguageOption`. */
interface TranslationLanguageOption {
  readonly id: number;
  readonly displayName: string;
  readonly codeName: string;
}

/** Mirrors `TranslationStatusResult`. */
interface TranslationStatusResult {
  /** Applied language filter (a non-default language), `null` for all. */
  readonly languageId: number | null;
  /** Applied content type type filter (`Website`, `Reusable`, `Email`, `Headless`), `null` for all. */
  readonly kind: string | null;
  readonly channelId: number | null;
  /** Display name of the default language, `null` without one. */
  readonly defaultLanguage: string | null;
  /** Per selected language: `covered` = items with a variant, `flagged` = outdated variants, `total` = all filtered items. */
  readonly languages: readonly StatsCoverageItem[];
  /** Variants in the selected languages (up to date and outdated). */
  readonly translatedCount: number;
  readonly outdatedCount: number;
  /** Items without a variant in a selected language (one per item and language). */
  readonly missingCount: number;
  /** Most days behind first. `since` = variant's last change, `until` = default variant's, `detail` = last modified by. */
  readonly outdated: readonly StatsAgedItem[];
  /** `value` = outdated, `secondaryValue` = missing; `adminPath` opens the content type. */
  readonly byContentType: StatsRankedResult;
  /** Non-default languages. Empty when only one language is set up. */
  readonly languageOptions: readonly TranslationLanguageOption[];
  /** Minutes a variant can be older than the default variant and still be up to date. */
  readonly toleranceMinutes: number;
  /** ISO timestamp of when the data was read from the database. */
  readonly updatedAt: string;
}

/** Mirrors `TranslationStatusClientProperties`. */
interface TranslationStatusTemplateProps {
  readonly report: TranslationStatusResult;
  /** Website, email and headless channels. */
  readonly channels: readonly StatsChannelOption[];
  /** Path of this page relative to the admin root, used to build admin links. */
  readonly pagePath: string | null;
}

const coverageCaptions: CoverageCaptions = {
  covered: 'Up to date',
  flagged: 'Outdated',
  missing: 'Missing',
  totalNoun: 'items',
};

const outdatedCaptions: AgedItemCaptions = {
  label: 'Item',
  category: 'Content type',
  language: 'Language',
  channel: 'Channel',
  detail: 'Last modified by',
  since: 'Translation modified',
  until: 'Default modified',
  days: 'Days behind',
};

const outdatedDaysCaptions: StatsRankedCaptions = {
  label: 'Item',
  secondaryLabel: 'Language',
  value: 'Days behind',
};

const typeCaptions: StatsRankedCaptions = {
  label: 'Content type',
  value: 'Outdated',
  secondaryValue: 'Missing',
};

const singleLanguageMessage = 'Only one language is set up.';

function toFilter(report: TranslationStatusResult): StatsSnapshotFilter {
  return { kind: report.kind, channelId: report.channelId, languageId: report.languageId };
}

function toleranceText(minutes: number): string {
  if (minutes % 60 === 0) {
    const hours = minutes / 60;
    return hours === 1 ? '1 hour' : `${numberFormat.format(hours)} hours`;
  }
  return `${numberFormat.format(minutes)} minutes`;
}

export const TranslationStatusTemplate = (props: TranslationStatusTemplateProps) => {
  const saveCsv = useCsvExport();
  const { data: report, isLoading, hasError, load } = useStatsCommand<TranslationStatusResult, StatsSnapshotFilter>(
    props.report,
  );
  const [filter, setFilter] = useState<StatsSnapshotFilter>(() => toFilter(props.report));

  const channels = useMemo(
    () => channelsForContentKind(props.channels, filter.kind),
    [props.channels, filter.kind],
  );

  const languageOptions = useMemo(
    () =>
      report.languageOptions.map((language) => ({
        id: language.id,
        label: language.displayName,
        secondaryLabel: language.codeName,
      })),
    [report.languageOptions],
  );

  const handleFilterChange = (next: StatsSnapshotFilter) => {
    const normalized = fitContentChannel(props.channels, next);
    setFilter(normalized);
    void load(normalized);
  };

  const handleRefresh = () => {
    void load(filter, { refresh: true });
  };

  const { pagePath } = props;
  const getAdminHref = useCallback(
    (item: StatsRankedItem) => toAdminHref(item.adminPath, pagePath),
    [pagePath],
  );
  const getAgedHref = useCallback(
    (item: StatsAgedItem) => toAdminHref(item.adminPath, pagePath),
    [pagePath],
  );

  const outdatedDays = useMemo(() => toDaysRankedItems(report.outdated, (item) => item.language), [report.outdated]);

  const defaultLanguage = report.defaultLanguage ?? 'the default language';
  const tolerance = toleranceText(report.toleranceMinutes);
  const outdatedHint = `A translation is outdated when the ${defaultLanguage} variant was saved more than ${tolerance} after it. Any save counts, also drafts that were never published, so this is a hint that the translation may need an update, not a comparison of the content.`;
  const missingHint = `Items without a variant in the language, the same as the language coverage of the Content inventory.`;

  const listedText =
    report.outdatedCount > report.outdated.length
      ? ` The ${numberFormat.format(report.outdated.length)} furthest behind of ${numberFormat.format(report.outdatedCount)} are listed.`
      : '';

  const language = report.languageOptions.find((option) => option.id === report.languageId);
  const fileSuffix = [
    (report.kind ?? 'all').toLowerCase(),
    ...(report.channelId ? [`channel-${report.channelId}`] : []),
    ...(language ? [language.codeName] : []),
  ].join('_');

  const exportLanguagesCsv = () => {
    saveCsv(
      'translation-status-languages',
      `translation-status-languages_${fileSuffix}.csv`,
      toCoverageCsv(report.languages, {
        label: 'Language',
        secondaryLabel: 'Code name',
        covered: coverageCaptions.covered,
        flagged: coverageCaptions.flagged,
        missing: coverageCaptions.missing,
      }),
    );
  };

  const exportOutdatedCsv = () => {
    saveCsv(
      'translation-status-outdated',
      `translation-status-outdated_${fileSuffix}.csv`,
      toAgedCsv(report.outdated, outdatedCaptions, getAgedHref),
    );
  };

  const exportTypesCsv = () => {
    saveCsv(
      'translation-status-types',
      `translation-status-types_${fileSuffix}.csv`,
      toRankedCsv(report.byContentType.items, typeCaptions, getAdminHref),
    );
  };

  const isSingleLanguage = report.languageOptions.length === 0;
  const totalItems = report.languages.reduce((max, item) => Math.max(max, item.total), 0);
  const isEmpty = totalItems === 0;
  const emptyMessage = 'No content items match the selected filters.';

  return (
    <div className="SimpleStats-root">
      <SnapshotFilterBar
        filter={filter}
        onChange={handleFilterChange}
        kinds={contentKindOptions}
        channels={channels}
        select={
          isSingleLanguage
            ? undefined
            : {
                label: 'Language',
                allLabel: 'All languages',
                options: languageOptions,
                value: filter.languageId ?? null,
                onChange: (languageId) => handleFilterChange({ ...filter, languageId }),
              }
        }
        onRefresh={handleRefresh}
        isLoading={isLoading}
        updatedAt={report.updatedAt}
      />

      {isSingleLanguage ? (
        <Callout type={CalloutType.QuickTip} placement={CalloutPlacementType.OnDesk} headline={singleLanguageMessage}>
          <p>
            Translations are compared with the default language ({defaultLanguage}). Add a language in the Languages
            application to see which items are missing or behind.
          </p>
        </Callout>
      ) : (
        <>
          <div className="SimpleStats-kpis">
            <InfoCard
              caption="Translated variants"
              tooltip={`Variants in ${language ? language.displayName : 'the non-default languages'} of the items that match the filters, up to date and outdated.`}
              text={numberFormat.format(report.translatedCount)}
              details={`Default language: ${defaultLanguage}`}
            />
            <InfoCard
              caption="Outdated"
              tooltip={outdatedHint}
              text={numberFormat.format(report.outdatedCount)}
              details={report.outdatedCount > 0 ? 'Behind the default language – needs attention' : 'None behind the default language'}
            />
            <InfoCard
              caption="Missing"
              tooltip={missingHint}
              text={numberFormat.format(report.missingCount)}
              details="Items without a translation"
            />
          </div>

          {report.outdatedCount > 0 && (
            <Callout type={CalloutType.FriendlyWarning} placement={CalloutPlacementType.OnDesk} headline="Outdated translations">
              <p>
                {`${numberFormat.format(report.outdatedCount)} ${report.outdatedCount === 1 ? 'translation was' : 'translations were'} last saved before the ${defaultLanguage} variant. Check whether they need an update.`}
              </p>
            </Callout>
          )}

          <StatsTile
            headline="By language"
            description={`Items per language: up to date, outdated and missing, out of all items that match the filters. ${missingHint}`}
            isLoading={isLoading}
            hasError={hasError}
            isEmpty={isEmpty}
            emptyMessage={emptyMessage}
            onExportCsv={exportLanguagesCsv}
            renderChart={() => (
              <CoverageBarChart
                items={report.languages}
                captions={coverageCaptions}
                ariaLabel="Up to date, outdated and missing translations per language"
              />
            )}
            renderTable={() => (
              <CoverageTable items={report.languages} labelCaption="Language" captions={coverageCaptions} />
            )}
          />

          <StatsTile
            headline="Outdated translations"
            description={`Translations behind the ${defaultLanguage} variant, most days behind first. Days behind count from the translation's last change to the default variant's. Click an item to open it.${listedText}`}
            isLoading={isLoading}
            hasError={hasError}
            isEmpty={report.outdated.length === 0}
            emptyMessage={isEmpty ? emptyMessage : 'No translations are behind the default language.'}
            onExportCsv={exportOutdatedCsv}
            defaultView="table"
            renderChart={() => (
              <RankedBarChart
                items={outdatedDays}
                captions={outdatedDaysCaptions}
                ariaLabel="Days behind per outdated translation"
                getHref={getAdminHref}
                showShare={false}
              />
            )}
            renderTable={() => (
              <AgedItemTable items={report.outdated} captions={outdatedCaptions} getAdminHref={getAgedHref} />
            )}
          />

          <StatsTile
            headline="By content type"
            description="Outdated and missing translations per content type, most outdated first. Click a type to open it in the Content types application."
            isLoading={isLoading}
            hasError={hasError}
            isEmpty={report.byContentType.items.length === 0}
            emptyMessage={isEmpty ? emptyMessage : 'All items are translated and up to date.'}
            onExportCsv={exportTypesCsv}
            defaultView="table"
            renderChart={() => (
              <RankedBarChart
                items={report.byContentType.items}
                captions={typeCaptions}
                ariaLabel="Outdated translations per content type"
                getHref={getAdminHref}
                showShare={false}
              />
            )}
            renderTable={() => (
              <RankedTable
                items={report.byContentType.items}
                captions={typeCaptions}
                getAdminHref={getAdminHref}
                showShare={false}
              />
            )}
          />

          <Callout type={CalloutType.QuickTip} placement={CalloutPlacementType.OnDesk} headline="How outdated is measured">
            <p>
              {outdatedHint} Variants saved within {tolerance} of each other (for example by an import or a bulk save) count as
              up to date. Items without a {defaultLanguage} variant are never outdated. Items in all workspaces are counted.
            </p>
          </Callout>
        </>
      )}
    </div>
  );
};
