import {
  Callout,
  CalloutPlacementType,
  CalloutType,
  InfoCard,
} from '@kentico/xperience-admin-components';
import React, { useCallback, useMemo, useState } from 'react';

import { toAdminHref } from '../shared/adminLinks';
import { AgedItemCaptions, AgedItemTable, toDaysRankedItems } from '../shared/AgedItemTable';
import {
  channelsForContentKind,
  contentKindLabel,
  contentKindOptions,
  fitContentChannel,
} from '../shared/contentKinds';
import { usageHint } from '../shared/contentUsage';
import { CoverageBarChart, CoverageCaptions } from '../shared/CoverageBarChart';
import { CoverageTable } from '../shared/CoverageTable';
import {
  toAgedCsv,
  toCoverageCsv,
  toRankedCsv,
  toShareCsv,
} from '../shared/csv';
import { DonutChart } from '../shared/DonutChart';
import { formatShare, numberFormat } from '../shared/format';
import { RankedBarChart } from '../shared/RankedBarChart';
import { RankedTable } from '../shared/RankedTable';
import { ShareTable } from '../shared/ShareTable';
import { SnapshotFilterBar } from '../shared/SnapshotFilterBar';
import { StatsTile } from '../shared/StatsTile';
import {
  StatsAgedItem,
  StatsChannelOption,
  StatsCoverageItem,
  StatsRankedCaptions,
  StatsRankedItem,
  StatsRankedResult,
  StatsShareSlice,
  StatsSnapshotFilter,
} from '../shared/types';
import { useCsvExport } from '../shared/useCsvExport';
import { useStatsCommand } from '../shared/useStatsCommand';
import '../shared/stats.css';

/** Mirrors `ContentAgeSummary`. */
interface ContentAgeSummary {
  /** Fixed order: under 3 months, 3–6, 6–12, over 12 months. */
  readonly buckets: StatsRankedResult;
  readonly notModified12Months: number;
  /** Least recently changed language variants, oldest first. */
  readonly oldest: readonly StatsAgedItem[];
}

/** Mirrors `ContentWorkflowSummary`. */
interface ContentWorkflowSummary {
  readonly inWorkflow: number;
  /** Variants unchanged in a step for more than `overdueDays`. */
  readonly overdue: number;
  readonly overdueDays: number;
  /** Longest unchanged first. `detail` is the step; `adminPath` links to the workflow's steps. */
  readonly items: readonly StatsAgedItem[];
}

/** Mirrors `UnusedReusableSummary`. */
interface UnusedReusableSummary {
  readonly count: number;
  readonly reusableItems: number;
  readonly byContentType: StatsRankedResult;
  /** Least recently changed first. */
  readonly items: readonly StatsAgedItem[];
}

/** Mirrors `ContentInventoryResult`. */
interface ContentInventoryResult {
  /** Applied content type type filter (`Website`, `Reusable`, `Email`, `Headless`), `null` for all. */
  readonly kind: string | null;
  readonly channelId: number | null;
  readonly totalItems: number;
  /** Items per content type type. `secondaryValue` is the number of content types. */
  readonly byKind: StatsRankedResult;
  /** Every content type of the kind, including types with no items (listed last). `secondaryLabel` is the kind. */
  readonly byContentType: StatsRankedResult;
  /** Language variants per status, in a fixed order (keys `published`, `draft`, `workflow`, `unpublished`, `other`). */
  readonly byStatus: StatsRankedResult;
  /** Default language first. */
  readonly languageCoverage: readonly StatsCoverageItem[];
  readonly age: ContentAgeSummary;
  readonly workflow: ContentWorkflowSummary;
  /** `null` when the filters exclude reusable items. */
  readonly unusedReusable: UnusedReusableSummary | null;
  readonly totalVariants: number;
  readonly scheduledPublish: number;
  readonly scheduledUnpublish: number;
  readonly contentTypeCount: number;
  readonly contentTypesInUse: number;
  readonly defaultLanguage: string | null;
  /** ISO timestamp of when the data was read from the database. */
  readonly updatedAt: string;
}

/** Mirrors `ContentInventoryClientProperties`. */
interface ContentInventoryTemplateProps {
  readonly report: ContentInventoryResult;
  /** Website, email and headless channels. */
  readonly channels: readonly StatsChannelOption[];
  /** Path of this page relative to the admin root, used to build admin links. */
  readonly pagePath: string | null;
}

const contentTypeCaptions: StatsRankedCaptions = {
  label: 'Content type',
  secondaryLabel: 'Used for',
  value: 'Items',
};

const ageCaptions: StatsRankedCaptions = { label: 'Last modified', value: 'Language variants' };

const unusedTypeCaptions: StatsRankedCaptions = { label: 'Content type', value: 'Unused items' };

