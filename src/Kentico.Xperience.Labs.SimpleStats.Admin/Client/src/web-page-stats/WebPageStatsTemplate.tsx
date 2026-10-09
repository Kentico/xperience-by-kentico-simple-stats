import { InfoCard } from '@kentico/xperience-admin-components';
import React, { useMemo, useState } from 'react';

import { toTimeSeriesCsv } from '../shared/csv';
import { DataRetentionNote } from '../shared/DataRetentionNote';
import { numberFormat } from '../shared/format';
import { StackedColumnChart } from '../shared/StackedColumnChart';
import { StatsFilterBar } from '../shared/StatsFilterBar';
import { StatsTile } from '../shared/StatsTile';
import { TimeSeriesTable } from '../shared/TimeSeriesTable';
import { StatsFilter, StatsGrouping, StatsPeriod, StatsSeries } from '../shared/types';
import { useCsvExport } from '../shared/useCsvExport';
import { useStatsCommand } from '../shared/useStatsCommand';
import { CampaignSources, WebPageCampaignsResult } from './CampaignSources';
import '../shared/stats.css';

/** Mirrors `StatsTimeSeries`. */
interface WebPageStatsSeries {
  readonly key: string;
  readonly displayName: string;
  readonly values: readonly number[];
  readonly total: number;
}

/** Mirrors `WebPageStatsResult`. */
interface WebPageStatsResult {
  readonly from: string;
  readonly to: string;
  readonly grouping: StatsGrouping;
  readonly periods: readonly StatsPeriod[];
  readonly series: readonly WebPageStatsSeries[];
  readonly total: number;
  readonly pageVisits: number;
  readonly uniqueContacts: number;
  readonly uniqueVisitors: number;
  readonly formSubmissions: number;
  readonly uniqueSubmitters: number;
  /** Path form submissions are matched by (`''` for the site root), or `null` when the page has no live URL. */
  readonly formUrlPath: string | null;
  /** Hosts form submissions must be logged on (language-specific domains). Empty means any host. */
  readonly formUrlHosts: readonly string[];
  /** The page's channel uses language-specific domains. */
  readonly usesLanguageDomains: boolean;
  /** Landings of the page and their UTM sources. */
  readonly campaigns: WebPageCampaignsResult;
  /** ISO timestamp of when the counts were read from the database. */
  readonly updatedAt: string;
}

/** Mirrors `WebPageStatsClientProperties`. */
interface WebPageStatsTemplateProps {
  readonly report: WebPageStatsResult;
  readonly today: string;
}

function getFormHostText(report: WebPageStatsResult): string {
  if (report.formUrlHosts.length > 0) {
    return `on ${report.formUrlHosts.join(', ')} (the domains of this language)`;
  }
  // Language-specific domains without a domain for this language: all languages share the path, so they are counted together.
  return report.usesLanguageDomains
    ? 'on any domain of this channel. No domain is configured for this language, so submissions of other languages on the same path are included'
    : 'on any domain of this channel';
}

function toFilter(report: WebPageStatsResult): StatsFilter {
  // The page belongs to one channel, so the channel filter is not used.
  return { from: report.from, to: report.to, grouping: report.grouping, channelId: null };
}

export const WebPageStatsTemplate = (props: WebPageStatsTemplateProps) => {
  const saveCsv = useCsvExport();
  const { data: report, isLoading, hasError, load } =
    useStatsCommand<WebPageStatsResult>(props.report);
  const [filter, setFilter] = useState<StatsFilter>(() => toFilter(props.report));

  const handleFilterChange = (next: StatsFilter) => {
    setFilter(next);
    void load(next);
  };

  const handleRefresh = () => {
    void load(filter, { refresh: true });
  };

  const chartSeries = useMemo<StatsSeries[]>(
    () => report.series.map((s) => ({ key: s.key, name: s.displayName, values: s.values })),
    [report.series],
  );

  const rangeText = `${report.from} – ${report.to}`;
  const formUrlText = report.formUrlPath === null ? null : report.formUrlPath || '/';
  const formHostText = getFormHostText(report);
  // Share of visitors that also submitted a form. Both are distinct contacts in the range.
  const conversionText =
    report.uniqueVisitors > 0
      ? `${Math.round((report.uniqueSubmitters / report.uniqueVisitors) * 1000) / 10}% of visitors`
      : 'No visitors';

  const exportCsv = () => {
    saveCsv(
      'web-page-stats',
      `web-page-stats_${report.from}_${report.to}_${report.grouping.toLowerCase()}.csv`,
      toTimeSeriesCsv(report.periods, chartSeries),
    );
  };

  return (
    <div className="SimpleStats-root SimpleStats-root--framed">
      <StatsFilterBar
        filter={filter}
        today={props.today}
        onChange={handleFilterChange}
        onRefresh={handleRefresh}
        isLoading={isLoading}
        updatedAt={report.updatedAt}
        showChannel={false}
      />

      <div className="SimpleStats-kpis">
        <InfoCard
          caption="Page visits"
          tooltip="Page visit activities logged for this page in the edited language."
          text={numberFormat.format(report.pageVisits)}
          details={rangeText}
        />
        <InfoCard
          caption="Unique visitors"
          tooltip="Number of distinct contacts with at least one page visit in the selected range."
          text={numberFormat.format(report.uniqueVisitors)}
          details="Contacts that visited"
        />
        <InfoCard
          caption="Form submissions"
          tooltip={
            formUrlText === null
              ? 'Form submissions are matched by the live page URL. This page has no live URL.'
              : `Form submissions logged on ${formUrlText} ${formHostText} (matched by the current live URL, ignoring query string and trailing slash). Submissions made under a former URL of the page are not counted.`
          }
          text={formUrlText === null ? '–' : numberFormat.format(report.formSubmissions)}
          details={
            formUrlText === null
              ? 'No live URL'
              : `${numberFormat.format(report.uniqueSubmitters)} contacts, ${conversionText}`
          }
        />
        <InfoCard
          caption="Total activities"
          tooltip="All contact activities logged for this page, including matched form submissions."
          text={numberFormat.format(report.total)}
          details={`From ${numberFormat.format(report.uniqueContacts)} contacts`}
        />
      </div>

      <StatsTile
        headline="Activities on this page"
        description={`Activities per ${report.grouping.toLowerCase()}, stacked by activity type. Counts only, no contact details.`}
        isLoading={isLoading}
        hasError={hasError}
        isEmpty={report.total === 0}
        emptyMessage="No activities were logged for this page in the selected range. Try a longer range."
        onExportCsv={exportCsv}
        renderChart={() => (
          <StackedColumnChart
            periods={report.periods}
            series={chartSeries}
            ariaLabel={`Activities on this page by type, ${rangeText}`}
          />
        )}
        renderTable={() => (
          <TimeSeriesTable grouping={report.grouping} periods={report.periods} series={chartSeries} />
        )}
      />

      <CampaignSources
        campaigns={report.campaigns}
        from={report.from}
        to={report.to}
        isLoading={isLoading}
        hasError={hasError}
        saveCsv={saveCsv}
      />

      <DataRetentionNote />
    </div>
  );
};
