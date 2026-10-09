import { Table, TableColumn, TableRow } from '@kentico/xperience-admin-components';
import React, { useMemo } from 'react';

import { CsvData, toAbsoluteUrl, toCsv } from '../shared/csv';
import { numberFormat } from '../shared/format';
import { adminLinkCell } from '../shared/RankedTable';
import { column, stringCell } from '../shared/table';
import { StatsRankedItem } from '../shared/types';

/** Mirrors `PageFreshnessItem`: one page language variant. */
export interface PageFreshnessItem {
  /** Unique in the list. */
  readonly key: string;
  readonly name: string;
  /** Website channel display name. */
  readonly channel: string | null;
  /** Language display name. */
  readonly language: string;
  /** Position in the content tree (the same in all languages), not the live URL. */
  readonly treePath: string;
  /** Last change of the latest version (`yyyy-MM-dd`, server date). */
  readonly lastModified: string;
  /** First publish (`yyyy-MM-dd`, server date), `null` when not stored. */
  readonly firstPublished: string | null;
  /** Page visits in the range. */
  readonly visits: number;
  /** Distinct contacts with a page visit in the range. */
  readonly visitors: number;
  /** The page's Content tab, relative to the admin root (see `adminLinks.ts`). */
  readonly adminPath: string | null;
}

/** Which columns a page list shows (table and CSV). */
export type PageFreshnessColumns = 'visits' | 'firstPublished';

export interface PageFreshnessTableProps {
  readonly items: readonly PageFreshnessItem[];
  /** `visits`: last modified, visits and visitors; `firstPublished`: first published and last modified. */
  readonly columns: PageFreshnessColumns;
  readonly getAdminHref: (item: PageFreshnessItem) => string | null;
}

const captions = {
  name: 'Page',
  channel: 'Channel',
  language: 'Language',
  treePath: 'Tree path',
  firstPublished: 'First published',
  lastModified: 'Last modified',
  visits: 'Visits',
  visitors: 'Visitors',
} as const;

/** Page list as a native admin table with plain single-line cells (the name links to the page). */
export const PageFreshnessTable = ({ items, columns: kind, getAdminHref }: PageFreshnessTableProps) => {
  const columns = useMemo<TableColumn[]>(
    () => [
      column('name', captions.name, 24, 60),
      column('language', captions.language, 10, 20),
      column('channel', captions.channel, 12, 30),
      column('treePath', captions.treePath, 20, 60),
      ...(kind === 'firstPublished' ? [column('firstPublished', captions.firstPublished, 12, 16)] : []),
      column('lastModified', captions.lastModified, 12, 16),
      ...(kind === 'visits'
        ? [column('visits', captions.visits, 8, 12), column('visitors', captions.visitors, 8, 12)]
        : []),
    ],
    [kind],
  );

  const rows = useMemo<TableRow[]>(
    () =>
      items.map((item) => ({
        identifier: item.key,
        disabled: false,
        cells: [
          adminLinkCell('name', item.name, getAdminHref(item)),
          stringCell('language', item.language),
          stringCell('channel', item.channel ?? '–'),
          stringCell('treePath', item.treePath),
          ...(kind === 'firstPublished' ? [stringCell('firstPublished', item.firstPublished ?? '–')] : []),
          stringCell('lastModified', item.lastModified),
          ...(kind === 'visits'
            ? [stringCell('visits', numberFormat.format(item.visits)), stringCell('visitors', numberFormat.format(item.visitors))]
            : []),
        ],
      })),
    [items, kind, getAdminHref],
  );

  return (
    <div className="SimpleStats-tableScroll">
      <Table columns={columns} rows={rows} />
    </div>
  );
};

/** CSV of a page list with the table's columns plus the page link (absolute). */
export function toPageFreshnessCsv(
  items: readonly PageFreshnessItem[],
  kind: PageFreshnessColumns,
  getAdminHref: (item: PageFreshnessItem) => string | null,
): CsvData {
  return toCsv(
    [
      captions.name,
      captions.language,
      captions.channel,
      captions.treePath,
      ...(kind === 'firstPublished' ? [captions.firstPublished] : []),
      captions.lastModified,
      ...(kind === 'visits' ? [captions.visits, captions.visitors] : []),
      'URL',
    ],
    items.map((item) => [
      item.name,
      item.language,
      item.channel,
      item.treePath,
      ...(kind === 'firstPublished' ? [item.firstPublished] : []),
      item.lastModified,
      ...(kind === 'visits' ? [item.visits, item.visitors] : []),
      toAbsoluteUrl(getAdminHref(item)),
    ]),
  );
}

/** Turns pages into ranked items for `RankedBarChart` (value = visits, secondary value = visitors, secondary label = language). */
export function toVisitRankedItems(items: readonly PageFreshnessItem[]): StatsRankedItem[] {
  return items.map((item, index) => ({
    rank: index + 1,
    key: item.key,
    label: item.name,
    secondaryLabel: item.language,
    value: item.visits,
    secondaryValue: item.visitors,
    share: 0,
    url: null,
    adminPath: item.adminPath,
  }));
}
