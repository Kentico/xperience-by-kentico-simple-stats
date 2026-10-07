import { InfoCard } from '@kentico/xperience-admin-components';
import React, { useCallback, useMemo, useState } from 'react';

import { toAdminHref } from '../shared/adminLinks';
import { ComboChart } from '../shared/ComboChart';
import { toShareCsv } from '../shared/csv';
import { DataRetentionNote } from '../shared/DataRetentionNote';
import { formatShare, numberFormat } from '../shared/format';
import { RankedBarChart } from '../shared/RankedBarChart';
import { ShareTable } from '../shared/ShareTable';
import { StatsFilterBar } from '../shared/StatsFilterBar';
import { StatsTile } from '../shared/StatsTile';
import {
  StatsChannelOption,
  StatsFilter,
  StatsPeriod,
  StatsRankedCaptions,
  StatsRankedItem,
  StatsRankedResult,
  StatsSeries,
  StatsShareSlice,
} from '../shared/types';
import { useCsvExport } from '../shared/useCsvExport';
import { useStatsCommand } from '../shared/useStatsCommand';
import { PageFreshnessItem, PageFreshnessTable, toPageFreshnessCsv, toVisitRankedItems } from './PageFreshnessTable';
import '../shared/stats.css';

/** Mirrors `PageFreshnessResult`. Counts are per page language variant. */
interface PageFreshnessResult {
  readonly from: string;
  readonly to: string;
  /** Applied website channel filter, `null` for all. */
  readonly channelId: number | null;
  /** Published pages with a URL (folders left out). */
  readonly publishedPages: number;
  /** Published pages not changed in `staleMonths` months. */
  readonly stalePages: number;
  /** `stalePages` / `publishedPages` (0–1). */
  readonly staleShare: number;
  /** Page visits of the published pages in the range. */
  readonly visits: number;
  readonly staleVisits: number;
  /** `staleVisits` / `visits` (0–1). */
  readonly staleVisitShare: number;
  /** Stale pages with at least one visit (all, not only `stalePopular`). */
  readonly stalePopularPages: number;
  /** Pages first published before the range with no visit in it (all, not only `noVisits`). */
  readonly noVisitPages: number;
  readonly staleMonths: number;
  /** Published pages per time since the last change (fixed order). `secondaryValue` is the bucket's visits. */
  readonly ageBuckets: StatsRankedResult;
  /** Stale pages with visits, most visits first. */
  readonly stalePopular: readonly PageFreshnessItem[];
  /** Pages without visits, first published longest ago first. */
  readonly noVisits: readonly PageFreshnessItem[];
  /** ISO timestamp of when the data was read from the database. */
  readonly updatedAt: string;
}

/** Mirrors `PageFreshnessClientProperties`. */
interface PageFreshnessTemplateProps {
  readonly report: PageFreshnessResult;
  /** Website channels. */
  readonly channels: readonly StatsChannelOption[];
  readonly today: string;
  /** Path of this page relative to the admin root, used to build admin links. */
  readonly pagePath: string | null;
}

const stalePopularCaptions: StatsRankedCaptions = {
  label: 'Page',
  secondaryLabel: 'Language',
  value: 'Visits',
  secondaryValue: 'Visitors',
};

const ageCaptions = { label: 'Last modified', pages: 'Published pages', visits: 'Visits' } as const;

const variantHint = 'Counts are per language variant: a page in two languages counts twice.';

const trackingHint =
  'Visits need activity tracking and cookie consent of the visitor, so a page with no visits may only be missing tracking.';

function toFilter(report: PageFreshnessResult): StatsFilter {
  // This report ignores grouping; the filter type is shared with trend reports.
  return { from: report.from, to: report.to, grouping: 'Day', channelId: report.channelId };
}

