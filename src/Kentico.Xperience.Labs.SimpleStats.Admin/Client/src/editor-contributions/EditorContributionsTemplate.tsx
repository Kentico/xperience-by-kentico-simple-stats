import { Callout, CalloutPlacementType, CalloutType, InfoCard } from '@kentico/xperience-admin-components';
import React, { useCallback, useMemo, useState } from 'react';

import { toAdminHref } from '../shared/adminLinks';
import { ComparisonInfoCard } from '../shared/ComparisonInfoCard';
import {
  channelsForContentKind,
  contentKindLabel,
  contentKindOptions,
  fitRangeContentChannel,
} from '../shared/contentKinds';
import { toTimeSeriesCsv } from '../shared/csv';
import { OptionToggle } from '../shared/filterControls';
import { numberFormat } from '../shared/format';
import { RankedBarChart } from '../shared/RankedBarChart';
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
  StatsRankedItem,
  StatsTimeSeriesResult,
} from '../shared/types';
import { useCsvExport } from '../shared/useCsvExport';
import { useStatsCommand } from '../shared/useStatsCommand';
import { EditorContribution, editorChartCaptions, EditorTable, toEditorCsv, toEditorRankedItems } from './EditorTable';
import '../shared/stats.css';

/** Mirrors `EditorContributionsResult`. */
interface EditorContributionsResult {
  readonly from: string;
  readonly to: string;
  readonly grouping: StatsGrouping;
  /** Applied content type type filter (`Website`, `Reusable`, `Email`, `Headless`), `null` for all. */
  readonly kind: string | null;
  readonly channelId: number | null;
  /** Users (that still exist) with any created or last modified item. */
  readonly activeEditors: StatsComparison;
  readonly created: StatsComparison;
  readonly lastModified: StatsComparison;
  /** `null` when content version history is disabled. */
  readonly published: StatsComparison | null;
  readonly versionHistoryEnabled: boolean;
  /** Versions kept per language variant, 0 when not limited. */
  readonly versionHistoryLength: number;
  /** Users with any contribution in the range (not only `byUser`). */
  readonly userCount: number;
  /** Most created + last modified first. */
  readonly byUser: readonly EditorContribution[];
  /** Created per period for the top users, the others as "Other users". */
  readonly series: StatsTimeSeriesResult;
  /** ISO timestamp of when the data was read from the database. */
  readonly updatedAt: string;
}

/** Mirrors `PublishingActivityFilter` (the filter is shared with the publishing activity report). */
interface EditorContributionsFilter {
  readonly range: StatsFilter;
  readonly kind: string | null;
}

/** Mirrors `EditorContributionsClientProperties`. */
interface EditorContributionsTemplateProps {
  readonly report: EditorContributionsResult;
  /** Website, email and headless channels. */
  readonly channels: readonly StatsChannelOption[];
  readonly today: string;
  /** Path of this page relative to the admin root, used to build admin links. */
  readonly pagePath: string | null;
}

const variantHint = 'Counts are per language variant: an item in two languages counts twice.';

const lastModifiedHint =
  'Only the latest change of each language variant is stored, so earlier changes, also by other users, are not counted.';

const systemUserHint =
  'System users (the service user used by imports and automation, or the public user) are shown as their own row. Changes by users that no longer exist are shown as Unknown user.';

const disabledHint =
  'Content versioning is not enabled (Settings → Content → Content versioning), so publishing is not tracked per user.';

function toFilter(report: EditorContributionsResult): EditorContributionsFilter {
  return {
    range: { from: report.from, to: report.to, grouping: report.grouping, channelId: report.channelId },
    kind: report.kind,
  };
}

