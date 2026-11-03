import { InfoCard } from '@kentico/xperience-admin-components';
import React, { useMemo, useState } from 'react';

import { ComparisonInfoCard } from '../shared/ComparisonInfoCard';
import { toTimeSeriesCsv } from '../shared/csv';
import { DataRetentionNote } from '../shared/DataRetentionNote';
import { numberFormat } from '../shared/format';
import { StackedColumnChart } from '../shared/StackedColumnChart';
import { StatsFilterBar } from '../shared/StatsFilterBar';
import { StatsTile } from '../shared/StatsTile';
import { TimeSeriesTable } from '../shared/TimeSeriesTable';
import {
  StatsChannelOption,
  StatsComparison,
  StatsFilter,
  StatsGrouping,
  StatsPeriod,
  StatsSeries,
} from '../shared/types';
import { useCsvExport } from '../shared/useCsvExport';
import { useStatsCommand } from '../shared/useStatsCommand';
import '../shared/stats.css';

/** Mirrors `ActivityCountsSeries`. */
interface ActivityCountsSeries {
  readonly activityType: string;
  readonly displayName: string;
  readonly values: readonly number[];
  readonly total: number;
}

/** Mirrors `ActivityCountsResult`. */
interface ActivityCountsResult {
  readonly from: string;
  readonly to: string;
  readonly grouping: StatsGrouping;
  readonly channelId: number | null;
  readonly periods: readonly StatsPeriod[];
  readonly series: readonly ActivityCountsSeries[];
  readonly total: number;
  /** `total` vs the previous period of the same length. */
  readonly totalComparison: StatsComparison;
  /** ISO timestamp of when the counts were read from the database. */
  readonly updatedAt: string;
}

/** Mirrors `ActivityCountsClientProperties`. */
interface ActivityCountsTemplateProps {
  readonly report: ActivityCountsResult;
  readonly channels: readonly StatsChannelOption[];
  readonly today: string;
}

function toFilter(report: ActivityCountsResult): StatsFilter {
  return {
    from: report.from,
    to: report.to,
    grouping: report.grouping,
    channelId: report.channelId,
  };
}

export const ActivityCountsTemplate = (props: ActivityCountsTemplateProps) => {
  const saveCsv = useCsvExport();
  const { data: report, isLoading, hasError, load } =
    useStatsCommand<ActivityCountsResult>(props.report);
  const [filter, setFilter] = useState<StatsFilter>(() => toFilter(props.report));

  const handleFilterChange = (next: StatsFilter) => {
    setFilter(next);
    void load(next);
  };

  const handleRefresh = () => {
    void load(filter, { refresh: true });
  };

  const chartSeries = useMemo<StatsSeries[]>(
    () =>
      report.series.map((s) => ({
        key: s.activityType,
        name: s.displayName,
        values: s.values,
      })),
    [report.series],
  );

  const topType = report.series[0];
  const rangeText = `${report.from} – ${report.to}`;

  const exportCsv = () => {
    saveCsv(
      'activity-counts',
      `activity-counts_${report.from}_${report.to}_${report.grouping.toLowerCase()}.csv`,
      toTimeSeriesCsv(report.periods, chartSeries),
    );
  };

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
      />

      <div className="SimpleStats-kpis">
        <ComparisonInfoCard
          caption="Total activities"
          tooltip={`All logged contact activities in the selected range (${rangeText}) and channel.`}
          comparison={report.totalComparison}
          noun="activities"
        />
        <InfoCard
          caption="Top activity type"
          tooltip="Activity type with the most activities in the selected range."
          text={topType ? topType.displayName : '–'}
          details={
            topType
              ? `${numberFormat.format(topType.total)} activities (${Math.round(
                  (topType.total / Math.max(report.total, 1)) * 100,
                )}%)`
              : 'No activities'
          }
        />
        <InfoCard
          caption="Activity types"
          tooltip="Number of activity types with at least one activity in the selected range."
          text={numberFormat.format(report.series.length)}
          details="With at least one activity"
        />
      </div>

      <StatsTile
        headline="Activity counts by type"
        description={`Activities per ${report.grouping.toLowerCase()}, stacked by activity type.`}
        isLoading={isLoading}
        hasError={hasError}
        isEmpty={report.total === 0}
        emptyMessage="No activities were logged in the selected range. Try a longer range or another channel."
        onExportCsv={exportCsv}
        renderChart={() => (
          <StackedColumnChart
            periods={report.periods}
            series={chartSeries}
            ariaLabel={`Activity counts by type, ${rangeText}`}
          />
        )}
        renderTable={() => (
          <TimeSeriesTable
            grouping={report.grouping}
            periods={report.periods}
            series={chartSeries}
          />
        )}
      />

      <DataRetentionNote />
    </div>
  );
};
