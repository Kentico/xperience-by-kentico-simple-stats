import { Callout, CalloutPlacementType, CalloutType, InfoCard } from '@kentico/xperience-admin-components';
import React, { useCallback, useMemo, useState } from 'react';

import { toAdminHref } from '../shared/adminLinks';
import { channelsForContentKind, contentKindOptions, fitContentChannel } from '../shared/contentKinds';
import { numberFormat } from '../shared/format';
import { SnapshotFilterBar, SnapshotKindOptions, SnapshotWindowOptions } from '../shared/SnapshotFilterBar';
import { StackedColumnChart } from '../shared/StackedColumnChart';
import { StatsTile } from '../shared/StatsTile';
import { toStatsSeries } from '../shared/timeSeries';
import { StatsChannelOption, StatsSnapshotFilter, StatsTimeSeriesResult } from '../shared/types';
import { useCsvExport } from '../shared/useCsvExport';
import { useStatsCommand } from '../shared/useStatsCommand';
import '../shared/stats.css';

import {
  PublishingCalendarItem,
  PublishingItemCaptions,
  PublishingItemTable,
  toPublishingCsv,
} from './PublishingItemTable';

/** Mirrors `PublishingCalendarResult`. */
interface PublishingCalendarResult {
  /** Applied window in days ahead. */
  readonly window: number;
  /** Applied content type type filter (`Website`, `Reusable`, `Email`, `Headless`), `null` for all. */
  readonly kind: string | null;
  readonly channelId: number | null;
  readonly upcomingPublish: number;
  readonly upcomingUnpublish: number;
  /** Scheduled sends of regular emails in the window. */
  readonly upcomingSend: number;
  readonly recentlyPublished: number;
  /** Days that count as recent for `recentlyPublished`. */
  readonly recentDays: number;
  /** Upcoming events per day from today to the window end (series `publish`, `unpublish`, `send`). */
  readonly days: StatsTimeSeriesResult;
  /** Soonest first. */
  readonly upcoming: readonly PublishingCalendarItem[];

  /** Newest first. */
  readonly recent: readonly PublishingCalendarItem[];
  /** ISO timestamp of when the data was read from the database. */
  readonly updatedAt: string;
}

/** Mirrors `PublishingCalendarClientProperties`. */
interface PublishingCalendarTemplateProps {
  readonly report: PublishingCalendarResult;
  /** Website and email channels. */
  readonly channels: readonly StatsChannelOption[];
  /** Windows (days ahead) the client can pick. */
  readonly windows: readonly number[];
  /** Path of this page relative to the admin root, used to build admin links. */
  readonly pagePath: string | null;
}

/** Content kinds without headless items: they cannot be scheduled to publish or unpublish (mirrors `PublishingCalendarReportBuilder.Kinds`). */
const kinds: SnapshotKindOptions = {
  ...contentKindOptions,
  items: contentKindOptions.items.filter((item) => item.id !== 'Headless'),
};

const eventCaptions: PublishingItemCaptions = { when: 'Scheduled for', action: 'Action' };

const recentCaptions: PublishingItemCaptions = { when: 'Published' };

const schedulerHint =
  'The system checks for scheduled items once per minute; large batches can take several minutes. Editing a scheduled item cancels its schedule.';

const variantHint =
  'Publishes and unpublishes count per language variant (an item scheduled in two languages counts twice); sends count per email.';

function toFilter(report: PublishingCalendarResult): StatsSnapshotFilter {
  return { kind: report.kind, channelId: report.channelId, window: report.window };
}

