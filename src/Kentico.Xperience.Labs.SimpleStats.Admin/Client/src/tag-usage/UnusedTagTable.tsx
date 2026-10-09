import { Table, TableColumn, TableRow } from '@kentico/xperience-admin-components';
import React, { useMemo } from 'react';

import { CsvData, toAbsoluteUrl, toCsv } from '../shared/csv';
import { adminLinkCell } from '../shared/RankedTable';
import { column, stringCell } from '../shared/table';

/** Mirrors `TagUsageUnusedTag`: a tag no content item has. */
export interface TagUsageUnusedTag {
  /** Tag ID, unique in the list. */
  readonly key: string;
  readonly tag: string;
  readonly taxonomy: string;
  /** Parent tag title, `null` for a top-level tag. */
  readonly parent: string | null;
  /** The tag in the Taxonomies application, relative to the admin root (see `adminLinks.ts`). */
  readonly adminPath: string | null;
}

const captions = {
  tag: 'Tag',
  taxonomy: 'Taxonomy',
  parent: 'Parent tag',
} as const;

export interface UnusedTagTableProps {
  readonly items: readonly TagUsageUnusedTag[];
  readonly getAdminHref: (item: TagUsageUnusedTag) => string | null;
}

/** Unused tags as a native admin table with plain single-line cells (the tag links to the Taxonomies application). */
export const UnusedTagTable = ({ items, getAdminHref }: UnusedTagTableProps) => {
  const columns = useMemo<TableColumn[]>(
    () => [
      column('tag', captions.tag, 20, 50),
      column('taxonomy', captions.taxonomy, 16, 40),
      column('parent', captions.parent, 16, 40),
    ],
    [],
  );

  const rows = useMemo<TableRow[]>(
    () =>
      items.map((item) => ({
        identifier: item.key,
        disabled: false,
        cells: [
          adminLinkCell('tag', item.tag, getAdminHref(item)),
          stringCell('taxonomy', item.taxonomy),
          stringCell('parent', item.parent ?? '–'),
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

/** CSV of the unused tags with the table's columns plus the tag link (absolute). */
export function toUnusedTagCsv(
  items: readonly TagUsageUnusedTag[],
  getAdminHref: (item: TagUsageUnusedTag) => string | null,
): CsvData {
  return toCsv(
    [captions.tag, captions.taxonomy, captions.parent, 'URL'],
    items.map((item) => [item.tag, item.taxonomy, item.parent, toAbsoluteUrl(getAdminHref(item))]),
  );
}
