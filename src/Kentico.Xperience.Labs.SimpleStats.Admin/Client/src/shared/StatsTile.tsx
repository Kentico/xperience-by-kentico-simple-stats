import {
  Button,
  ButtonColor,
  ButtonSize,
  Card,
  Headline,
  HeadlineSize,
  IconToggleButtons,
  Spinner,
} from '@kentico/xperience-admin-components';
import React, { ReactNode, useState } from 'react';

import { useCanExport } from './exportPermission';

export type StatsTileView = 'chart' | 'table';

export interface StatsTileProps {
  readonly headline: string;
  readonly description?: string;
  readonly isLoading?: boolean;
  readonly hasError?: boolean;
  readonly isEmpty?: boolean;
  readonly emptyMessage?: string;
  /** Chart view. Omit for list-only tiles: the chart/table toggle is hidden and the table is shown. */
  readonly renderChart?: () => ReactNode;
  readonly renderTable: () => ReactNode;
  /** Called by the CSV button. Omit to hide the button. Also hidden without the Export permission. */
  readonly onExportCsv?: () => void;
  /** Tile-level controls shown before the chart/table toggle (for example a toggle that switches the list). */
  readonly headerControls?: ReactNode;
  /** View shown first. Default `chart`; use `table` for lists where the exact rows matter most. */
  readonly defaultView?: StatsTileView;
}

const viewItems = [
  { id: 'chart', icon: 'xp-graph' as const, tooltip: 'Chart', ariaLabel: 'Show chart' },
  { id: 'table', icon: 'xp-table' as const, tooltip: 'Table', ariaLabel: 'Show table' },
];

/** Report tile: headline, chart/table toggle (when it has a chart), CSV export, loading, empty and error states. */
export const StatsTile = ({
  headline,
  description,
  isLoading = false,
  hasError = false,
  isEmpty = false,
  emptyMessage = 'No data for the selected filters.',
  renderChart,
  renderTable,
  onExportCsv,
  headerControls,
  defaultView = 'chart',
}: StatsTileProps) => {
  const [view, setView] = useState<StatsTileView>(defaultView);
  const canExport = useCanExport();

  let content: ReactNode;
  if (hasError) {
    content = (
      <div className="SimpleStats-error">
        The report could not be loaded. Try again or change the filters.
      </div>
    );
  } else if (isEmpty) {
    content = <div className="SimpleStats-empty">{emptyMessage}</div>;
  } else {
    content = view === 'chart' && renderChart ? renderChart() : renderTable();
  }

  return (
    <Card
      headline={
        <div className="SimpleStats-tileHeader">
          <Headline size={HeadlineSize.M}>{headline}</Headline>
          <div className="SimpleStats-tileActions">
            {headerControls}
            {renderChart && (
              <IconToggleButtons
                items={viewItems}
                selectedItemId={view}
                onChange={(id) => setView(id as StatsTileView)}
              />
            )}
            {onExportCsv && canExport && (
              <Button
                label="Export CSV"
                icon="xp-arrow-down-line"
                color={ButtonColor.Secondary}
                size={ButtonSize.S}
                disabled={isLoading || isEmpty || hasError}
                onClick={onExportCsv}
              />
            )}
          </div>
        </div>
      }
      description={description}
    >
      <div
        className={`SimpleStats-tileBody${isLoading ? ' SimpleStats-tileBody--loading' : ''}`}
        aria-busy={isLoading}
      >
        {isLoading && (
          <div className="SimpleStats-loading">
            <Spinner />
          </div>
        )}
        {content}
      </div>
    </Card>
  );
};
