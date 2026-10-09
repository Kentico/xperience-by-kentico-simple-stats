import { NameToggleButton } from '@kentico/xperience-admin-components';

import { allKindsId, SnapshotKindOptions } from './SnapshotFilterBar';
import { StatsChannelOption, StatsFilter, StatsSnapshotFilter } from './types';

/**
 * Kind (content type type) and channel filter of the content reports.
 * Mirrors `StatsContentKinds` on the server.
 */

/** Labels of the content type types (`ClassContentTypeType` values), as named in the Content types application. */
const kindLabels: Readonly<Record<string, string>> = {
  Website: 'Pages',
  Reusable: 'Reusable content',
  Email: 'Emails',
  Headless: 'Headless items',
};

/** Channel type (`ChannelType`) of the items of each kind. Reusable items have no channel. */
const kindChannelTypes: Readonly<Record<string, string>> = {
  Website: 'Website',
  Email: 'Email',
  Headless: 'Headless',
};

const kindItems: NameToggleButton[] = [
  { id: allKindsId, label: 'All' },
  { id: 'Website', label: 'Pages' },
  { id: 'Reusable', label: 'Reusable' },
  { id: 'Email', label: 'Emails' },
  { id: 'Headless', label: 'Headless' },
];

/** Kind toggle of the snapshot filter bar. */
export const contentKindOptions: SnapshotKindOptions = { label: 'Content', items: kindItems };

/** Label of a kind ("Pages" for `Website`), "All" for `null`. */
export function contentKindLabel(kind: string | null): string {
  return kind ? (kindLabels[kind] ?? kind) : 'All';
}

/** Channels the channel filter offers for a kind: none for all kinds and reusable items. */
export function channelsForContentKind(
  channels: readonly StatsChannelOption[],
  kind: string | null,
): readonly StatsChannelOption[] {
  const type = kind ? kindChannelTypes[kind] : undefined;
  return type ? channels.filter((channel) => channel.type === type) : [];
}

function channelFits(
  channels: readonly StatsChannelOption[],
  kind: string | null,
  channelId: number | null,
): boolean {
  return channelsForContentKind(channels, kind).some((c) => c.id === channelId);
}

/**
 * Clears the channel when it does not fit the kind: the channel filter applies only to pages, emails and
 * headless items, with a channel of the matching type.
 */
export function fitContentChannel<TFilter extends StatsSnapshotFilter>(
  channels: readonly StatsChannelOption[],
  filter: TFilter,
): TFilter {
  return channelFits(channels, filter.kind, filter.channelId) ? filter : { ...filter, channelId: null };
}

/** `fitContentChannel` for filters that keep the channel in a date range (`range.channelId`). */
export function fitRangeContentChannel<TFilter extends { kind: string | null; range: StatsFilter }>(
  channels: readonly StatsChannelOption[],
  filter: TFilter,
): TFilter {
  return channelFits(channels, filter.kind, filter.range.channelId)
    ? filter
    : { ...filter, range: { ...filter.range, channelId: null } };
}
