import {
  DateTimeRangeInput,
  NameToggleButton,
  NameToggleButtons,
} from '@kentico/xperience-admin-components';
import React, { ReactNode } from 'react';

import { addDays, formatDateOnly, parseDateOnly, rangeLength } from './dates';
import { ChannelSelect, RefreshControl } from './filterControls';
import { StatsChannelOption, StatsFilter, StatsGrouping } from './types';

const presetDays = [7, 30, 90] as const;
const customPresetId = 'custom';
const allTimePresetId = 'all';

const presetItems: NameToggleButton[] = [
  ...presetDays.map((days) => ({ id: String(days), label: `${days} days` })),
  { id: customPresetId, label: 'Custom' },
];

const groupingItems: NameToggleButton[] = [
  { id: 'Day', label: 'Day' },
  { id: 'Week', label: 'Week' },
  { id: 'Month', label: 'Month' },
];

export interface StatsFilterBarProps {
  readonly filter: StatsFilter;
  /** Server date (`yyyy-MM-dd`) that presets end on. */
  readonly today: string;
  /** Channel options. Hide the channel filter by passing none. */
  readonly channels?: readonly StatsChannelOption[];
  readonly onChange: (filter: StatsFilter) => void;
  /** Reloads the current filter bypassing the server cache. Hide the button by omitting it. */
  readonly onRefresh?: () => void;
  /** Shows the refresh button as in progress. */
  readonly isLoading?: boolean;
  /** ISO timestamp of when the shown data was read from the database. */
  readonly updatedAt?: string;
  /** Shows the grouping control. Hide it for reports that use the range only (for example ranked lists). */
  readonly showGrouping?: boolean;
  /** Shows the channel filter (when there are channel options). Hide it for data without a channel (for example contacts). */
  readonly showChannel?: boolean;
  /** Report-specific filter items shown after the shared filters (for example an `OptionToggle`). */
  readonly children?: ReactNode;
  /** Extra buttons shown before the refresh button (for example a link to a native application). */
  readonly actions?: ReactNode;
  /**
   * Shows an "All time" preset (the server picks the range) and whether it is selected. Omit to hide it.
   * Picking another preset or a custom range calls `onChange`; the report then turns "All time" off.
   */
  readonly allTime?: boolean;
  /** Called when the "All time" preset is picked. */
  readonly onAllTime?: () => void;
}

function getPresetId(filter: StatsFilter, today: string): string {
  const days = rangeLength(filter.from, filter.to);
  return filter.to === today && presetDays.some((d) => d === days)
    ? String(days)
    : customPresetId;
}

/**
 * Shared filters: date range presets + custom range, grouping, channel, refresh.
 * Current-state reports (no range) use `SnapshotFilterBar`.
 */
export const StatsFilterBar = ({
  filter,
  today,
  channels = [],
  onChange,
  onRefresh,
  isLoading = false,
  updatedAt,
  showGrouping = true,
  showChannel = true,
  children,
  actions,
  allTime,
  onAllTime,
}: StatsFilterBarProps) => {
  const [showCustom, setShowCustom] = React.useState(
    () => getPresetId(filter, today) === customPresetId,
  );
  const presetId = allTime ? allTimePresetId : showCustom ? customPresetId : getPresetId(filter, today);
  const items = allTime === undefined ? presetItems : [{ id: allTimePresetId, label: 'All time' }, ...presetItems];

  const handlePreset = (id: string) => {
    if (id === allTimePresetId) {
      setShowCustom(false);
      onAllTime?.();
      return;
    }
    if (id === customPresetId) {
      setShowCustom(true);
      if (allTime) {
        // Leaves "All time" with the range it showed.
        onChange(filter);
      }
      return;
    }
    setShowCustom(false);
    onChange({ ...filter, from: addDays(today, -(Number(id) - 1)), to: today });
  };

  const handleRange = (value: { from: Date; to: Date } | null) => {
    if (!value) {
      return;
    }
    const from = formatDateOnly(value.from);
    const to = formatDateOnly(value.to);
    if (from !== filter.from || to !== filter.to) {
      onChange({ ...filter, from, to });
    }
  };

  return (
    <div className="SimpleStats-filterBar">
      <div className="SimpleStats-filterItem">
        <span className="SimpleStats-label">Date range</span>
        <NameToggleButtons
          items={items}
          selectedItemId={presetId}
          onChange={handlePreset}
        />
      </div>

      {showCustom && !allTime && (
        <div className="SimpleStats-filterItem">
          <span className="SimpleStats-label">From – to</span>
          <DateTimeRangeInput
            value={{
              from: parseDateOnly(filter.from),
              to: parseDateOnly(filter.to),
            }}
            maxDate={parseDateOnly(today)}
            showTime={false}
            onChange={handleRange}
          />
        </div>
      )}

      {showGrouping && (
        <div className="SimpleStats-filterItem">
          <span className="SimpleStats-label">Group by</span>
          <NameToggleButtons
            items={groupingItems}
            selectedItemId={filter.grouping}
            onChange={(id) => onChange({ ...filter, grouping: id as StatsGrouping })}
          />
        </div>
      )}

      {showChannel && channels.length > 0 && (
        <ChannelSelect
          channels={channels}
          channelId={filter.channelId}
          onChange={(channelId) => onChange({ ...filter, channelId })}
        />
      )}

      {children}

      {onRefresh && (
        <RefreshControl
          onRefresh={onRefresh}
          isLoading={isLoading}
          updatedAt={updatedAt}
          actions={actions}
        />
      )}
    </div>
  );
};
