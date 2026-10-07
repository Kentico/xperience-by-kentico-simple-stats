import { NameToggleButton } from '@kentico/xperience-admin-components';
import React from 'react';

import { allOptionId, ChannelSelect, OptionToggle, RefreshControl } from './filterControls';
import { StatsChannelOption, StatsSnapshotFilter } from './types';

/** Toggle item id that stands for "all" (`kind: null`). */
export const allKindsId = allOptionId;

export interface SnapshotKindOptions {
  /** Label above the toggle, for example "Content type type". */
  readonly label: string;
  /** Toggle items. Use `allKindsId` for the item that clears the kind. */
  readonly items: readonly NameToggleButton[];
}

export interface SnapshotWindowOptions {
  /** Label above the toggle, for example "Next". */
  readonly label: string;
  /** Windows in days, for example 7, 30 and 90. Shown as "7 days". */
  readonly days: readonly number[];
}

export interface SnapshotFilterBarProps {
  readonly filter: StatsSnapshotFilter;
  readonly onChange: (filter: StatsSnapshotFilter) => void;
  /** Window toggle (days ahead or back, `filter.window`). Omit to hide it. */
  readonly windows?: SnapshotWindowOptions;
  /** Kind toggle (for example content kinds). Omit to hide it. */
  readonly kinds?: SnapshotKindOptions;
  /** Channel options. Hide the channel filter by passing none. */
  readonly channels?: readonly StatsChannelOption[];
  /** Reloads the current filter bypassing the server cache. */
  readonly onRefresh: () => void;
  /** Shows the refresh button as in progress. */
  readonly isLoading?: boolean;
  /** ISO timestamp of when the shown data was read from the database. */
  readonly updatedAt?: string;
}

/**
 * Filters of a current-state (snapshot) report: optional window toggle, kind toggle, channel, refresh.
 * Same layout as `StatsFilterBar`, without date range or grouping.
 */
export const SnapshotFilterBar = ({
  filter,
  onChange,
  windows,
  kinds,
  channels = [],
  onRefresh,
  isLoading = false,
  updatedAt,
}: SnapshotFilterBarProps) => (
  <div className="SimpleStats-filterBar">
    {windows && (
      <OptionToggle
        label={windows.label}
        items={windows.days.map((days) => ({ id: String(days), label: `${days} days` }))}
        value={filter.window ? String(filter.window) : null}
        onChange={(id) => onChange({ ...filter, window: id ? Number(id) : null })}
      />
    )}

    {kinds && (
      <OptionToggle
        label={kinds.label}
        items={kinds.items}
        value={filter.kind}
        onChange={(kind) => onChange({ ...filter, kind })}
      />
    )}

    {channels.length > 0 && (
      <ChannelSelect
        channels={channels}
        channelId={filter.channelId}
        onChange={(channelId) => onChange({ ...filter, channelId })}
      />
    )}

    <RefreshControl onRefresh={onRefresh} isLoading={isLoading} updatedAt={updatedAt} />
  </div>
);
