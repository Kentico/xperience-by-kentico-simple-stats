import { Table, TableColumn, TableRow } from '@kentico/xperience-admin-components';
import React, { useMemo } from 'react';

import { CsvData, toAbsoluteUrl, toCsv } from '../shared/csv';
import { formatServerDateTimeCsv, parseServerDateTime } from '../shared/dates';
import { adminLinkCell } from '../shared/RankedTable';
import { column, stringCell } from '../shared/table';

/** Mirrors `PublishingAction` (sent as its name). */
export type PublishingAction = 'Publish' | 'Unpublish' | 'Send';

/** Mirrors `PublishingCalendarItem`: one scheduled event (publish, unpublish or email send) or recently published language variant. */
export interface PublishingCalendarItem {
  /** Unique in the list. */
  readonly key: string;
  /** Scheduled time, or last publish time (server time, no time zone). */
  readonly when: string;
  /** `null` for recently published variants. */
  readonly action: PublishingAction | null;
  readonly label: string;
  readonly contentType: string;
  readonly language: string;
  /** Channel; for reusable items "Content hub - <workspace>" (set by the server). `null` when unknown. */
  readonly channel: string | null;
  readonly modifiedBy: string | null;
  /** Native admin page of the item, relative to the admin root (see `adminLinks.ts`). */
  readonly adminPath?: string | null;
}

/** Column captions of a publishing list. Omit `action` to hide the column (recently published). */
export interface PublishingItemCaptions {
  /** For example "Scheduled for". */
  readonly when: string;
  readonly action?: string;
}

const actionLabels: Readonly<Record<PublishingAction, string>> = {
  Publish: 'Publish',
  Unpublish: 'Unpublish',
  Send: 'Send',
};

const dateTimeFormat = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' });

/** Scheduled or publish time in server time, for example "Oct 6, 2026, 2:30 PM". */
export function formatWhen(value: string): string {
  const date = parseServerDateTime(value);
  return date ? dateTimeFormat.format(date) : value;
}

function actionLabel(action: PublishingAction | null): string {
  return action ? (actionLabels[action] ?? String(action)) : '–';
}

export interface PublishingItemTableProps {
  readonly items: readonly PublishingCalendarItem[];
  readonly captions: PublishingItemCaptions;
  /** Returns the link to the item where it is edited (same tab), or `null`. */
  readonly getAdminHref: (item: PublishingCalendarItem) => string | null;
}

/** Scheduled events or recently published variants as a native admin table with plain single-line cells. */
export const PublishingItemTable = ({ items, captions, getAdminHref }: PublishingItemTableProps) => {
  const columns = useMemo<TableColumn[]>(
    () => [
      column('when', captions.when, 14, 22),
      ...(captions.action ? [column('action', captions.action, 8, 12)] : []),
      column('label', 'Item', 24, 100),
      column('contentType', 'Content type', 14, 40),
      column('language', 'Language', 10, 20),
      column('channel', 'Channel', 12, 30),
      column('modifiedBy', 'Last modified by', 12, 30),
    ],
    [captions],
  );

  const rows = useMemo<TableRow[]>(
    () =>
      items.map((item) => ({
        identifier: item.key,
        disabled: false,
        cells: [
          stringCell('when', formatWhen(item.when)),
          ...(captions.action ? [stringCell('action', actionLabel(item.action))] : []),
          adminLinkCell('label', item.label, getAdminHref(item)),
          stringCell('contentType', item.contentType || '–'),
          stringCell('language', item.language),
          stringCell('channel', item.channel ?? '–'),
          stringCell('modifiedBy', item.modifiedBy ?? '–'),
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

/** CSV of a publishing list: time (`yyyy-MM-dd HH:mm`, server time), optional action, item, type, language, channel, last modified by, URL. */
export function toPublishingCsv(
  items: readonly PublishingCalendarItem[],
  captions: PublishingItemCaptions,
  getAdminHref: (item: PublishingCalendarItem) => string | null,
): CsvData {
  return toCsv(
    [
      `${captions.when} (server time)`,
      ...(captions.action ? [captions.action] : []),
      'Item',
      'Content type',
      'Language',
      'Channel',
      'Last modified by',
      'URL',
    ],
    items.map((item) => [
      formatServerDateTimeCsv(item.when),
      ...(captions.action ? [actionLabel(item.action)] : []),
      item.label,
      item.contentType,
      item.language,
      item.channel,
      item.modifiedBy,
      toAbsoluteUrl(getAdminHref(item)),
    ]),
  );
}
