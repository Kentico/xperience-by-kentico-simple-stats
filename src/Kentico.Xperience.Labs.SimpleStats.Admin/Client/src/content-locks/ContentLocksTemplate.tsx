import { Callout, CalloutPlacementType, CalloutType, InfoCard } from '@kentico/xperience-admin-components';
import React, { useCallback, useMemo, useState } from 'react';

import { toAdminHref } from '../shared/adminLinks';
import { AgedItemCaptions, AgedItemTable, toDaysRankedItems } from '../shared/AgedItemTable';
import { channelsForContentKind, contentKindOptions, fitContentChannel } from '../shared/contentKinds';
import { toAgedCsv, toRankedCsv } from '../shared/csv';
import { numberFormat } from '../shared/format';
import { RankedBarChart } from '../shared/RankedBarChart';
import { RankedTable } from '../shared/RankedTable';
import { SnapshotFilterBar } from '../shared/SnapshotFilterBar';
import { StatsTile } from '../shared/StatsTile';
import {
  StatsAgedItem,
  StatsChannelOption,
  StatsRankedCaptions,
  StatsRankedItem,
  StatsRankedResult,
  StatsSnapshotFilter,
} from '../shared/types';
import { useCsvExport } from '../shared/useCsvExport';
import { useStatsCommand } from '../shared/useStatsCommand';
import '../shared/stats.css';

/** Mirrors `ContentLocksResult`. */
interface ContentLocksResult {
  /** Whether content locking is enabled (Settings → Content). Existing locks are reported either way. */
  readonly lockingEnabled: boolean;
  /** Applied content type type filter (`Website`, `Reusable`, `Email`, `Headless`), `null` for all. */
  readonly kind: string | null;
  readonly channelId: number | null;
  /** Locked language variants. */
  readonly lockedCount: number;
  /** Users holding locks (users that no longer exist count as one). */
  readonly userCount: number;
  /** Locks held for more than `oldLockDays` days. */
  readonly oldLockCount: number;
  readonly oldLockDays: number;
  /** Date of the oldest lock (`yyyy-MM-dd`, server date), `null` without locks. */
  readonly oldestLockedSince: string | null;
  /** Whole days the oldest lock is held, `null` without locks. */
  readonly oldestLockDays: number | null;
  /** Locks per user, most first. `secondaryValue` is the age of the user's oldest lock in days; `adminPath` opens the user. */
  readonly byUser: StatsRankedResult;
  /** Oldest lock first. `detail` is the user, `since` the lock date, `channel` and `lastModified` set by the server. */
  readonly items: readonly StatsAgedItem[];
  /** ISO timestamp of when the data was read from the database. */
  readonly updatedAt: string;
}

/** Mirrors `ContentLocksClientProperties`. */
interface ContentLocksTemplateProps {
  readonly report: ContentLocksResult;
  /** Website, email and headless channels. */
  readonly channels: readonly StatsChannelOption[];
  /** Path of this page relative to the admin root, used to build admin links. */
  readonly pagePath: string | null;
}

const itemCaptions: AgedItemCaptions = {
  label: 'Item',
  category: 'Content type',
  language: 'Language',
  channel: 'Channel',
  detail: 'Locked by',
  since: 'Locked since',
  lastModified: 'Last modified',
  days: 'Days locked',
};

const itemDaysCaptions: StatsRankedCaptions = {
  label: 'Item',
  secondaryLabel: 'Locked by',
  value: 'Days locked',
};

const userCaptions: StatsRankedCaptions = {
  label: 'User',
  value: 'Locked items',
  secondaryValue: 'Oldest lock (days)',
};

const disabledHint = 'Content locking is not enabled (Settings → Content).';

const variantHint = 'Locks count per language variant: an item edited in two languages holds two locks.';

const unlockHint =
  'Unlocking an item discards the holder’s unsaved changes, so ask them to use Release lock first (it saves their changes). Disabling a user account releases all of its locks.';

const releaseHint =
  'Locks are also released when the item is published, scheduled, reverted, moved to another workflow step, sent (emails) or moved to the recycle bin.';

function toFilter(report: ContentLocksResult): StatsSnapshotFilter {
  return { kind: report.kind, channelId: report.channelId };
}

function daysText(days: number): string {
  return days === 1 ? '1 day' : `${numberFormat.format(days)} days`;
}