const daysCaptions: StatsRankedCaptions = {
  label: 'Item',
  secondaryLabel: 'Content type',
  value: 'Days since last change',
};

const workflowDaysCaptions: StatsRankedCaptions = {
  label: 'Item',
  secondaryLabel: 'Workflow step',
  value: 'Days since last change',
};

const oldestCaptions: AgedItemCaptions = {
  label: 'Item',
  category: 'Content type',
  language: 'Language',
  since: 'Last modified',
  days: 'Days',
};

const workflowCaptions: AgedItemCaptions = { ...oldestCaptions, detail: 'Workflow step' };

const unusedCaptions: AgedItemCaptions = {
  label: 'Item',
  category: 'Content type',
  since: 'Last modified',
  days: 'Days',
};

const coverageCaptions: CoverageCaptions = {
  covered: 'With variant',
  missing: 'Missing',
  totalNoun: 'items',
};

/** Content types shown in the chart. The table lists all. */
const chartLimit = 25;

/** Days after which the oldest content chart highlights a bar. */
const staleDays = 365;

const statusHint =
  'Status of the latest version of each language variant. An item has one variant per language it is translated to.';

const workflowHint =
  'The time an item entered its step is not stored, so days count from the last change of the language variant.';

function toFilter(report: ContentInventoryResult): StatsSnapshotFilter {
  return { kind: report.kind, channelId: report.channelId };
}

function statusValue(report: ContentInventoryResult, key: string): number {
  return report.byStatus.items.find((item) => item.key === key)?.value ?? 0;
}

