import { Callout, CalloutPlacementType, CalloutType, InfoCard } from '@kentico/xperience-admin-components';
import React, { useCallback, useMemo, useState } from 'react';

import { toAdminHref } from '../shared/adminLinks';
import { AgedItemCaptions, AgedItemTable, toDaysRankedItems } from '../shared/AgedItemTable';
import { ComparisonInfoCard } from '../shared/ComparisonInfoCard';
import { channelsForContentKind, contentKindLabel, contentKindOptions } from '../shared/contentKinds';
import { toAgedCsv, toTimeSeriesCsv } from '../shared/csv';
import { OptionToggle } from '../shared/filterControls';
import { numberFormat } from '../shared/format';
import { RankedBarChart } from '../shared/RankedBarChart';
import { StackedColumnChart } from '../shared/StackedColumnChart';
import { StatsFilterBar } from '../shared/StatsFilterBar';
import { StatsTile } from '../shared/StatsTile';
import { toStatsSeries } from '../shared/timeSeries';
import { TimeSeriesTable } from '../shared/TimeSeriesTable';
import {
  StatsAgedItem,
  StatsChannelOption,
  StatsComparison,
  StatsFilter,
  StatsGrouping,
  StatsRankedCaptions,
  StatsRankedItem,
  StatsTimeSeriesResult,
} from '../shared/types';
import { useCsvExport } from '../shared/useCsvExport';
import { useStatsCommand } from '../shared/useStatsCommand';
import {
  formatDays,
  PublishingActivityContentType,
  PublishingTypeTable,
  toCreatedRankedItems,
  toPublishingTypeCsv,
} from './PublishingTypeTable';
import '../shared/stats.css';

/** Mirrors `PublishingActivityResult`. */
interface PublishingActivityResult {
  readonly from: string;
  readonly to: string;
  readonly grouping: StatsGrouping;
  /** Applied content type type filter (`Website`, `Reusable`, `Email`, `Headless`), `null` for all. */
  readonly kind: string | null;
  readonly channelId: number | null;
  readonly created: StatsComparison;
  readonly firstPublished: StatsComparison;
  /** `null` when content version history is disabled. */
  readonly updates: StatsComparison | null;
  readonly versionHistoryEnabled: boolean;
  /** Versions kept per language variant, 0 when not limited. */
  readonly versionHistoryLength: number;
  readonly medianDaysToPublish: number | null;
  readonly percentile90DaysToPublish: number | null;
  /** Published variants without a first publish date (migrated content). Not limited to the range. */
  readonly publishedDateUnknown: number;
  /** Created, first published and (with version history) updates per period. */
  readonly series: StatsTimeSeriesResult;
  readonly byContentType: readonly PublishingActivityContentType[];
  /** `since` = created, `until` = first published, `days` = whole days between them. */
  readonly slowest: readonly StatsAgedItem[];
  /** ISO timestamp of when the data was read from the database. */
  readonly updatedAt: string;
}

/** Mirrors `PublishingActivityFilter`. */
interface PublishingActivityFilter {
  readonly range: StatsFilter;
  readonly kind: string | null;
}

/** Mirrors `PublishingActivityClientProperties`. */
interface PublishingActivityTemplateProps {
  readonly report: PublishingActivityResult;
  /** Website, email and headless channels. */
  readonly channels: readonly StatsChannelOption[];
  readonly today: string;
  /** Path of this page relative to the admin root, used to build admin links. */
  readonly pagePath: string | null;
}

const slowestCaptions: AgedItemCaptions = {
  label: 'Item',
  category: 'Content type',
  language: 'Language',
  channel: 'Channel',
  since: 'Created',
  until: 'First published',
  days: 'Days to publish',
};

const slowestDaysCaptions: StatsRankedCaptions = {
  label: 'Item',
  secondaryLabel: 'Content type',
  value: 'Days to publish',
};

const typeCaptions: StatsRankedCaptions = {
  label: 'Content type',
  value: 'Created',
  secondaryValue: 'First published',
};

const variantHint = 'Counts are per language variant: an item in two languages counts twice.';

const disabledHint =
  'Content versioning is not enabled (Settings → Content → Content versioning), so updates (publishes after the first) are not counted.';

function toFilter(report: PublishingActivityResult): PublishingActivityFilter {
  return {
    range: { from: report.from, to: report.to, grouping: report.grouping, channelId: report.channelId },
    kind: report.kind,
  };
}

