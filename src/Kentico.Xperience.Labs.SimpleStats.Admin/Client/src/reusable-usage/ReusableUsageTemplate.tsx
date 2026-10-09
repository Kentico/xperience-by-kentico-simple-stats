import {
  ButtonColor,
  Callout,
  CalloutPlacementType,
  CalloutType,
  InfoCard,
  LinkButton,
} from '@kentico/xperience-admin-components';
import React, { useCallback, useMemo, useState } from 'react';

import { toAdminHref } from '../shared/adminLinks';
import { usageHint } from '../shared/contentUsage';
import { toRankedCsv, toShareCsv } from '../shared/csv';
import { formatShare, numberFormat } from '../shared/format';
import { RankedBarChart } from '../shared/RankedBarChart';
import { RankedTable } from '../shared/RankedTable';
import { ShareTable } from '../shared/ShareTable';
import { SnapshotFilterBar } from '../shared/SnapshotFilterBar';
import { StackedColumnChart } from '../shared/StackedColumnChart';
import { StatsTile } from '../shared/StatsTile';
import {
  StatsPeriod,
  StatsRankedCaptions,
  StatsRankedItem,
  StatsRankedResult,
  StatsSeries,
  StatsShareSlice,
  StatsSnapshotFilter,
} from '../shared/types';
import { useCsvExport } from '../shared/useCsvExport';
import { useStatsCommand } from '../shared/useStatsCommand';
import {
  ReusableUsageItem,
  ReusableUsageTable,
  toReusableUsageCsv,
  toUsageRankedItems,
  usageCaptions,
} from './ReusableUsageTable';
import '../shared/stats.css';

/** Mirrors `ReusableUsageContentTypeOption`. */
interface ReusableUsageContentTypeOption {
  readonly id: number;
  readonly displayName: string;
  readonly itemCount: number;
}

/** Mirrors `ReusableUsageResult`. A usage is one content item that references the item. */
interface ReusableUsageResult {
  /** Applied content type filter (class ID), `null` for all. */
  readonly contentTypeId: number | null;
  readonly reusableItems: number;
  readonly usedItems: number;
  /** `usedItems` / `reusableItems` (0–1). */
  readonly usedShare: number;
  readonly usedOnce: number;
  readonly unused: number;
  /** Usages of all items. */
  readonly usages: number;
  /** Most used items, most usages first. */
  readonly mostUsed: readonly ReusableUsageItem[];
  /** Items per number of usages (fixed order, 0 included). */
  readonly distribution: StatsRankedResult;
  /** Usages per content type. `secondaryValue` is the type's items, `tertiaryValue` its used items. */
  readonly byContentType: StatsRankedResult;
  /** Reusable content types with items. */
  readonly contentTypeOptions: readonly ReusableUsageContentTypeOption[];
  /** ISO timestamp of when the data was read from the database. */
  readonly updatedAt: string;
}

/** Mirrors `ReusableUsageClientProperties`. */
interface ReusableUsageTemplateProps {
  readonly report: ReusableUsageResult;
  /** Path of this page relative to the admin root, used to build admin links. */
  readonly pagePath: string | null;
  /** Content inventory report (lists the unused items), `null` without its permission. */
  readonly contentInventoryPath: string | null;
}

const mostUsedCaptions: StatsRankedCaptions = {
  label: usageCaptions.name,
  secondaryLabel: usageCaptions.contentType,
  value: usageCaptions.usages,
};

const typeCaptions: StatsRankedCaptions = {
  label: 'Content type',
  value: 'Usages',
  secondaryValue: 'Items',
  tertiaryValue: 'Used items',
};

const distributionCaptions = { label: 'Usages', value: 'Reusable items' } as const;

const usedInHint = 'Open an item and its Used in tab to see where it is used.';

const deliveredHint = 'Changing a reusable item does not change emails that were already sent.';

function toFilter(report: ReusableUsageResult): StatsSnapshotFilter {
  return { kind: null, channelId: null, contentTypeId: report.contentTypeId };
}

