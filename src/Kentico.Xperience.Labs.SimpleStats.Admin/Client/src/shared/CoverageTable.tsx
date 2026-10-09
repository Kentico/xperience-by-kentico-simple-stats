import { Table, TableColumn, TableRow } from '@kentico/xperience-admin-components';
import React, { useMemo } from 'react';

import { CoverageCaptions, coveredNotFlagged } from './CoverageBarChart';
import { formatShare, numberFormat } from './format';
import { column, stringCell } from './table';
import { StatsCoverageItem } from './types';

export interface CoverageTableProps {
  readonly items: readonly StatsCoverageItem[];
  /** Caption of the label column, for example "Language". */
  readonly labelCaption: string;
  readonly captions: CoverageCaptions;
  /** Caption of a column with `item.secondaryLabel` after the label (for example "Content types"). Omit to hide it. */
  readonly secondaryLabelCaption?: string;
  /** Caption of a column with `item.total` before the covered column (for example "Variants"). Omit to hide it. */
  readonly totalCaption?: string;
}

/**
 * "x of y" rows as a native admin table: label, optional secondary label, optional total, covered,
 * optional flagged (see `CoverageCaptions.flagged`), missing, share.
 */
export const CoverageTable = ({ items, labelCaption, captions, secondaryLabelCaption, totalCaption }: CoverageTableProps) => {
  const columns = useMemo<TableColumn[]>(
    () => [
      column('label', labelCaption, 20, 40),
      ...(secondaryLabelCaption ? [column('secondaryLabel', secondaryLabelCaption, 20, 60)] : []),
      ...(totalCaption ? [column('total', totalCaption, 10, 20)] : []),
      column('covered', captions.covered, 10, 20),
      ...(captions.flagged ? [column('flagged', captions.flagged, 10, 20)] : []),
      column('missing', captions.missing, 10, 20),
      column('share', 'Share', 10, 16),
    ],
    [labelCaption, captions, secondaryLabelCaption, totalCaption],
  );

  const rows = useMemo<TableRow[]>(
    () =>
      items.map((item) => ({
        identifier: item.key,
        disabled: false,
        cells: [
          stringCell('label', item.label),
          ...(secondaryLabelCaption ? [stringCell('secondaryLabel', item.secondaryLabel ?? '')] : []),
          ...(totalCaption ? [stringCell('total', numberFormat.format(item.total))] : []),
          stringCell('covered', numberFormat.format(coveredNotFlagged(item, captions))),
          ...(captions.flagged ? [stringCell('flagged', numberFormat.format(item.flagged ?? 0))] : []),
          stringCell('missing', numberFormat.format(item.missing)),
          stringCell('share', formatShare(item.share)),
        ],
      })),
    [items, captions, secondaryLabelCaption, totalCaption],
  );

  return (
    <div className="SimpleStats-tableScroll">
      <Table columns={columns} rows={rows} />
    </div>
  );
};