export const PublishingCalendarTemplate = (props: PublishingCalendarTemplateProps) => {
  const saveCsv = useCsvExport();
  const { data: report, isLoading, hasError, load } = useStatsCommand<
    PublishingCalendarResult,
    StatsSnapshotFilter
  >(props.report);
  const [filter, setFilter] = useState<StatsSnapshotFilter>(() => toFilter(props.report));

  const channels = useMemo(
    () => channelsForContentKind(props.channels, filter.kind),
    [props.channels, filter.kind],
  );

  const windows = useMemo<SnapshotWindowOptions>(
    () => ({ label: 'Scheduled in the next', days: props.windows }),
    [props.windows],
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
  const getItemHref = useCallback(
    (item: PublishingCalendarItem) => toAdminHref(item.adminPath, pagePath),
    [pagePath],
  );

  const daySeries = useMemo(() => toStatsSeries(report.days.series), [report.days.series]);

  const windowText = `the next ${numberFormat.format(report.window)} days`;
  const recentText = `the last ${numberFormat.format(report.recentDays)} days`;
  const upcomingTotal = report.upcomingPublish + report.upcomingUnpublish + report.upcomingSend;

  const listedText = (shown: number, total: number) =>
    total > shown ? ` The first ${numberFormat.format(shown)} of ${numberFormat.format(total)} are listed.` : '';

  const fileSuffix = `${report.window}d_${(report.kind ?? 'all').toLowerCase()}${report.channelId ? `_channel-${report.channelId}` : ''}`;

  const exportUpcomingCsv = () => {
    saveCsv(
      'publishing-calendar-upcoming',
      `publishing-calendar-upcoming_${fileSuffix}.csv`,
      toPublishingCsv(report.upcoming, eventCaptions, getItemHref),
    );
  };

  const exportRecentCsv = () => {
    saveCsv(
      'publishing-calendar-recent',
      `publishing-calendar-recent_${fileSuffix}.csv`,
      toPublishingCsv(report.recent, recentCaptions, getItemHref),
    );
  };

  return (
    <div className="SimpleStats-root">
      <SnapshotFilterBar
        filter={filter}
        onChange={handleFilterChange}
        windows={windows}
        kinds={kinds}
        channels={channels}
        onRefresh={handleRefresh}
        isLoading={isLoading}
        updatedAt={report.updatedAt}
      />

      <div className="SimpleStats-kpis">
        <InfoCard
          caption="Scheduled to publish"
          tooltip={`Language variants with a scheduled publish in ${windowText}. ${variantHint}`}
          text={numberFormat.format(report.upcomingPublish)}
          details={`In ${windowText}`}
        />
        <InfoCard
          caption="Scheduled to unpublish"
          tooltip={`Language variants with a scheduled unpublish in ${windowText}. ${variantHint}`}
          text={numberFormat.format(report.upcomingUnpublish)}
          details={`In ${windowText}`}
        />
        <InfoCard
          caption="Scheduled sends"
          tooltip={`Regular emails scheduled to be sent in ${windowText}.`}
          text={numberFormat.format(report.upcomingSend)}
          details={`In ${windowText}`}
        />
        <InfoCard
          caption="Recently published"
          tooltip={`Language variants last published in ${recentText}. A variant published several times counts once.`}
          text={numberFormat.format(report.recentlyPublished)}
          details={`In ${recentText}`}
        />
      </div>

      <StatsTile
        headline="Upcoming"
        description={`Scheduled publishes, unpublishes and email sends in ${windowText}: the chart shows them per day, the table lists them soonest first (click an item to open it). Times are server time.${listedText(report.upcoming.length, upcomingTotal)}`}
        isLoading={isLoading}
        hasError={hasError}
        isEmpty={upcomingTotal === 0}
        emptyMessage={`Nothing is scheduled to publish, unpublish or send in ${windowText}.`}
        onExportCsv={exportUpcomingCsv}
        renderChart={() => (
          <StackedColumnChart
            periods={report.days.periods}
            series={daySeries}
            ariaLabel={`Scheduled publishes, unpublishes and sends per day in ${windowText}`}
          />
        )}
        renderTable={() => (
          <PublishingItemTable items={report.upcoming} captions={eventCaptions} getAdminHref={getItemHref} />
        )}
      />

      <StatsTile
        headline="Recently published"
        description={`Language variants last published in ${recentText}, newest first. Click an item to open it. Times are server time.${listedText(report.recent.length, report.recentlyPublished)}`}
        isLoading={isLoading}
        hasError={hasError}
        isEmpty={report.recentlyPublished === 0}
        emptyMessage={`No content was published in ${recentText}.`}
        onExportCsv={exportRecentCsv}
        renderTable={() => (
          <PublishingItemTable items={report.recent} captions={recentCaptions} getAdminHref={getItemHref} />
        )}
      />

      <Callout type={CalloutType.QuickTip} placement={CalloutPlacementType.OnDesk} headline="Scheduling">
        <p>
          {schedulerHint} {variantHint} Sends are regular emails only. Items in all workspaces are counted.
        </p>
      </Callout>
    </div>
  );
};