export const PageFreshnessTemplate = (props: PageFreshnessTemplateProps) => {
  const saveCsv = useCsvExport();
  const { data: report, isLoading, hasError, load } = useStatsCommand<PageFreshnessResult>(props.report);
  const [filter, setFilter] = useState<StatsFilter>(() => toFilter(props.report));

  const handleFilterChange = (next: StatsFilter) => {
    setFilter(next);
    void load(next);
  };

  const handleRefresh = () => {
    void load(filter, { refresh: true });
  };

  const { pagePath } = props;
  const getPageHref = useCallback((item: PageFreshnessItem) => toAdminHref(item.adminPath, pagePath), [pagePath]);
  const getRankedHref = useCallback((item: StatsRankedItem) => toAdminHref(item.adminPath, pagePath), [pagePath]);

  const stalePopularItems = useMemo(() => toVisitRankedItems(report.stalePopular), [report.stalePopular]);

  const agePeriods = useMemo<StatsPeriod[]>(
    () => report.ageBuckets.items.map((item) => ({ start: item.key, label: item.label })),
    [report.ageBuckets.items],
  );
  const ageColumns = useMemo<StatsSeries>(
    () => ({ key: 'pages', name: ageCaptions.pages, values: report.ageBuckets.items.map((item) => item.value) }),
    [report.ageBuckets.items],
  );
  const ageLine = useMemo<StatsSeries>(
    () => ({
      key: 'visits',
      name: ageCaptions.visits,
      values: report.ageBuckets.items.map((item) => item.secondaryValue ?? 0),
    }),
    [report.ageBuckets.items],
  );
  const ageSlices = useMemo<StatsShareSlice[]>(
    () =>
      report.ageBuckets.items.map((item) => ({
        key: item.key,
        name: item.label,
        value: item.value,
        secondaryValue: item.secondaryValue,
      })),
    [report.ageBuckets.items],
  );

  const rangeText = `${report.from} – ${report.to}`;
  const staleText = `not changed in ${numberFormat.format(report.staleMonths)} months`;
  const fileSuffix = `${report.from}_${report.to}${report.channelId ? `_channel-${report.channelId}` : ''}`;

  const listedText = (shown: number, total: number) =>
    total > shown ? ` The first ${numberFormat.format(shown)} of ${numberFormat.format(total)} are listed.` : '';

  const exportStalePopularCsv = () => {
    saveCsv(
      'page-freshness-stale-popular',
      `page-freshness-stale-popular_${fileSuffix}.csv`,
      toPageFreshnessCsv(report.stalePopular, 'visits', getPageHref),
    );
  };

  const exportAgeCsv = () => {
    saveCsv(
      'page-freshness-age',
      `page-freshness-age_${fileSuffix}.csv`,
      toShareCsv(ageSlices, ageCaptions.label, ageCaptions.pages, ageCaptions.visits),
    );
  };

  const exportNoVisitsCsv = () => {
    saveCsv(
      'page-freshness-no-visits',
      `page-freshness-no-visits_${fileSuffix}.csv`,
      toPageFreshnessCsv(report.noVisits, 'firstPublished', getPageHref),
    );
  };

  const noPages = report.publishedPages === 0;
  const noPagesMessage = 'No published pages match the selected channel.';

  return (
    <div className="SimpleStats-root">
      <StatsFilterBar
        filter={filter}
        today={props.today}
        channels={props.channels}
        onChange={handleFilterChange}
        onRefresh={handleRefresh}
        isLoading={isLoading}
        updatedAt={report.updatedAt}
        showGrouping={false}
      />

      <div className="SimpleStats-kpis">
        <InfoCard
          caption="Published pages"
          tooltip={`Published pages with a URL in the selected channel. Page folders and pages without a URL are not counted. ${variantHint}`}
          text={numberFormat.format(report.publishedPages)}
          details="Current state"
        />
        <InfoCard
          caption="Stale pages"
          tooltip={`Published pages ${staleText} (same threshold as Content inventory).`}
          text={numberFormat.format(report.stalePages)}
          details={`${formatShare(report.staleShare)} of published pages`}
        />
        <InfoCard
          caption="Visits to stale pages"
          tooltip={`Share of the page visits in the selected range that went to pages ${staleText}.`}
          text={formatShare(report.staleVisitShare)}
          details={`${numberFormat.format(report.staleVisits)} of ${numberFormat.format(report.visits)} visits`}
        />
        <InfoCard
          caption="Pages with no visits"
          tooltip={`Published pages first published before the range start with no page visit in the range. ${trackingHint}`}
          text={numberFormat.format(report.noVisitPages)}
          details={rangeText}
        />
      </div>

      <div className="SimpleStats-tiles SimpleStats-tiles--halves">
        <StatsTile
          headline="Stale but popular"
          description={`Published pages ${staleText} that were visited in the selected range, most visits first. Refresh these first. Click a page to open it.${listedText(report.stalePopular.length, report.stalePopularPages)}`}
          isLoading={isLoading}
          hasError={hasError}
          isEmpty={report.stalePopular.length === 0}
          emptyMessage={
            noPages
              ? noPagesMessage
              : `No visited page in the selected range is ${staleText}.`
          }
          onExportCsv={exportStalePopularCsv}
          renderChart={() => (
            <RankedBarChart
              items={stalePopularItems}
              captions={stalePopularCaptions}
              ariaLabel={`Visits of stale pages, ${rangeText}`}
              getHref={getRankedHref}
              showShare={false}
            />
          )}
          renderTable={() => <PageFreshnessTable items={report.stalePopular} columns="visits" getAdminHref={getPageHref} />}
        />

        <StatsTile
          headline="Visits by page age"
          description="Published pages by time since their last change (columns) and their visits in the selected range (line)."
          isLoading={isLoading}
          hasError={hasError}
          isEmpty={noPages}
          emptyMessage={noPagesMessage}
          onExportCsv={exportAgeCsv}
          renderChart={() => (
            <ComboChart
              periods={agePeriods}
              columns={ageColumns}
              line={ageLine}
              ariaLabel={`Published pages and visits by time since the last change, ${rangeText}`}
            />
          )}
          renderTable={() => (
            <ShareTable
              slices={ageSlices}
              labelCaption={ageCaptions.label}
              valueCaption={ageCaptions.pages}
              secondaryValueCaption={ageCaptions.visits}
            />
          )}
        />
      </div>

      <StatsTile
        headline="Published pages with no visits"
        description={`Published pages first published before ${report.from} that were not visited in the selected range, first published longest ago first. Pages published later are left out, so new pages get a fair chance.${listedText(report.noVisits.length, report.noVisitPages)}`}
        isLoading={isLoading}
        hasError={hasError}
        isEmpty={report.noVisits.length === 0}
        emptyMessage={noPages ? noPagesMessage : 'Every published page was visited in the selected range.'}
        onExportCsv={exportNoVisitsCsv}
        renderTable={() => <PageFreshnessTable items={report.noVisits} columns="firstPublished" getAdminHref={getPageHref} />}
      />

      <DataRetentionNote>
        {trackingHint} {variantHint} The last change is the latest version of the page, so a newer draft counts as a change.
      </DataRetentionNote>
    </div>
  );
};
