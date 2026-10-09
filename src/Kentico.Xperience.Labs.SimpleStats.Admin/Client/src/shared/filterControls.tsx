import {
  Button,
  ButtonColor,
  MenuItem,
  NameToggleButton,
  NameToggleButtons,
  Select,
} from '@kentico/xperience-admin-components';
import React, { ReactNode } from 'react';

import { StatsChannelOption } from './types';

const allIdValue = '0';

const timeFormat = new Intl.DateTimeFormat(undefined, { timeStyle: 'short' });
const dateTimeFormat = new Intl.DateTimeFormat(undefined, {
  dateStyle: 'medium',
  timeStyle: 'short',
});

function formatUpdatedAt(value: string): string | null {
  const date = new Date(value);
  if (Number.isNaN(date.getTime()) || date.getFullYear() < 2000) {
    return null;
  }
  const isToday = date.toDateString() === new Date().toDateString();
  return (isToday ? timeFormat : dateTimeFormat).format(date);
}

export interface ChannelSelectProps {
  readonly channels: readonly StatsChannelOption[];
  /** Selected channel ID, `null` for all channels. */
  readonly channelId: number | null;
  readonly onChange: (channelId: number | null) => void;
}

/** Channel filter item of the filter bars: "All channels" plus one option per channel. */
export const ChannelSelect = ({ channels, channelId, onChange }: ChannelSelectProps) => (
  <IdSelect
    label="Channel"
    allLabel="All channels"
    options={channels.map((channel) => ({
      id: channel.id,
      label: channel.displayName,
      secondaryLabel: channel.type,
    }))}
    value={channelId}
    onChange={onChange}
  />
);

/** Option of an `IdSelect`. */
export interface IdSelectOption {
  /** Positive ID. */
  readonly id: number;
  readonly label: string;
  readonly secondaryLabel?: string;
}

export interface IdSelectProps {
  /** Label above the select, for example "Order status". */
  readonly label: string;
  /** Label of the option that clears the value, for example "All statuses". */
  readonly allLabel: string;
  readonly options: readonly IdSelectOption[];
  /** Selected ID, `null` for all. */
  readonly value: number | null;
  readonly onChange: (value: number | null) => void;
}

/**
 * Filter bar item with a select of ID options plus an "all" option (for example channels or order statuses).
 * Use it instead of an `OptionToggle` when the options come from project data and can be many.
 */
export const IdSelect = ({ label, allLabel, options, value, onChange }: IdSelectProps) => {
  const handleChange = (selected?: string) => {
    const id = Number(selected ?? allIdValue);
    onChange(id > 0 ? id : null);
  };

  return (
    <div className="SimpleStats-filterItem SimpleStats-filterItem--channel">
      <Select label={label} value={String(value ?? allIdValue)} onChange={handleChange}>
        <MenuItem primaryLabel={allLabel} value={allIdValue} />
        {options.map((option) => (
          <MenuItem
            key={option.id}
            primaryLabel={option.label}
            secondaryLabel={option.secondaryLabel}
            value={String(option.id)}
          />
        ))}
      </Select>
    </div>
  );
};

/** Option of a `TextSelect`. */
export interface TextSelectOption {
  /** Any text, also empty. */
  readonly value: string;
  readonly label: string;
}

export interface TextSelectProps {
  /** Label above the select, for example "Source". */
  readonly label: string;
  /** Label of the option that clears the value, for example "All sources". */
  readonly allLabel: string;
  readonly options: readonly TextSelectOption[];
  /** Selected value, `null` for all. */
  readonly value: string | null;
  readonly onChange: (value: string | null) => void;
}

/** Prefix of option values, so any text (also an empty string) is told apart from the "all" option. */
const textOptionPrefix = 'v:';

/**
 * Filter bar item with a select of text options plus an "all" option (for example UTM sources from project data).
 * Like `IdSelect`, for values that are free text instead of IDs.
 */
export const TextSelect = ({ label, allLabel, options, value, onChange }: TextSelectProps) => {
  const handleChange = (selected?: string) => {
    onChange(selected?.startsWith(textOptionPrefix) ? selected.slice(textOptionPrefix.length) : null);
  };

  return (
    <div className="SimpleStats-filterItem SimpleStats-filterItem--channel">
      <Select label={label} value={value === null ? allIdValue : textOptionPrefix + value} onChange={handleChange}>
        <MenuItem primaryLabel={allLabel} value={allIdValue} />
        {options.map((option) => (
          <MenuItem key={option.value} primaryLabel={option.label} value={textOptionPrefix + option.value} />
        ))}
      </Select>
    </div>
  );
};

/** Toggle item id that stands for "all" (`null` value). */
export const allOptionId = 'all';

export interface OptionToggleProps {
  /** Label above the toggle, for example "Event type". */
  readonly label: string;
  /** Toggle items. Use `allOptionId` for the item that clears the value. */
  readonly items: readonly NameToggleButton[];
  /** Selected item id, `null` for the `allOptionId` item. */
  readonly value: string | null;
  readonly onChange: (value: string | null) => void;
}

/** Filter bar item with a labeled option toggle, for example a kind or type filter with an "All" item. */
export const OptionToggle = ({ label, items, value, onChange }: OptionToggleProps) => (
  <div className="SimpleStats-filterItem">
    <span className="SimpleStats-label">{label}</span>
    <NameToggleButtons
      items={[...items]}
      selectedItemId={value ?? allOptionId}
      onChange={(id) => onChange(id === allOptionId ? null : id)}
    />
  </div>
);

export interface RefreshControlProps {
  /** Reloads the current filter bypassing the server cache. */
  readonly onRefresh: () => void;
  /** Shows the refresh button as in progress. */
  readonly isLoading: boolean;
  /** ISO timestamp of when the shown data was read from the database. */
  readonly updatedAt?: string;
  /** Extra buttons shown before the refresh button (for example a link to a native application). */
  readonly actions?: ReactNode;
}

/** Filter bar item at the end of the bar: "Updated <time>", optional actions and the refresh button. */
export const RefreshControl = ({ onRefresh, isLoading, updatedAt, actions }: RefreshControlProps) => {
  const updatedText = updatedAt ? formatUpdatedAt(updatedAt) : null;

  return (
    <div className="SimpleStats-filterItem SimpleStats-filterItem--refresh">
      {updatedText && <span className="SimpleStats-updated">Updated {updatedText}</span>}
      {actions}
      <Button
        label="Refresh"
        icon="xp-rotate-right"
        color={ButtonColor.Secondary}
        title="Reload data from the database"
        inProgress={isLoading}
        onClick={onRefresh}
      />
    </div>
  );
};
