import { Table, TableColumn, TableRow } from '@kentico/xperience-admin-components';
import React, { useMemo } from 'react';

import { numberFormat } from './format';
import { adminLinkCell } from './RankedTable';
import { column, stringCell } from './table';
import { StatsAgedItem, StatsRankedItem } from './types';

/** Column captions of an aged item list, used by the table and CSV. Omit optional captions to hide the column. */
export interface AgedItemCaptions {
  /** For example "Item". */
  readonly label: string;
  /** For example "Content type". */
  readonly category?: string;
  /** For example "Language". */
  readonly language?: string;
  /** For example "Channel" (`item.channel`). */
  readonly channel?: string;
  /** For example "Workflow step". */
  readonly detail?: string;
  /** For example "Last modified". */
  readonly since: string;
  /** For example "Last modified" when `since` is another date (`item.lastModified`). */
  readonly lastModified?: string;
  /** For example "Days". */
  readonly days: string;
}

export interface AgedItemTableProps {
  readonly items: readonly StatsAgedItem[];
  readonly captions: AgedItemCaptions;
  /** Returns the link to the item's native admin page (same tab), or `null`. */
  readonly getAdminHref?: (item: StatsAgedItem) => string | null;
}

/** "Oldest first" list as a native admin table with plain single-line cells (the label links when there is an admin link). */
export const AgedItemTable = ({ items, captions, getAdminHref }: AgedItemTableProps) => {
  const columns = useMemo<TableColumn[]>(
    () => [
      column('label', captions.label, 30, 100),
      ...(captions.category ? [column('category', captions.category, 16, 40)] : []),
      ...(captions.language ? [column('language', captions.language, 10, 24)] : []),
      ...(captions.channel ? [column('channel', captions.channel, 14, 36)] : []),
      ...(captions.detail ? [column('detail', captions.detail, 16, 40)] : []),
      column('since', captions.since, 12, 20),
      ...(captions.lastModified ? [column('lastModified', captions.lastModified, 12, 20)] : []),
      column('days', captions.days, 8, 14),
    ],
    [captions],
  );

  const rows = useMemo<TableRow[]>(
    () =>
      items.map((item) => ({
        identifier: item.key,
        disabled: false,
        cells: [
          adminLinkCell('label', item.label, getAdminHref ? getAdminHref(item) : null),
          ...(captions.category ? [stringCell('category', item.category ?? '–')] : []),
          ...(captions.language ? [stringCell('language', item.language ?? '–')] : []),
          ...(captions.channel ? [stringCell('channel', item.channel ?? '–')] : []),
          ...(captions.detail ? [stringCell('detail', item.detail ?? '–')] : []),
          stringCell('since', item.since),
          ...(captions.lastModified ? [stringCell('lastModified', item.lastModified ?? '–')] : []),
          stringCell('days', numberFormat.format(item.days)),
        ],
      })),
    [items, captions, getAdminHref],
  );

  return (
    <div className="SimpleStats-tableScroll">
      <Table columns={columns} rows={rows} />
    </div>
  );
};

/** Turns aged items into ranked items (value = days), for `RankedBarChart`. */
export function toDaysRankedItems(
  items: readonly StatsAgedItem[],
  getSecondaryLabel: (item: StatsAgedItem) => string | null = (item) => item.category,
): StatsRankedItem[] {
  return items.map((item, index) => ({
    rank: index + 1,
    key: item.key,
    label: item.label,
    secondaryLabel: getSecondaryLabel(item),
    value: item.days,
    secondaryValue: null,
    share: 0,
    url: null,
    adminPath: item.adminPath ?? null,
  }));
}
