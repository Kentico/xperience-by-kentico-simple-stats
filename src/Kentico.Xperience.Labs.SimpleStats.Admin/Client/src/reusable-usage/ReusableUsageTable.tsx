import { Table, TableColumn, TableRow } from '@kentico/xperience-admin-components';
import React, { useMemo } from 'react';

import { CsvData, toAbsoluteUrl, toCsv } from '../shared/csv';
import { numberFormat } from '../shared/format';
import { adminLinkCell } from '../shared/RankedTable';
import { column, stringCell } from '../shared/table';
import { StatsRankedItem } from '../shared/types';

/** Mirrors `ReusableUsageItem`: one used reusable item with its usages per kind of referencing item. */
export interface ReusableUsageItem {
  /** Content item ID, unique in the list. */
  readonly key: string;
  readonly name: string;
  readonly contentType: string | null;
  /** "Content hub - <workspace>", built on the server. */
  readonly channel: string | null;
  readonly pages: number;
  readonly emails: number;
  readonly reusableItems: number;
  readonly headlessItems: number;
  /** Content items that reference the item (all kinds). */
  readonly usages: number;
  /** Last change of any language variant (`yyyy-MM-dd`, server date). */
  readonly lastModified: string | null;
  /** The item in the Content hub, relative to the admin root (see `adminLinks.ts`). */
  readonly adminPath: string | null;
}

export const usageCaptions = {
  name: 'Item',
  contentType: 'Content type',
  channel: 'Channel',
  pages: 'Pages',
  emails: 'Emails',
  reusableItems: 'Reusable items',
  headlessItems: 'Headless items',
  usages: 'Total usages',
  lastModified: 'Last modified',
} as const;

export interface ReusableUsageTableProps {
  readonly items: readonly ReusableUsageItem[];
  readonly getAdminHref: (item: ReusableUsageItem) => string | null;
}

/** Most used items as a native admin table with plain single-line cells (the name links to the item in the Content hub). */
export const ReusableUsageTable = ({ items, getAdminHref }: ReusableUsageTableProps) => {
  const columns = useMemo<TableColumn[]>(
    () => [
      column('name', usageCaptions.name, 24, 60),
      column('contentType', usageCaptions.contentType, 12, 30),
      column('channel', usageCaptions.channel, 14, 36),
      column('pages', usageCaptions.pages, 7, 10),
      column('emails', usageCaptions.emails, 7, 10),
      column('reusableItems', usageCaptions.reusableItems, 9, 12),
      column('headlessItems', usageCaptions.headlessItems, 9, 12),
      column('usages', usageCaptions.usages, 8, 12),
      column('lastModified', usageCaptions.lastModified, 12, 16),
    ],
    [],
  );

  const rows = useMemo<TableRow[]>(
    () =>
      items.map((item) => ({
        identifier: item.key,
        disabled: false,
        cells: [
          adminLinkCell('name', item.name, getAdminHref(item)),
          stringCell('contentType', item.contentType ?? '–'),
          stringCell('channel', item.channel ?? '–'),
          stringCell('pages', numberFormat.format(item.pages)),
          stringCell('emails', numberFormat.format(item.emails)),
          stringCell('reusableItems', numberFormat.format(item.reusableItems)),
          stringCell('headlessItems', numberFormat.format(item.headlessItems)),
          stringCell('usages', numberFormat.format(item.usages)),
          stringCell('lastModified', item.lastModified ?? '–'),
        ],
      })),
    [items, getAdminHref],
  );

  return (
    <div className="SimpleStats-tableScroll">
      <Table columns={columns} rows={rows} />
    </div>
  );
};

/** CSV of the most used items with the table's columns plus the item link (absolute). */
export function toReusableUsageCsv(
  items: readonly ReusableUsageItem[],
  getAdminHref: (item: ReusableUsageItem) => string | null,
): CsvData {
  return toCsv(
    [
      usageCaptions.name,
      usageCaptions.contentType,
      usageCaptions.channel,
      usageCaptions.pages,
      usageCaptions.emails,
      usageCaptions.reusableItems,
      usageCaptions.headlessItems,
      usageCaptions.usages,
      usageCaptions.lastModified,
      'URL',
    ],
    items.map((item) => [
      item.name,
      item.contentType,
      item.channel,
      item.pages,
      item.emails,
      item.reusableItems,
      item.headlessItems,
      item.usages,
      item.lastModified,
      toAbsoluteUrl(getAdminHref(item)),
    ]),
  );
}

/** Turns items into ranked items for `RankedBarChart` (value = total usages, secondary label = content type). */
export function toUsageRankedItems(items: readonly ReusableUsageItem[]): StatsRankedItem[] {
  return items.map((item, index) => ({
    rank: index + 1,
    key: item.key,
    label: item.name,
    secondaryLabel: item.contentType,
    value: item.usages,
    secondaryValue: null,
    share: 0,
    url: null,
    adminPath: item.adminPath,
  }));
}