export const ContentLocksTemplate = (props: ContentLocksTemplateProps) => {
  const saveCsv = useCsvExport();
  const { data: report, isLoading, hasError, load } = useStatsCommand<ContentLocksResult, StatsSnapshotFilter>(
    props.report,
  );
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

  const itemDays = useMemo(() => toDaysRankedItems(report.items, (item) => item.detail), [report.items]);

  const oldText = `more than ${daysText(report.oldLockDays)}`;
  const listedText =
    report.lockedCount > report.items.length
      ? ` The ${numberFormat.format(report.items.length)} oldest of ${numberFormat.format(report.lockedCount)} are listed.`
      : '';

  const fileSuffix = `${(report.kind ?? 'all').toLowerCase()}${report.channelId ? `_channel-${report.channelId}` : ''}`;

  const exportItemsCsv = () => {
    saveCsv(
      'content-locks-items',
      `content-locks-items_${fileSuffix}.csv`,
      toAgedCsv(report.items, itemCaptions, getAgedHref),
    );
  };

  const exportUsersCsv = () => {
    saveCsv(
      'content-locks-users',
      `content-locks-users_${fileSuffix}.csv`,
      toRankedCsv(report.byUser.items, userCaptions, getAdminHref),
    );
  };

  const isEmpty = report.lockedCount === 0;
  const emptyMessage = report.lockingEnabled
    ? 'No content items that match the filters are locked.'
    : `No content items are locked. ${disabledHint}`;

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

      {!report.lockingEnabled && (
        <Callout type={CalloutType.FriendlyWarning} placement={CalloutPlacementType.OnDesk} headline="Content locking is off">
          <p>
            {disabledHint}
            {isEmpty ? '' : ' Locks that still exist are listed below.'}
          </p>
        </Callout>
      )}

      <div className="SimpleStats-kpis">
        <InfoCard
          caption="Locked items"
          tooltip={`Language variants locked for editing that match the filters. ${variantHint}`}
          text={numberFormat.format(report.lockedCount)}
          details={report.lockingEnabled ? 'Locked for editing' : 'Content locking is off'}
        />
        <InfoCard
          caption="Users holding locks"
          tooltip="Users who hold at least one lock. Locks of users that no longer exist count as one Unknown user."
          text={numberFormat.format(report.userCount)}
          details={`${numberFormat.format(report.lockedCount)} locks`}
        />
        <InfoCard
          caption="Old locks"
          tooltip={`Locks held for ${oldText}. Check whether the holder is still editing the item.`}
          text={numberFormat.format(report.oldLockCount)}
          details={report.oldLockCount > 0 ? `Held ${oldText} – needs attention` : `None held ${oldText}`}
        />
        <InfoCard
          caption="Oldest lock"
          tooltip="Time since the oldest lock was taken, in whole days."
          text={report.oldestLockDays === null ? '–' : daysText(report.oldestLockDays)}
          details={report.oldestLockedSince ? `Since ${report.oldestLockedSince}` : 'No locks'}
        />
      </div>

      {report.oldLockCount > 0 && (
        <Callout type={CalloutType.FriendlyWarning} placement={CalloutPlacementType.OnDesk} headline="Old locks">
          <p>
            {`${numberFormat.format(report.oldLockCount)} ${report.oldLockCount === 1 ? 'lock is' : 'locks are'} held for ${oldText}. ${unlockHint}`}
          </p>
        </Callout>
      )}

      <StatsTile
        headline="Locked items"
        description={`Language variants locked for editing, oldest lock first. Bars over ${daysText(report.oldLockDays)} are highlighted. Click an item to open it.${listedText}`}
        isLoading={isLoading}
        hasError={hasError}
        isEmpty={isEmpty}
        emptyMessage={emptyMessage}
        onExportCsv={exportItemsCsv}
        defaultView="table"
        renderChart={() => (
          <RankedBarChart
            items={itemDays}
            captions={itemDaysCaptions}
            ariaLabel="Days locked per item"
            getHref={getAdminHref}
            showShare={false}
            highlightFrom={report.oldLockDays + 1}
          />
        )}
        renderTable={() => <AgedItemTable items={report.items} captions={itemCaptions} getAdminHref={getAgedHref} />}
      />

      <StatsTile
        headline="Locks by user"
        description="Users holding locks, most locks first, with the age of their oldest lock. Click a user to open it in the Users application."
        isLoading={isLoading}
        hasError={hasError}
        isEmpty={isEmpty}
        emptyMessage={emptyMessage}
        onExportCsv={exportUsersCsv}
        renderChart={() => (
          <RankedBarChart
            items={report.byUser.items}
            captions={userCaptions}
            ariaLabel="Locked items per user"
            getHref={getAdminHref}
          />
        )}
        renderTable={() => (
          <RankedTable items={report.byUser.items} captions={userCaptions} getAdminHref={getAdminHref} />
        )}
      />

      <Callout type={CalloutType.QuickTip} placement={CalloutPlacementType.OnDesk} headline="Releasing locks">
        <p>
          {unlockHint} {releaseHint} {variantHint} Items in all workspaces are counted.
        </p>
      </Callout>
    </div>
  );
};