/** Clears the channel when it does not fit the kind (the channel filter applies only to pages, emails and headless items). */
function fitChannel(
  channels: readonly StatsChannelOption[],
  filter: PublishingActivityFilter,
): PublishingActivityFilter {
  const fits = channelsForContentKind(channels, filter.kind).some((c) => c.id === filter.range.channelId);
  return fits ? filter : { ...filter, range: { ...filter.range, channelId: null } };
}

export const PublishingActivityTemplate = (props: PublishingActivityTemplateProps) => {
  const saveCsv = useCsvExport();
  const { data: report, isLoading, hasError, load } = useStatsCommand<
    PublishingActivityResult,
    PublishingActivityFilter
  >(props.report);
  const [filter, setFilter] = useState<PublishingActivityFilter>(() => toFilter(props.report));

  const channels = useMemo(
    () => channelsForContentKind(props.channels, filter.kind),
    [props.channels, filter.kind],
  );

  const handleFilterChange = (next: PublishingActivityFilter) => {
    const normalized = fitChannel(props.channels, next);
    setFilter(normalized);
    void load(normalized);
  };

  const handleRefresh = () => {
    void load(filter, { refresh: true });
  };

  const { pagePath } = props;
  const getRankedHref = useCallback((item: StatsRankedItem) => toAdminHref(item.adminPath, pagePath), [pagePath]);
  const getAgedHref = useCallback((item: StatsAgedItem) => toAdminHref(item.adminPath, pagePath), [pagePath]);
  const getTypeHref = useCallback(
    (item: PublishingActivityContentType) => toAdminHref(item.adminPath, pagePath),
    [pagePath],
  );

  const { series: trend } = report;
  const series = useMemo(() => toStatsSeries(trend.series), [trend.series]);
  const typeItems = useMemo(() => toCreatedRankedItems(report.byContentType), [report.byContentType]);
  const slowestDays = useMemo(() => toDaysRankedItems(report.slowest), [report.slowest]);

  const rangeText = `${report.from} – ${report.to}`;
  const period = trend.grouping.toLowerCase();
  const fileSuffix = `${report.from}_${report.to}_${(report.kind ?? 'all').toLowerCase()}${report.channelId ? `_channel-${report.channelId}` : ''}`;
  const historyText =
    report.versionHistoryLength > 0
      ? ` It keeps the last ${numberFormat.format(report.versionHistoryLength)} versions of each language variant, so older updates are missing.`
      : '';
  const updatesHint = `Updates are publishes after the first, read from content version history. History starts when content versioning was enabled, so earlier updates are not counted. Emails are not versioned, so they have no updates.${historyText}`;
  const unknownHint =
    report.publishedDateUnknown > 0
      ? ` ${numberFormat.format(report.publishedDateUnknown)} published ${report.publishedDateUnknown === 1 ? 'item has' : 'items have'} no first publish date (for example migrated content), so ${report.publishedDateUnknown === 1 ? 'it is' : 'they are'} not in the first published counts.`
      : '';

  const exportSeriesCsv = () => {
    saveCsv(
      'publishing-activity-series',
      `publishing-activity-series_${fileSuffix}_${period}.csv`,
      toTimeSeriesCsv(trend.periods, series, { includeTotal: false }),
    );
  };

  const exportTypesCsv = () => {
    saveCsv(
      'publishing-activity-types',
      `publishing-activity-types_${fileSuffix}.csv`,
      toPublishingTypeCsv(report.byContentType, report.versionHistoryEnabled, getTypeHref),
    );
  };

  const exportSlowestCsv = () => {
    saveCsv(
      'publishing-activity-slowest',
      `publishing-activity-slowest_${fileSuffix}.csv`,
      toAgedCsv(report.slowest, slowestCaptions, getAgedHref),
    );
  };

  const filterText = report.kind ? ` ${contentKindLabel(report.kind)} only.` : '';
  const isEmpty = trend.total === 0;
  const emptyMessage = 'No content was created or published in the selected range. Try a longer range.';

  return (
    <div className="SimpleStats-root">
      <StatsFilterBar
        filter={filter.range}
        today={props.today}
        channels={channels}
        onChange={(range) => handleFilterChange({ ...filter, range })}
        onRefresh={handleRefresh}
        isLoading={isLoading}
        updatedAt={report.updatedAt}
      >
        <OptionToggle
          label={contentKindOptions.label}
          items={contentKindOptions.items}
          value={filter.kind}
          onChange={(kind) => handleFilterChange({ ...filter, kind })}
        />
      </StatsFilterBar>

      {!report.versionHistoryEnabled && (
        <Callout
          type={CalloutType.FriendlyWarning}
          placement={CalloutPlacementType.OnDesk}
          headline="Content versioning is off"
        >
          <p>{disabledHint}</p>
        </Callout>
      )}

      <div className="SimpleStats-kpis">
        <ComparisonInfoCard
          caption="Created"
          tooltip={`Language variants created in the selected range (${rangeText}).${filterText} ${variantHint}`}
          comparison={report.created}
          noun="items"
        />
        <ComparisonInfoCard
          caption="First published"
          tooltip={`Language variants published for the first time in the selected range.${filterText}${unknownHint}`}
          comparison={report.firstPublished}
          noun="items"
        />
        {report.updates ? (
          <ComparisonInfoCard
            caption="Updates"
            tooltip={`Publishes in the selected range that were not the first publish of the language variant.${filterText} ${updatesHint}`}
            comparison={report.updates}
            noun="updates"
          />
        ) : (
          <InfoCard
            caption="Updates"
            tooltip={disabledHint}
            text="–"
            details="Content versioning is off"
          />
        )}
        <InfoCard
          caption="Median days to publish"
          tooltip="Days from created to first published, for language variants first published in the selected range. Half took less, half took longer. A first publish before the creation date is left out."
          text={formatDays(report.medianDaysToPublish)}
          details={
            report.percentile90DaysToPublish === null
              ? 'Nothing first published in the range'
              : `90% within ${formatDays(report.percentile90DaysToPublish)} days`
          }
        />
      </div>

      <StatsTile
        headline="Created and published"
        description={`Language variants created, first published${report.versionHistoryEnabled ? ' and updated' : ''} per ${period}.${filterText}`}
        isLoading={isLoading}
        hasError={hasError}
        isEmpty={isEmpty}
        emptyMessage={emptyMessage}
        onExportCsv={exportSeriesCsv}
        renderChart={() => (
          <StackedColumnChart
            periods={trend.periods}
            series={series}
            stacked={false}
            ariaLabel={`Content created and published, ${rangeText}`}
          />
        )}
        renderTable={() => (
          <TimeSeriesTable grouping={trend.grouping} periods={trend.periods} series={series} showTotal={false} />
        )}
      />

      <div className="SimpleStats-tiles SimpleStats-tiles--halves">
        <StatsTile
          headline="By content type"
          description={`Content types with activity in the selected range, most first. The median days to publish counts language variants first published in the range. The chart shows created items. Click a content type to open it.${filterText}`}
          isLoading={isLoading}
          hasError={hasError}
          isEmpty={report.byContentType.length === 0}
          emptyMessage={emptyMessage}
          onExportCsv={exportTypesCsv}
          defaultView="table"
          renderChart={() => (
            <RankedBarChart
              items={typeItems}
              captions={typeCaptions}
              ariaLabel={`Created items per content type, ${rangeText}`}
              getHref={getRankedHref}
            />
          )}
          renderTable={() => (
            <PublishingTypeTable
              items={report.byContentType}
              showUpdates={report.versionHistoryEnabled}
              getAdminHref={getTypeHref}
            />
          )}
        />

        <StatsTile
          headline="Slowest to publish"
          description={`Language variants first published in the selected range that took longest from created to first published. Click an item to open it.${filterText}`}
          isLoading={isLoading}
          hasError={hasError}
          isEmpty={report.slowest.length === 0}
          emptyMessage="Nothing was first published in the selected range."
          onExportCsv={exportSlowestCsv}
          defaultView="table"
          renderChart={() => (
            <RankedBarChart
              items={slowestDays}
              captions={slowestDaysCaptions}
              ariaLabel={`Days to publish per item, ${rangeText}`}
              getHref={getRankedHref}
              showShare={false}
            />
          )}
          renderTable={() => (
            <AgedItemTable items={report.slowest} captions={slowestCaptions} getAdminHref={getAgedHref} />
          )}
        />
      </div>

      <Callout type={CalloutType.QuickTip} placement={CalloutPlacementType.OnDesk} headline="How content is counted">
        <p>
          {variantHint} Created is when the language variant was created; first published is its first publish in that
          language. {report.versionHistoryEnabled ? updatesHint : disabledHint}
          {unknownHint} Deleted items are not counted, also not for past dates. Page folders are left out.
        </p>
      </Callout>
    </div>
  );
};
