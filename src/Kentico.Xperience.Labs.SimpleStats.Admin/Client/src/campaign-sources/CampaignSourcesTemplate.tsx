import { Callout, CalloutPlacementType, CalloutType, InfoCard } from '@kentico/xperience-admin-components';
import React, { useMemo, useState } from 'react';

import { noUtmDataMessage, noValueLabel, sentences, shownText, utmCaptureGuideUrl } from '../shared/campaigns';
import { ComparisonInfoCard } from '../shared/ComparisonInfoCard';
import { toRankedCsv, toTimeSeriesCsv } from '../shared/csv';
import { DataRetentionNote } from '../shared/DataRetentionNote';
import { TextSelect } from '../shared/filterControls';
import { formatPreviousPeriod, formatShare, numberFormat } from '../shared/format';
import { RankedBarChart } from '../shared/RankedBarChart';
import { RankedTable } from '../shared/RankedTable';
import { StackedColumnChart } from '../shared/StackedColumnChart';
import { StatsFilterBar } from '../shared/StatsFilterBar';
import { StatsTile } from '../shared/StatsTile';
import { toStatsSeries } from '../shared/timeSeries';
import { TimeSeriesTable } from '../shared/TimeSeriesTable';
import {
  StatsChannelOption,
  StatsComparison,
  StatsFilter,
  StatsGrouping,
  StatsRankedCaptions,
  StatsRankedResult,
  StatsTimeSeriesResult,
} from '../shared/types';
import { useCsvExport } from '../shared/useCsvExport';
import { useStatsCommand } from '../shared/useStatsCommand';
import '../shared/stats.css';

/** Mirrors `CampaignSourcesResult`. */
interface CampaignSourcesResult {
  readonly from: string;
  readonly to: string;
  readonly grouping: StatsGrouping;
  readonly channelId: number | null;
  /** Applied source filter, `null` for all. */
  readonly source: string | null;
  /** Applied content filter (empty string: no content), `null` for all. */
  readonly content: string | null;
  /** All landings, not limited by the source filter. */
  readonly landings: StatsComparison;
  /** Campaign landings of the selected source and content. */
  readonly campaignLandings: StatsComparison;
  /** Campaign landings / landings (0–1), `null` without landings. */
  readonly campaignShare: number | null;
  readonly campaignVisitors: number;
  /** Distinct sources, not limited by the source filter. */
  readonly sources: StatsComparison;
  /** Campaign landings per period by the top sources plus "Other". */
  readonly series: StatsTimeSeriesResult;
  /** All sources (not limited by the source filter), with the previous period. */
  readonly bySource: StatsRankedResult;
  /** Landing pages of the selected source and content. */
  readonly pages: StatsRankedResult;
  /** Source and content pairs of the selected source. */
  readonly bySourceContent: StatsRankedResult;
  readonly sourceOptions: readonly string[];
  /** Contents of the selected source (empty string: no content). */
  readonly contentOptions: readonly string[];
  /** Any activity on the site has a UTM source (`false`: UTM values are not captured). */
  readonly hasAnyUtmData: boolean;
  /** ISO timestamp of when the data was read from the database. */
  readonly updatedAt: string;
}

/** Mirrors `CampaignSourcesFilter`. */
interface CampaignSourcesFilter {
  readonly range: StatsFilter;
  readonly source: string | null;
  readonly content: string | null;
}

/** Mirrors `CampaignSourcesClientProperties`. */
interface CampaignSourcesTemplateProps {
  readonly report: CampaignSourcesResult;
  /** Website channels. */
  readonly channels: readonly StatsChannelOption[];
  readonly today: string;
}

const pageCaptions: StatsRankedCaptions = {
  label: 'Page',
  secondaryLabel: 'Channel and language',
  value: 'Campaign landings',
  secondaryValue: 'Visitors',
};

const contentCaptions: StatsRankedCaptions = {
  label: 'Source',
  secondaryLabel: 'Content',
  value: 'Landings',
  secondaryValue: 'Visitors',
};

const landingHint =
  'A landing page activity is logged for the first page of a browsing session (a new session starts after 20 minutes without page views), so landings are about the sessions that started on a page.';

const utmHint =
  'Xperience does not store UTM values by itself; they exist only when the site captures them (see UTM capture in the usage guide). Only utm_source and utm_content are stored. utm_campaign is not, so put the campaign or creative name in utm_content.';

function toFilter(report: CampaignSourcesResult): CampaignSourcesFilter {
  return {
    range: { from: report.from, to: report.to, grouping: report.grouping, channelId: report.channelId },
    source: report.source,
    content: report.content,
  };
}

