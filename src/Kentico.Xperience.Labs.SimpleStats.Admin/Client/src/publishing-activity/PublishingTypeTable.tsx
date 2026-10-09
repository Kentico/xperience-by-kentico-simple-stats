import { Table, TableColumn, TableRow } from '@kentico/xperience-admin-components';
import React, { useMemo } from 'react';

import { CsvData, toAbsoluteUrl, toCsv } from '../shared/csv';
import { numberFormat } from '../shared/format';
import { adminLinkCell } from '../shared/RankedTable';
import { column, stringCell } from '../shared/table';
import { StatsRankedItem } from '../shared/types';

/** Mirrors `PublishingActivityContentType`: activity of one content type in the range. */
export interface PublishingActivityContentType {
  /** Unique in the list. */
  readonly key: string;
  readonly contentType: string;
  readonly created: number;
  readonly firstPublished: number;
  /** `null` when content version history is disabled. */
  readonly updates: number | null;
  /** Median days from created to first published, `null` without first publishes. */
  readonly medianDaysToPublish: number | null;
  /** The content type in the Content types application, relative to the admin root. */
  readonly adminPath: string | null;
}

export interface PublishingTypeTableProps {
  readonly items: readonly PublishingActivityContentType[];
  /** Shows the updates column (content version history is enabled). */
  readonly showUpdates: boolean;
  readonly getAdminHref: (item: PublishingActivityContentType) => string | null;
}

const captions = {
  contentType: 'Content type',
  created: 'Created',
  firstPublished: 'First published',
  updates: 'Updates',
  median: 'Median days to publish',
} as const;

const daysFormat = new Intl.NumberFormat(undefined, { maximumFractionDigits: 1 });

/** Formats days to publish with one decimal, "–" when unknown. */
export function formatDays(days: number | null): string {
  return days === null ? '–' : daysFormat.format(days);
}

/** Content types with created, first published, updates and median days to publish (the type links to the content type). */
export const PublishingTypeTable = ({ items, showUpdates, getAdminHref }: PublishingTypeTableProps) => {
  const columns = useMemo<TableColumn[]>(
    () => [
      column('contentType', captions.contentType, 24, 60),
      column('created', captions.created, 10, 14),
      column('firstPublished', captions.firstPublished, 12, 16),
      ...(showUpdates ? [column('updates', captions.updates, 10, 14)] : []),
      column('median', captions.median, 14, 20),
    ],
    [showUpdates],
  );

  const rows = useMemo<TableRow[]>(
    () =>
      items.map((item) => ({
        identifier: item.key,
        disabled: false,
        cells: [
          adminLinkCell('contentType', item.contentType, getAdminHref(item)),
          stringCell('created', numberFormat.format(item.created)),
          stringCell('firstPublished', numberFormat.format(item.firstPublished)),
          ...(showUpdates ? [stringCell('updates', numberFormat.format(item.updates ?? 0))] : []),
          stringCell('median', formatDays(item.medianDaysToPublish)),
        ],
      })),
    [items, showUpdates, getAdminHref],
  );

  return (
    <div className="SimpleStats-tableScroll">
      <Table columns={columns} rows={rows} />
    </div>
  );
};

/** CSV of the content types with the table's columns plus the content type link (absolute). */
export function toPublishingTypeCsv(
  items: readonly PublishingActivityContentType[],
  showUpdates: boolean,
  getAdminHref: (item: PublishingActivityContentType) => string | null,
): CsvData {
  return toCsv(
    [
      captions.contentType,
      captions.created,
      captions.firstPublished,
      ...(showUpdates ? [captions.updates] : []),
      captions.median,
      'URL',
    ],
    items.map((item) => [
      item.contentType,
      item.created,
      item.firstPublished,
      ...(showUpdates ? [item.updates ?? 0] : []),
      item.medianDaysToPublish,
      toAbsoluteUrl(getAdminHref(item)),
    ]),
  );
}

/** Turns content types into ranked items for `RankedBarChart` (value = created, secondary value = first published). */
export function toCreatedRankedItems(items: readonly PublishingActivityContentType[]): StatsRankedItem[] {
  return [...items]
    .sort((a, b) => b.created - a.created || a.contentType.localeCompare(b.contentType))
    .filter((item) => item.created > 0)
    .map((item, index) => ({
      rank: index + 1,
      key: item.key,
      label: item.contentType,
      secondaryLabel: null,
      value: item.created,
      secondaryValue: item.firstPublished,
      share: 0,
      url: null,
      adminPath: item.adminPath,
    }));
}