export const EditorContributionsTemplate = (props: EditorContributionsTemplateProps) => {
  const saveCsv = useCsvExport();
  const { data: report, isLoading, hasError, load } = useStatsCommand<
    EditorContributionsResult,
    EditorContributionsFilter
  >(props.report);
  const [filter, setFilter] = useState<EditorContributionsFilter>(() => toFilter(props.report));

  const channels = useMemo(
    () => channelsForContentKind(props.channels, filter.kind),
    [props.channels, filter.kind],
  );

  const handleFilterChange = (next: EditorContributionsFilter) => {
    const normalized = fitRangeContentChannel(props.channels, next);
    setFilter(normalized);
    void load(normalized);
  };

  const handleRefresh = () => {
    void load(filter, { refresh: true });
  };

  const { pagePath } = props;
  const getRankedHref = useCallback((item: StatsRankedItem) => toAdminHref(item.adminPath, pagePath), [pagePath]);
  const getUserHref = useCallback((item: EditorContribution) => toAdminHref(item.adminPath, pagePath), [pagePath]);

  const { series: trend } = report;
  const series = useMemo(() => toStatsSeries(trend.series), [trend.series]);
  const userItems = useMemo(() => toEditorRankedItems(report.byUser), [report.byUser]);

  const rangeText = `${report.from} – ${report.to}`;
  const period = trend.grouping.toLowerCase();
  const fileSuffix = `${report.from}_${report.to}_${(report.kind ?? 'all').toLowerCase()}${report.channelId ? `_channel-${report.channelId}` : ''}`;
  const historyText =
    report.versionHistoryLength > 0
      ? ` It keeps the last ${numberFormat.format(report.versionHistoryLength)} versions of each language variant, so older publishes are missing.`
      : '';
  const publishedHint = `Published counts every publish (first and later ones) by the user, read from content version history. History starts when content versioning was enabled, so earlier publishes are not counted. Emails are not versioned.${historyText}`;
  const filterText = report.kind ? ` ${contentKindLabel(report.kind)} only.` : '';
  const listText =
    report.userCount > report.byUser.length
      ? ` Shows the top ${numberFormat.format(report.byUser.length)} of ${numberFormat.format(report.userCount)} users.`
      : '';

  const exportUsersCsv = () => {
    saveCsv(
      'editor-contributions-users',
      `editor-contributions-users_${fileSuffix}.csv`,
      toEditorCsv(report.byUser, report.versionHistoryEnabled, getUserHref),
    );
  };

  const exportSeriesCsv = () => {
    saveCsv(
      'editor-contributions-series',
      `editor-contributions-series_${fileSuffix}_${period}.csv`,
      toTimeSeriesCsv(trend.periods, series),
    );
  };

  const emptyMessage = 'No content was created or changed in the selected range. Try a longer range.';

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

      <div className="SimpleStats-kpis">
        <ComparisonInfoCard
          caption="Active editors"
          tooltip={`Users who created or last modified content in the selected range (${rangeText}).${filterText} Users that no longer exist are not counted.`}
          comparison={report.activeEditors}
          noun="users"
        />
        <ComparisonInfoCard
          caption="Created"
          tooltip={`Language variants created in the selected range.${filterText} ${variantHint}`}
          comparison={report.created}
          noun="items"
        />
        <ComparisonInfoCard
          caption="Last modified"
          tooltip={`Language variants whose latest change is in the selected range.${filterText} ${lastModifiedHint}`}
          comparison={report.lastModified}
          noun="items"
        />
        {report.published ? (
          <ComparisonInfoCard
            caption="Published"
            tooltip={`Publishes in the selected range.${filterText} ${publishedHint}`}
            comparison={report.published}
            noun="publishes"
          />
        ) : (
          <InfoCard caption="Published" tooltip={disabledHint} text="–" details="Content versioning is off" />
        )}
      </div>

      <StatsTile
        headline="By editor"
        description={`Users who created, last modified or published content in the selected range, most created + last modified first. ${systemUserHint} Click a user to open it.${listText}${filterText}`}
        isLoading={isLoading}
        hasError={hasError}
        isEmpty={report.byUser.length === 0}
        emptyMessage={emptyMessage}
        onExportCsv={exportUsersCsv}
        defaultView="table"
        renderChart={() => (
          <RankedBarChart
            items={userItems}
            captions={editorChartCaptions}
            ariaLabel={`Created and last modified items per user, ${rangeText}`}
            getHref={getRankedHref}
            showShare={false}
          />
        )}
        renderTable={() => (
          <EditorTable items={report.byUser} showPublished={report.versionHistoryEnabled} getAdminHref={getUserHref} />
        )}
      />

      <StatsTile
        headline="Created over time"
        description={`Language variants created per ${period}, by the users who created the most. The others are summed as Other users.${filterText}`}
        isLoading={isLoading}
        hasError={hasError}
        isEmpty={trend.total === 0}
        emptyMessage="No content was created in the selected range. Try a longer range."
        onExportCsv={exportSeriesCsv}
        renderChart={() => (
          <StackedColumnChart periods={trend.periods} series={series} ariaLabel={`Content created per user, ${rangeText}`} />
        )}
        renderTable={() => <TimeSeriesTable grouping={trend.grouping} periods={trend.periods} series={series} />}
      />

      <Callout type={CalloutType.QuickTip} placement={CalloutPlacementType.OnDesk} headline="How contributions are counted">
        <p>
          {variantHint} Created is who created the language variant. Last modified is who made its latest change.{' '}
          {lastModifiedHint} {report.versionHistoryEnabled ? publishedHint : disabledHint} Deleted items are not counted,
          also not for past dates. Page folders are left out.
        </p>
      </Callout>
    </div>
  );
};