function contentLabel(content: string): string {
  return content === '' ? noValueLabel : content;
}

export const CampaignSourcesTemplate = (props: CampaignSourcesTemplateProps) => {
  const saveCsv = useCsvExport();
  const { data: report, isLoading, hasError, load } = useStatsCommand<CampaignSourcesResult, CampaignSourcesFilter>(
    props.report,
  );
  const [filter, setFilter] = useState<CampaignSourcesFilter>(() => toFilter(props.report));

  const handleFilterChange = (next: CampaignSourcesFilter) => {
    // A new source has other contents.
    const normalized = next.source === filter.source ? next : { ...next, content: null };
    setFilter(normalized);
    void load(normalized);
  };

  const handleRefresh = () => {
    void load(filter, { refresh: true });
  };

  const sourceOptions = useMemo(
    () => report.sourceOptions.map((source) => ({ value: source, label: source })),
    [report.sourceOptions],
  );
  const contentOptions = useMemo(
    () => report.contentOptions.map((content) => ({ value: content, label: contentLabel(content) })),
    [report.contentOptions],
  );
  const series = useMemo(() => toStatsSeries(report.series.series), [report.series.series]);
  const bySourceCaptions = useMemo(() => sourceCaptions(report), [report]);

  const rangeText = `${report.from} – ${report.to}`;
  const period = report.series.grouping.toLowerCase();
  const selectedText =
    report.source === null
      ? 'all sources'
      : `${report.source}${report.content === null ? '' : ` / ${contentLabel(report.content)}`}`;
  const selectedSentence = report.source === null ? '' : `Source: ${selectedText}.`;
  const fileSuffix = `${report.from}_${report.to}${report.channelId ? `_channel-${report.channelId}` : ''}`;
  const selectedFileSuffix = `${fileSuffix}${report.source === null ? '' : `_${report.source}`}${report.content ? `_${report.content}` : ''}`;

  const noCampaignLandings = report.source === null
    ? 'No campaign landings in the selected range. Try a longer range or another channel.'
    : `No campaign landings from ${selectedText} in the selected range.`;
  const emptyMessage = report.hasAnyUtmData ? noCampaignLandings : noUtmDataMessage;

  const exportSeriesCsv = () => {
    saveCsv(
      'campaign-sources-series',
      `campaign-sources-series_${selectedFileSuffix}_${period}.csv`,
      toTimeSeriesCsv(report.series.periods, series),
    );
  };

  const exportSourcesCsv = () => {
    saveCsv(
      'campaign-sources-sources',
      `campaign-sources-sources_${fileSuffix}.csv`,
      toRankedCsv(report.bySource.items, bySourceCaptions),
    );
  };

  const exportPagesCsv = () => {
    saveCsv(
      'campaign-sources-pages',
      `campaign-sources-pages_${selectedFileSuffix}.csv`,
      toRankedCsv(report.pages.items, pageCaptions),
    );
  };

  const exportContentCsv = () => {
    saveCsv(
      'campaign-sources-content',
      `campaign-sources-content_${fileSuffix}${report.source === null ? '' : `_${report.source}`}.csv`,
      toRankedCsv(report.bySourceContent.items, contentCaptions),
    );
  };

  return (
    <div className="SimpleStats-root">
      <StatsFilterBar
        filter={filter.range}
        today={props.today}
        channels={props.channels}
        onChange={(range) => handleFilterChange({ ...filter, range })}
        onRefresh={handleRefresh}
        isLoading={isLoading}
        updatedAt={report.updatedAt}
      >
        <TextSelect
          label="Source"
          allLabel="All sources"
          options={sourceOptions}
          value={filter.source}
          onChange={(source) => handleFilterChange({ ...filter, source })}
        />
        {filter.source !== null && (
          <TextSelect
            label="Content"
            allLabel="All contents"
            options={contentOptions}
            value={filter.content}
            onChange={(content) => handleFilterChange({ ...filter, content })}
          />
        )}
      </StatsFilterBar>

      <div className="SimpleStats-kpis">
        <ComparisonInfoCard
          caption="Landings"
          tooltip={`Landing page activities in the selected range (${rangeText}), with or without UTM values. ${landingHint}`}
          comparison={report.landings}
          noun="landings"
        />
        <ComparisonInfoCard
          caption="Campaign landings"
          tooltip={`Landings with a UTM source (utm_source)${report.source === null ? '' : ` from ${selectedText}`}.`}
          comparison={report.campaignLandings}
          noun="landings"
        />
        <InfoCard
          caption="Campaign share"
          tooltip={`Campaign landings${report.source === null ? '' : ` from ${selectedText}`} / all landings. Visitors are distinct contacts.`}
          text={report.campaignShare === null ? '–' : formatShare(report.campaignShare)}
          details={
            report.campaignShare === null
              ? 'No landings'
              : `${numberFormat.format(report.campaignVisitors)} visitors`
          }
        />
        <ComparisonInfoCard
          caption="Sources"
          tooltip="Distinct UTM sources with landings in the selected range (all sources)."
          comparison={report.sources}
          noun="sources"
        />
      </div>

      <StatsTile
        headline="Top landing pages"
        description={sentences(
          `Pages where campaign landings from ${selectedText} started, most first.`,
          shownText(report.pages, 'pages'),
          'Share is of those campaign landings. Click a page to open it on the website.',
        )}
        isLoading={isLoading}
        hasError={hasError}
        isEmpty={report.pages.items.length === 0}
        emptyMessage={emptyMessage}
        onExportCsv={exportPagesCsv}
        renderChart={() => (
          <RankedBarChart
            items={report.pages.items}
            captions={pageCaptions}
            ariaLabel={`Top landing pages, ${selectedText}, ${rangeText}`}
          />
        )}
        renderTable={() => <RankedTable items={report.pages.items} captions={pageCaptions} showSecondaryLabel />}
      />

      <div className="SimpleStats-tiles SimpleStats-tiles--halves">
        <StatsTile
          headline="Top sources"
          description={sentences(
            'Campaign landings per UTM source, all sources (the source filter does not apply).',
            shownText(report.bySource, 'sources'),
            'Share is of all campaign landings; visitors are distinct contacts.',
          )}
          isLoading={isLoading}
          hasError={hasError}
          isEmpty={report.bySource.items.length === 0}
          emptyMessage={report.hasAnyUtmData ? 'No campaign landings in the selected range.' : noUtmDataMessage}
          onExportCsv={exportSourcesCsv}
          renderChart={() => (
            <RankedBarChart
              items={report.bySource.items}
              captions={bySourceCaptions}
              ariaLabel={`Campaign landings by source, ${rangeText}`}
            />
          )}
          renderTable={() => <RankedTable items={report.bySource.items} captions={bySourceCaptions} />}
        />

        <StatsTile
          headline="Source and content"
          description={sentences(
            report.source === null
              ? 'Campaign landings per UTM source and content (utm_content).'
              : `Campaign landings from ${report.source} per content (utm_content). The content filter does not apply.`,
            shownText(report.bySourceContent, 'pairs'),
            `${noValueLabel} means no content was given.`,
          )}
          isLoading={isLoading}
          hasError={hasError}
          isEmpty={report.bySourceContent.items.length === 0}
          emptyMessage={emptyMessage}
          onExportCsv={exportContentCsv}
          renderTable={() => (
            <RankedTable items={report.bySourceContent.items} captions={contentCaptions} showSecondaryLabel />
          )}
        />
      </div>

      <StatsTile
        headline="Campaign landings over time"
        description={sentences(
          `Campaign landings per ${period}, by the top 5 sources; the rest is Other.`,
          selectedSentence,
        )}
        isLoading={isLoading}
        hasError={hasError}
        isEmpty={report.series.total === 0}
        emptyMessage={emptyMessage}
        onExportCsv={exportSeriesCsv}
        renderChart={() => (
          <StackedColumnChart
            periods={report.series.periods}
            series={series}
            ariaLabel={`Campaign landings by source, ${rangeText}`}
          />
        )}
        renderTable={() => (
          <TimeSeriesTable grouping={report.series.grouping} periods={report.series.periods} series={series} />
        )}
      />

      <Callout type={CalloutType.QuickTip} placement={CalloutPlacementType.OnDesk} headline="About campaign data">
        <p>
          {utmHint} {landingHint} Conversions after a campaign landing are not shown.
        </p>
        <p>
          {/* Plain anchor: Callout styles links in its content with the admin link colors. */}
          <a href={utmCaptureGuideUrl} target="_blank" rel="noopener noreferrer">
            UTM capture in the usage guide
          </a>
        </p>
      </Callout>

      <DataRetentionNote />
    </div>
  );
};

/** Captions of the sources list, with the previous period ("Landings previous 30 days"). */
function sourceCaptions(report: CampaignSourcesResult): StatsRankedCaptions {
  return {
    label: 'Source',
    value: 'Landings',
    secondaryValue: 'Visitors',
    previousValue: `Landings ${formatPreviousPeriod(report.landings)}`,
    change: 'Change',
  };
}
