import React, { useMemo, useState } from 'react';

import { ComparisonInfoCard } from '../shared/ComparisonInfoCard';
import { toShareCsv, toTimeSeriesCsv } from '../shared/csv';
import { DataRetentionNote } from '../shared/DataRetentionNote';
import { DonutChart } from '../shared/DonutChart';
import { formatShare } from '../shared/format';
import { ShareTable } from '../shared/ShareTable';
import { StackedColumnChart } from '../shared/StackedColumnChart';
import { StatsFilterBar } from '../shared/StatsFilterBar';
import { StatsTile } from '../shared/StatsTile';
import { toStatsSeries } from '../shared/timeSeries';
import { TimeSeriesTable } from '../shared/TimeSeriesTable';
import {
  StatsComparison,
  StatsFilter,
  StatsSeries,
  StatsShareSlice,
  StatsTimeSeriesResult,
} from '../shared/types';
import { useCsvExport } from '../shared/useCsvExport';
import { useStatsCommand } from '../shared/useStatsCommand';
import '../shared/stats.css';

/** Mirrors `NewContactsResult`. */
interface NewContactsResult {
  /** Series `identified` and `anonymous`, always in this order. */
  readonly trend: StatsTimeSeriesResult;
  readonly identified: number;
  readonly anonymous: number;
  /** Share of identified contacts (0–1). */
  readonly identifiedShare: number;
  /** New contacts vs the previous period of the same length. */
  readonly totalComparison: StatsComparison;
  readonly identifiedComparison: StatsComparison;
  readonly anonymousComparison: StatsComparison;
}

/** Mirrors `NewContactsClientProperties`. */
interface NewContactsTemplateProps {
  readonly report: NewContactsResult;
  readonly today: string;
}

const identifiedHint = 'Identified contacts have an email address. Anonymous contacts do not.';

function toFilter(report: NewContactsResult): StatsFilter {
  // Contacts have no channel; the filter type is shared with channel reports.
  return {
    from: report.trend.from,
    to: report.trend.to,
    grouping: report.trend.grouping,
    channelId: null,
  };
}

export const NewContactsTemplate = (props: NewContactsTemplateProps) => {
  const saveCsv = useCsvExport();
  const { data: report, isLoading, hasError, load } =
    useStatsCommand<NewContactsResult>(props.report);
  const [filter, setFilter] = useState<StatsFilter>(() => toFilter(props.report));

  const handleFilterChange = (next: StatsFilter) => {
    setFilter(next);
    void load(next);
  };

  const handleRefresh = () => {
    void load(filter, { refresh: true });
  };

  const { trend } = report;
  const total = trend.total;
  const isEmpty = total === 0;
  const rangeText = `${trend.from} – ${trend.to}`;
  const anonymousShare = total > 0 ? report.anonymous / total : 0;

  const series = useMemo<StatsSeries[]>(() => toStatsSeries(trend.series), [trend.series]);

  const slices = useMemo<StatsShareSlice[]>(
    () => trend.series.map((s) => ({ key: s.key, name: s.displayName, value: s.total })),
    [trend.series],
  );

  const exportTrendCsv = () => {
    saveCsv(
      'new-contacts',
      `new-contacts_${trend.from}_${trend.to}_${trend.grouping.toLowerCase()}.csv`,
      toTimeSeriesCsv(trend.periods, series),
    );
  };

  const exportShareCsv = () => {
    saveCsv(
      'new-contacts-share',
      `new-contacts-share_${trend.from}_${trend.to}.csv`,
      toShareCsv(slices, 'Segment', 'Contacts'),
    );
  };

  const emptyMessage = 'No contacts were created in the selected range. Try a longer range.';

  return (
    <div className="SimpleStats-root">
      <StatsFilterBar
        filter={filter}
        today={props.today}
        onChange={handleFilterChange}
        onRefresh={handleRefresh}
        isLoading={isLoading}
        updatedAt={trend.updatedAt}
        showChannel={false}
      />

      <div className="SimpleStats-kpis">
        <ComparisonInfoCard
          caption="New contacts"
          tooltip={`Contacts created in the selected range (${rangeText}) that still exist.`}
          comparison={report.totalComparison}
          noun="new contacts"
        />
        <ComparisonInfoCard
          caption="Identified"
          tooltip={`${identifiedHint} ${isEmpty ? 'No new contacts.' : `${formatShare(report.identifiedShare)} of new contacts.`}`}
          comparison={report.identifiedComparison}
          noun="identified contacts"
        />
        <ComparisonInfoCard
          caption="Anonymous"
          tooltip={`${identifiedHint} ${isEmpty ? 'No new contacts.' : `${formatShare(anonymousShare)} of new contacts.`}`}
          comparison={report.anonymousComparison}
          noun="anonymous contacts"
        />
      </div>

      <div className="SimpleStats-tiles">
        <StatsTile
          headline="New contacts over time"
          description={`Contacts created per ${trend.grouping.toLowerCase()}, stacked by identified and anonymous.`}
          isLoading={isLoading}
          hasError={hasError}
          isEmpty={isEmpty}
          emptyMessage={emptyMessage}
          onExportCsv={exportTrendCsv}
          renderChart={() => (
            <StackedColumnChart
              periods={trend.periods}
              series={series}
              ariaLabel={`New contacts over time, ${rangeText}`}
            />
          )}
          renderTable={() => (
            <TimeSeriesTable grouping={trend.grouping} periods={trend.periods} series={series} />
          )}
        />

        <StatsTile
          headline="Identified vs anonymous"
          description={identifiedHint}
          isLoading={isLoading}
          hasError={hasError}
          isEmpty={isEmpty}
          emptyMessage={emptyMessage}
          onExportCsv={exportShareCsv}
          renderChart={() => (
            <DonutChart
              slices={slices}
              centerValue={formatShare(report.identifiedShare)}
              centerCaption="identified"
              ariaLabel={`Identified vs anonymous new contacts, ${rangeText}`}
            />
          )}
          renderTable={() => (
            <ShareTable slices={slices} labelCaption="Segment" valueCaption="Contacts" />
          )}
        />
      </div>

      <DataRetentionNote>
        Merged contacts are also removed, so they are not counted.
      </DataRetentionNote>
    </div>
  );
};
