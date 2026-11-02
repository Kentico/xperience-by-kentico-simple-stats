import { Link, SimpleStatusError, SimpleStatusWarning } from '@kentico/xperience-admin-components';
import React from 'react';

import { OverviewAttentionItem } from './types';

export interface AttentionListProps {
  /** Items of all loaded tiles. Sorted here: problems first, then cautions. */
  readonly items: readonly OverviewAttentionItem[];
  readonly getHref: (adminPath: string | null) => string | null;
}

const toneOrder: Record<string, number> = { Problem: 0, Caution: 1 };

/**
 * "Needs attention" list. The tone uses the native admin status elements (error = problem, warning = caution),
 * the same traffic-light meaning as the `StatsTone` chart colors.
 */
export const AttentionList = ({ items, getHref }: AttentionListProps) => {
  const sorted = [...items].sort((a, b) => (toneOrder[a.tone] ?? 2) - (toneOrder[b.tone] ?? 2));

  return (
    <ul className="SimpleStats-overviewList">
      {sorted.map((item) => {
        const href = getHref(item.adminPath);
        return (
          <li key={item.key} className="SimpleStats-overviewListRow">
            <span className="SimpleStats-overviewStatus">
              {item.tone === 'Problem' ? (
                <SimpleStatusError content={{ label: 'Problem' }} />
              ) : (
                <SimpleStatusWarning content={{ label: 'Check' }} />
              )}
            </span>
            <span className="SimpleStats-overviewListText">{item.text}</span>
            {href && (
              <span className="SimpleStats-overviewListAction">
                <Link href={href} text="View details" />
              </span>
            )}
          </li>
        );
      })}
    </ul>
  );
};