export const ReusableUsageTemplate = (props: ReusableUsageTemplateProps) => {
  const saveCsv = useCsvExport();
  const { data: report, isLoading, hasError, load } = useStatsCommand<ReusableUsageResult, StatsSnapshotFilter>(
    props.report,
  );
  const [filter, setFilter] = useState<StatsSnapshotFilter>(() => toFilter(props.report));

  const handleFilterChange = (next: StatsSnapshotFilter) => {
    setFilter(next);
    void load(next);
  };

  const handleRefresh = () => {
    void load(filter, { refresh: true });
  };

  const { pagePath } = props;
  const getItemHref = useCallback((item: ReusableUsageItem) => toAdminHref(item.adminPath, pagePath), [pagePath]);
  const getRankedHref = useCallback((item: StatsRankedItem) => toAdminHref(item.adminPath, pagePath), [pagePath]);
  const inventoryHref = toAdminHref(props.contentInventoryPath, pagePath);

  const mostUsedItems = useMemo(() => toUsageRankedItems(report.mostUsed), [report.mostUsed]);

  const distributionPeriods = useMemo<StatsPeriod[]>(
    () => report.distribution.items.map((item) => ({ start: item.key, label: item.label })),
    [report.distribution.items],
  );
  const distributionSeries = useMemo<StatsSeries[]>(
    () => [
      {
        key: 'items',
        name: distributionCaptions.value,
        values: report.distribution.items.map((item) => item.value),
      },
    ],
    [report.distribution.items],
  );
  const distributionSlices = useMemo<StatsShareSlice[]>(
    () =>
      report.distribution.items.map((item) => ({
        key: item.key,
        name: item.label,
        value: item.value,
      })),
    [report.distribution.items],
  );

  const typeOptions = useMemo(
    () => report.contentTypeOptions.map((option) => ({ id: option.id, label: option.displayName })),
    [report.contentTypeOptions],
  );

  const fileSuffix = report.contentTypeId ? `_type-${report.contentTypeId}` : '_all';

  const exportItemsCsv = () => {
    saveCsv(
      'reusable-usage-items',
      `reusable-usage-items${fileSuffix}.csv`,
      toReusableUsageCsv(report.mostUsed, getItemHref),
    );
  };

  const exportDistributionCsv = () => {
    saveCsv(
      'reusable-usage-distribution',
      `reusable-usage-distribution${fileSuffix}.csv`,
      toShareCsv(distributionSlices, distributionCaptions.label, distributionCaptions.value),
    );
  };

  const exportTypesCsv = () => {
    saveCsv(
      'reusable-usage-types',
      `reusable-usage-types${fileSuffix}.csv`,
      toRankedCsv(report.byContentType.items, typeCaptions, getRankedHref),
    );
  };

  const listedText =
    report.usedItems > report.mostUsed.length
      ? ` The ${numberFormat.format(report.mostUsed.length)} most used of ${numberFormat.format(report.usedItems)} are listed.`
      : '';

  const noItems = report.reusableItems === 0;
  const noItemsMessage = 'No reusable items match the selected content type.';

  return (
    <div className="SimpleStats-root">
      <SnapshotFilterBar
        filter={filter}
        onChange={handleFilterChange}
        select={{
          label: 'Content type',
          allLabel: 'All content types',
          options: typeOptions,
          value: filter.contentTypeId ?? null,
          onChange: (contentTypeId) => handleFilterChange({ ...filter, contentTypeId }),
        }}
        actions={
          inventoryHref && (
            <LinkButton
              label="Open Content inventory"
              color={ButtonColor.Tertiary}
              href={inventoryHref}
              title="Content inventory lists the unused reusable items"
            />
          )
        }
        onRefresh={handleRefresh}
        isLoading={isLoading}
        updatedAt={report.updatedAt}
      />

      <div className="SimpleStats-kpis">
        <InfoCard
          caption="Reusable items"
          tooltip="Reusable items in the Content hub of the selected content type, in all workspaces."
          text={numberFormat.format(report.reusableItems)}
          details={`${numberFormat.format(report.usages)} usages`}
        />
        <InfoCard
          caption="Used items"
          tooltip={`Reusable items referenced by at least one content item. ${usageHint}`}
          text={numberFormat.format(report.usedItems)}
          details={`${formatShare(report.usedShare)} of reusable items`}
        />
        <InfoCard
          caption="Used once"
          tooltip="Items referenced by exactly one content item. They may not need to be reusable."
          text={numberFormat.format(report.usedOnce)}
          details="Exactly 1 usage"
        />
        <InfoCard
          caption="Unused"
          tooltip="Items no content item references. Content inventory lists them (Unused reusable items)."
          text={numberFormat.format(report.unused)}
          details="Listed in Content inventory"
        />
      </div>

      <StatsTile
        headline="Most used items"
        description={`Reusable items referenced by the most content items: changes to them show in all of these. Click an item to open it in the Content hub.${listedText}`}
        isLoading={isLoading}
        hasError={hasError}
        isEmpty={report.mostUsed.length === 0}
        emptyMessage={noItems ? noItemsMessage : 'No reusable item is referenced by another content item.'}
        onExportCsv={exportItemsCsv}
        renderChart={() => (
          <RankedBarChart
            items={mostUsedItems}
            captions={mostUsedCaptions}
            ariaLabel="Usages per reusable item"
            getHref={getRankedHref}
            showShare={false}
          />
        )}
        renderTable={() => <ReusableUsageTable items={report.mostUsed} getAdminHref={getItemHref} />}
      />

      <div className="SimpleStats-tiles SimpleStats-tiles--halves">
        <StatsTile
          headline="Usage distribution"
          description="Reusable items by the number of content items that reference them."
          isLoading={isLoading}
          hasError={hasError}
          isEmpty={noItems}
          emptyMessage={noItemsMessage}
          onExportCsv={exportDistributionCsv}
          renderChart={() => (
            <StackedColumnChart
              periods={distributionPeriods}
              series={distributionSeries}
              ariaLabel="Reusable items by number of usages"
            />
          )}
          renderTable={() => (
            <ShareTable
              slices={distributionSlices}
              labelCaption={distributionCaptions.label}
              valueCaption={distributionCaptions.value}
            />
          )}
        />

        <StatsTile
          headline="By content type"
          description="Usages, items and used items per reusable content type, most usages first. Click a type to open it."
          isLoading={isLoading}
          hasError={hasError}
          isEmpty={noItems}
          emptyMessage={noItemsMessage}
          onExportCsv={exportTypesCsv}
          renderTable={() => (
            <RankedTable items={report.byContentType.items} captions={typeCaptions} getAdminHref={getRankedHref} />
          )}
        />
      </div>

      <Callout type={CalloutType.QuickTip} placement={CalloutPlacementType.OnDesk} headline="What counts as a usage">
        <p>
          {usageHint} A usage is one referencing content item, whatever its number of languages, versions and references.{' '}
          {usedInHint} {deliveredHint}
        </p>
      </Callout>
    </div>
  );
};
