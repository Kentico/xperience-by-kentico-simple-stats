import { Link } from '@kentico/xperience-admin-components';
import React from 'react';

import { formatRowDate } from './format';
import { OverviewRow } from './types';

export interface OverviewRowsProps {
  readonly rows: readonly OverviewRow[];
  readonly emptyMessage: string;
  readonly getHref: (adminPath: string | null) => string | null;
  /** Rows in the full report. When more than shown, a "more" line is added. */
  readonly total?: number;
}

/** Short list of an Overview card: label (link), detail and date. */
export const OverviewRows = ({ rows, emptyMessage, getHref, total }: OverviewRowsProps) => {
  if (rows.length === 0) {
    return <div className="SimpleStats-empty">{emptyMessage}</div>;
  }

  const more = total !== undefined ? total - rows.length : 0;

  return (
    <>
      <ul className="SimpleStats-overviewList SimpleStats-overviewTable">
        {rows.map((row) => {
          const href = getHref(row.adminPath);
          // Always render all three cells so the columns line up across rows.
          return (
            <li key={row.key} className="SimpleStats-overviewListRow">
              <span className="SimpleStats-overviewListDate">{row.date ? formatRowDate(row.date) : ''}</span>
              <span className="SimpleStats-overviewListText">
                {href ? <Link href={href} text={row.label} ellipsis /> : row.label}
              </span>
              <span className="SimpleStats-overviewListDetail">{row.detail ?? ''}</span>
            </li>
          );
        })}
      </ul>
      {more > 0 && <div className="SimpleStats-overviewMore">and {more} more in the full report</div>}
    </>
  );
};
