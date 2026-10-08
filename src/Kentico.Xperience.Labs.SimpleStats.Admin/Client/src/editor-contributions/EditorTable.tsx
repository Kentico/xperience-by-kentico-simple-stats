import { Table, TableColumn, TableRow } from '@kentico/xperience-admin-components';
import React, { useMemo } from 'react';

import { CsvData, toAbsoluteUrl, toCsv } from '../shared/csv';
import { numberFormat } from '../shared/format';
import { adminLinkCell } from '../shared/RankedTable';
import { column, stringCell } from '../shared/table';
import { StatsRankedCaptions, StatsRankedItem } from '../shared/types';

/** Mirrors `EditorContribution`: contributions of one administration user in the range. */
export interface EditorContribution {
  /** `user:{id}`, or `(unknown)` for users that no longer exist. Unique in the list. */
  readonly key: string;
  readonly user: string;
  /** The product's service user (imports, automation) or the public user. */
  readonly isSystemUser: boolean;
  readonly created: number;
  readonly lastModified: number;
  /** `null` when content version history is disabled. */
  readonly published: number | null;
  /** Distinct content types of these contributions. */
  readonly contentTypes: number;
  /** The user in the Users application, relative to the admin root. `null` for unknown users. */
  readonly adminPath: string | null;
}

export interface EditorTableProps {
  readonly items: readonly EditorContribution[];
  /** Shows the published column (content version history is enabled). */
  readonly showPublished: boolean;
  readonly getAdminHref: (item: EditorContribution) => string | null;
}

const captions = {
  rank: '#',
  user: 'User',
  account: 'Account',
  created: 'Created',
  lastModified: 'Last modified',
  published: 'Published',
  contentTypes: 'Content types',
} as const;

const systemUserText = 'System user';

/** Chart captions: the bar is created + last modified, the tooltip shows both. */
export const editorChartCaptions: StatsRankedCaptions = {
  label: captions.user,
  secondaryLabel: captions.account,
  value: 'Created + last modified',
  secondaryValue: captions.created,
  tertiaryValue: captions.lastModified,
};

/** Users with created, last modified, published (with version history) and content types (the user links to the user). */
export const EditorTable = ({ items, showPublished, getAdminHref }: EditorTableProps) => {
  const columns = useMemo<TableColumn[]>(
    () => [
      column('rank', captions.rank, 6, 8),
      column('user', captions.user, 24, 60),
      column('account', captions.account, 12, 16),
      column('created', captions.created, 10, 14),
      column('lastModified', captions.lastModified, 12, 16),
      ...(showPublished ? [column('published', captions.published, 10, 14)] : []),
      column('contentTypes', captions.contentTypes, 12, 16),
    ],
    [showPublished],
  );

  const rows = useMemo<TableRow[]>(
    () =>
      items.map((item, index) => ({
        identifier: item.key,
        disabled: false,
        cells: [
          stringCell('rank', String(index + 1)),
          adminLinkCell('user', item.user, getAdminHref(item)),
          stringCell('account', item.isSystemUser ? systemUserText : ''),
          stringCell('created', numberFormat.format(item.created)),
          stringCell('lastModified', numberFormat.format(item.lastModified)),
          ...(showPublished ? [stringCell('published', numberFormat.format(item.published ?? 0))] : []),
          stringCell('contentTypes', numberFormat.format(item.contentTypes)),
        ],
      })),
    [items, showPublished, getAdminHref],
  );

  return (
    <div className="SimpleStats-tableScroll">
      <Table columns={columns} rows={rows} />
    </div>
  );
};

/** CSV of the users with the table's columns plus the user link (absolute). */
export function toEditorCsv(
  items: readonly EditorContribution[],
  showPublished: boolean,
  getAdminHref: (item: EditorContribution) => string | null,
): CsvData {
  return toCsv(
    [
      'Rank',
      captions.user,
      captions.account,
      captions.created,
      captions.lastModified,
      ...(showPublished ? [captions.published] : []),
      captions.contentTypes,
      'URL',
    ],
    items.map((item, index) => [
      index + 1,
      item.user,
      item.isSystemUser ? systemUserText : '',
      item.created,
      item.lastModified,
      ...(showPublished ? [item.published ?? 0] : []),
      item.contentTypes,
      toAbsoluteUrl(getAdminHref(item)),
    ]),
  );
}

/**
 * Turns users into ranked items for `RankedBarChart` (value = created + last modified, secondary value = created,
 * third value = last modified), in the server's order. Users with publishes only are left out of the chart.
 */
export function toEditorRankedItems(items: readonly EditorContribution[]): StatsRankedItem[] {
  return items
    .filter((item) => item.created + item.lastModified > 0)
    .map((item, index) => ({
      rank: index + 1,
      key: item.key,
      label: item.user,
      secondaryLabel: item.isSystemUser ? systemUserText : null,
      value: item.created + item.lastModified,
      secondaryValue: item.created,
      tertiaryValue: item.lastModified,
      share: 0,
      url: null,
      adminPath: item.adminPath,
    }));
}