export const ContentInventoryTemplate = (props: ContentInventoryTemplateProps) => {
  const saveCsv = useCsvExport();
  const { data: report, isLoading, hasError, load } = useStatsCommand<
    ContentInventoryResult,
    StatsSnapshotFilter
  >(props.report);
  const [filter, setFilter] = useState<StatsSnapshotFilter>(() => toFilter(props.report));

  const channels = useMemo(
    () => channelsForContentKind(props.channels, filter.kind),
    [props.channels, filter.kind],
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

  // Kinds are shown with the names the admin uses ("Pages" for Website).
  const contentTypes = useMemo<StatsRankedItem[]>(
    () =>
      report.byContentType.items.map((item) => ({
        ...item,
        secondaryLabel: item.secondaryLabel ? contentKindLabel(item.secondaryLabel) : null,
      })),
    [report.byContentType.items],
  );
  const chartContentTypes = useMemo(
    () => contentTypes.filter((item) => item.value > 0).slice(0, chartLimit),
    [contentTypes],
  );

  const statusSlices = useMemo<StatsShareSlice[]>(
    () => report.byStatus.items.map((item) => ({ key: item.key, name: item.label, value: item.value })),
    [report.byStatus.items],
  );
  const ageSlices = useMemo<StatsShareSlice[]>(
    () => report.age.buckets.items.map((item) => ({ key: item.key, name: item.label, value: item.value })),
    [report.age.buckets.items],
  );

  const { workflow, age, unusedReusable: unused } = report;
  const oldestDays = useMemo(() => toDaysRankedItems(age.oldest), [age.oldest]);
  const workflowDays = useMemo(
    () => toDaysRankedItems(workflow.items, (item) => item.detail),
    [workflow.items],
  );

  const published = statusValue(report, 'published');
  const publishedShare = report.totalVariants > 0 ? published / report.totalVariants : 0;

  const kindText = report.byKind.items
    .map((item) => `${numberFormat.format(item.value)} ${contentKindLabel(item.key).toLowerCase()}`)
    .join(' · ');

  const scheduledText = [
    report.scheduledPublish > 0 ? `${numberFormat.format(report.scheduledPublish)} scheduled to publish` : null,
    report.scheduledUnpublish > 0 ? `${numberFormat.format(report.scheduledUnpublish)} scheduled to unpublish` : null,
  ]
    .filter(Boolean)
    .join(', ');

  const listedText = (shown: number, total: number) =>
    total > shown ? `The ${numberFormat.format(shown)} oldest of ${numberFormat.format(total)} are listed.` : '';

  const fileSuffix = `${(report.kind ?? 'all').toLowerCase()}${report.channelId ? `_channel-${report.channelId}` : ''}`;

  const exportContentTypesCsv = () => {
    saveCsv(
      'content-inventory-types',
      `content-inventory-types_${fileSuffix}.csv`,
      toRankedCsv(contentTypes, contentTypeCaptions, getAdminHref),
    );
  };

  const exportStatusCsv = () => {
    saveCsv(
      'content-inventory-status',
      `content-inventory-status_${fileSuffix}.csv`,
      toShareCsv(statusSlices, 'Status', 'Language variants'),
    );
  };

  const exportAgeCsv = () => {
    saveCsv(
      'content-inventory-age',
      `content-inventory-age_${fileSuffix}.csv`,
      toShareCsv(ageSlices, ageCaptions.label, ageCaptions.value),
    );
  };

  const exportOldestCsv = () => {
    saveCsv(
      'content-inventory-oldest',
      `content-inventory-oldest_${fileSuffix}.csv`,
      toAgedCsv(age.oldest, oldestCaptions, getAgedHref),
    );
  };

  const exportWorkflowCsv = () => {
    saveCsv(
      'content-inventory-workflow',
      `content-inventory-workflow_${fileSuffix}.csv`,
      toAgedCsv(workflow.items, workflowCaptions, getAgedHref),
    );
  };

  const exportUnusedCsv = () => {
    if (unused) {
      saveCsv('content-inventory-unused-reusable', `content-inventory-unused-reusable.csv`, toAgedCsv(unused.items, unusedCaptions, getAgedHref));
    }
  };

  const exportCoverageCsv = () => {
    saveCsv(
      'content-inventory-languages',
      `content-inventory-languages_${fileSuffix}.csv`,
      toCoverageCsv(report.languageCoverage, {
        label: 'Language',
        secondaryLabel: 'Code name',
        covered: coverageCaptions.covered,
        missing: coverageCaptions.missing,
      }),
    );
  };

  const isEmpty = report.totalItems === 0;
  const emptyMessage = 'No content items match the selected filters.';

  return (
    <div className="SimpleStats-root">
      <SnapshotFilterBar
        filter={filter}
        onChange={handleFilterChange}
        kinds={contentKindOptions}
        channels={channels}
        onRefresh={handleRefresh}
        isLoading={isLoading}
        updatedAt={report.updatedAt}
      />

      <div className="SimpleStats-kpis">
        <InfoCard
          caption="Content items"
          tooltip="Content items that match the filters. Page folders are not counted. Each item counts once, whatever the number of languages."
          text={numberFormat.format(report.totalItems)}
          details={kindText || contentKindLabel(report.kind)}
        />
        <InfoCard
          caption="Content types in use"
          tooltip="Content types with at least one item that matches the filters. Unused types are listed last in Items by content type."
          text={numberFormat.format(report.contentTypesInUse)}
          details={`Of ${numberFormat.format(report.contentTypeCount)} content types`}
        />
        <InfoCard
          caption="Languages"
          tooltip="Content languages. See Language coverage for items missing a translation."
          text={numberFormat.format(report.languageCoverage.length)}
          details={report.defaultLanguage ? `Default: ${report.defaultLanguage}` : 'No default language'}
        />
        <InfoCard
          caption="Action needed"
          tooltip={`Language variants in a workflow step unchanged for more than ${workflow.overdueDays} days. ${workflowHint}`}
          text={numberFormat.format(workflow.overdue)}
          details={`${numberFormat.format(workflow.inWorkflow)} in workflow`}
        />
        <InfoCard
          caption="Not modified in 12 months"
          tooltip="Language variants whose latest version has not changed in the last 12 months."
          text={numberFormat.format(age.notModified12Months)}
          details={`Of ${numberFormat.format(report.totalVariants)} language variants`}
        />
        {unused && (
          <InfoCard
            caption="Unused reusable items"
            tooltip={usageHint}
            text={numberFormat.format(unused.count)}
            details={`Of ${numberFormat.format(unused.reusableItems)} reusable items`}
          />
        )}
      </div>

      {workflow.inWorkflow > 0 && (
        <StatsTile
          headline="Action needed: items in workflow steps"
          description={`Language variants waiting in a workflow step, longest unchanged first. Bars over ${workflow.overdueDays} days are highlighted. ${workflowHint} Click an item to open it (or its workflow's steps when the item cannot be linked). ${listedText(workflow.items.length, workflow.inWorkflow)}`}
          isLoading={isLoading}
          hasError={hasError}
          onExportCsv={exportWorkflowCsv}
          renderChart={() => (
            <RankedBarChart
              items={workflowDays}
              captions={workflowDaysCaptions}
              ariaLabel="Days since last change of items in workflow steps"
              getHref={getAdminHref}
              showShare={false}
              highlightFrom={workflow.overdueDays + 1}
            />
          )}
          renderTable={() => (
            <AgedItemTable items={workflow.items} captions={workflowCaptions} getAdminHref={getAgedHref} />
          )}
        />
      )}

      <div className="SimpleStats-tiles">
        <StatsTile
          headline="Oldest content"
          description={`Language variants with the oldest last change, oldest first. Bars over 12 months are highlighted. Click an item to open it. ${listedText(age.oldest.length, report.totalVariants)}`}
          isLoading={isLoading}
          hasError={hasError}
          isEmpty={age.oldest.length === 0}
          emptyMessage={emptyMessage}
          onExportCsv={exportOldestCsv}
          renderChart={() => (
            <RankedBarChart
              items={oldestDays}
              captions={daysCaptions}
              ariaLabel="Days since last change of the oldest content"
              getHref={getAdminHref}
              showShare={false}
              highlightFrom={staleDays}
            />
          )}
          renderTable={() => (
            <AgedItemTable items={age.oldest} captions={oldestCaptions} getAdminHref={getAgedHref} />
          )}
        />

        {/* Charts with a predictable, limited size, stacked beside the longer list. */}
        <div className="SimpleStats-tileColumn">
          <StatsTile
            headline="Content age"
            description="Language variants by time since the last change of their latest version."
            isLoading={isLoading}
            hasError={hasError}
            isEmpty={report.totalVariants === 0}
            emptyMessage={emptyMessage}
            onExportCsv={exportAgeCsv}
            renderChart={() => (
              <RankedBarChart
                items={report.age.buckets.items}
                captions={ageCaptions}
                ariaLabel="Language variants by time since last change"
              />
            )}
            renderTable={() => (
              <ShareTable slices={ageSlices} labelCaption={ageCaptions.label} valueCaption={ageCaptions.value} />
            )}
          />

          <StatsTile
            headline="Status"
            description={scheduledText ? `${statusHint} ${scheduledText}.` : statusHint}
            isLoading={isLoading}
            hasError={hasError}
            isEmpty={report.totalVariants === 0}
            emptyMessage={emptyMessage}
            onExportCsv={exportStatusCsv}
            renderChart={() => (
              <DonutChart
                slices={statusSlices}
                centerValue={formatShare(publishedShare)}
                centerCaption="published"
                ariaLabel="Language variants by status"
              />
            )}
            renderTable={() => (
              <ShareTable slices={statusSlices} labelCaption="Status" valueCaption="Language variants" />
            )}
          />
        </div>
      </div>

      {unused && (
        <StatsTile
          headline="Unused reusable items"
          description={`The chart shows unused items per content type (click a bar to open the type); the table lists the items, least recently changed first (click an item to open it in the Content hub). ${listedText(unused.items.length, unused.count)} ${usageHint}`}
          isLoading={isLoading}
          hasError={hasError}
          isEmpty={unused.count === 0}
          emptyMessage="Every reusable item is referenced by another content item."
          onExportCsv={exportUnusedCsv}
          renderChart={() => (
            <RankedBarChart
              items={unused.byContentType.items}
              captions={unusedTypeCaptions}
              ariaLabel="Unused reusable items per content type"
              getHref={getAdminHref}
            />
          )}
          renderTable={() => (
            <AgedItemTable items={unused.items} captions={unusedCaptions} getAdminHref={getAgedHref} />
          )}
        />
      )}

      <StatsTile
        headline="Items by content type"
        description={`Every content type, most used first, including types with no items. The chart shows up to ${chartLimit} types with items. Click a type's graph bar or name in the table to open it.`}
        isLoading={isLoading}
        hasError={hasError}
        isEmpty={report.contentTypeCount === 0}
        emptyMessage="There are no content types of this kind."
        onExportCsv={exportContentTypesCsv}
        renderChart={() =>
          chartContentTypes.length > 0 ? (
            <RankedBarChart
              items={chartContentTypes}
              captions={contentTypeCaptions}
              ariaLabel="Content items by content type"
              getHref={getAdminHref}
            />
          ) : (
            <div className="SimpleStats-empty">{emptyMessage}</div>
          )
        }
        renderTable={() => (
          <RankedTable items={contentTypes} captions={contentTypeCaptions} getAdminHref={getAdminHref} />
        )}
      />

      <StatsTile
        headline="Language coverage"
        description="Items with a variant in each language, out of all items that match the filters. Missing items have no translation in that language yet."
        isLoading={isLoading}
        hasError={hasError}
        isEmpty={isEmpty || report.languageCoverage.length === 0}
        emptyMessage={emptyMessage}
        onExportCsv={exportCoverageCsv}
        renderChart={() => (
          <CoverageBarChart
            items={report.languageCoverage}
            captions={coverageCaptions}
            ariaLabel="Content items with a variant per language"
          />
        )}
        renderTable={() => (
          <CoverageTable items={report.languageCoverage} labelCaption="Language" captions={coverageCaptions} />
        )}
      />

      <Callout
        type={CalloutType.QuickTip}
        placement={CalloutPlacementType.OnDesk}
        headline="Current state"
      >
        <p>
          These numbers show content as it is now (the latest version of each language variant), not
          trends over time. Items in all workspaces are counted.
        </p>
      </Callout>
    </div>
  );
};
