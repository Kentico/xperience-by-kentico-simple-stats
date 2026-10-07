import * as am5 from '@amcharts/amcharts5';
import * as am5xy from '@amcharts/amcharts5/xy';
import { Colors } from '@kentico/xperience-admin-components';
import React, { useId, useLayoutEffect, useMemo } from 'react';

import { chartLogoSpace, createChartRoot, getChartTokens, getSeriesPalette, resolveToken } from './chartTheme';
import { formatShare, numberFormat } from './format';
import { StatsCoverageItem } from './types';
import { useStableValue } from './useStableValue';

export interface CoverageCaptions {
  /** Caption of the covered part, for example "With variant". */
  readonly covered: string;
  /** Caption of the missing part, for example "Missing". */
  readonly missing: string;
  /** Noun of the total in tooltips, for example "items". */
  readonly totalNoun: string;
  /**
   * Caption of the flagged part of the covered rows (`item.flagged`), for example "Outdated". When set, the covered part
   * shows `covered - flagged` and the flagged part is its own segment in the alert color. Omit for plain coverage.
   */
  readonly flagged?: string;
}

/** Covered rows without the flagged ones (when `captions.flagged` is set), else all covered rows. */
export function coveredNotFlagged(item: StatsCoverageItem, captions: CoverageCaptions): number {
  return captions.flagged ? Math.max(item.covered - (item.flagged ?? 0), 0) : item.covered;
}

export interface CoverageBarChartProps {
  readonly items: readonly StatsCoverageItem[];
  readonly captions: CoverageCaptions;
  /** Accessible name for the chart. */
  readonly ariaLabel: string;
}

interface ChartRow {
  readonly key: string;
  readonly label: string;
  readonly covered: number;
  readonly flagged: number;
  readonly missing: number;
  readonly tooltip: string;
}

const rowHeight = 40;
const chartPadding = 88;
const minHeight = 160;
const labelWidth = 160;

/** amCharts reads `[...]` as text formatting; double the brackets so data shows as typed. */
function escapeChartText(text: string): string {
  return text.replace(/\[/g, '[[').replace(/\]/g, ']]');
}

/**
 * Horizontal 100% stacked bar chart (amCharts 5): one bar per row, covered part in the first
 * series color, optional flagged part (see `CoverageCaptions.flagged`) in the alert color and
 * the missing part in the disabled background color (like an empty track).
 * The root is created in `useLayoutEffect` and disposed on unmount or data change.
 */
export const CoverageBarChart = React.memo(function CoverageBarChart({ items, captions, ariaLabel }: CoverageBarChartProps) {
  const chartId = `stats-chart-${useId().replace(/:/g, '')}`;

  const rows = useMemo<ChartRow[]>(
    () =>
      items.map((item) => {
        const covered = coveredNotFlagged(item, captions);
        const flagged = captions.flagged ? (item.flagged ?? 0) : 0;
        const coveredLine = captions.flagged
          ? `${captions.covered}: ${numberFormat.format(covered)}`
          : `${captions.covered}: ${numberFormat.format(item.covered)} of ${numberFormat.format(item.total)} ${captions.totalNoun} (${formatShare(item.share)})`;
        return {
          key: item.key,
          label: item.label,
          covered,
          flagged,
          missing: item.missing,
          tooltip: [
            `[bold]${escapeChartText(item.label)}[/]`,
            coveredLine,
            ...(captions.flagged ? [`${captions.flagged}: ${numberFormat.format(flagged)}`] : []),
            `${captions.missing}: ${numberFormat.format(item.missing)}`,
            ...(captions.flagged ? [`Total: ${numberFormat.format(item.total)} ${captions.totalNoun}`] : []),
          ].join('\n'),
        };
      }),
    [items, captions],
  );
  // Rebuild the chart only when the rows change by content, not on every new prop identity.
  const data = useStableValue(rows);

  const height = Math.max(minHeight, data.length * rowHeight + chartPadding) + chartLogoSpace;

  useLayoutEffect(() => {
    const root = createChartRoot(chartId);
    root.numberFormatter.set('numberFormat', '#,###');

    const tokens = getChartTokens(root.dom);
    const coveredColor = getSeriesPalette(root.dom)[0];
    const missingColor = resolveToken(Colors.BackgroundDisabled, root.dom);
    const flaggedColor = resolveToken(Colors.AlertBackgroundHighEmphasis, root.dom);

    const chart = root.container.children.push(
      am5xy.XYChart.new(root, {
        panX: false,
        panY: false,
        wheelX: 'none',
        wheelY: 'none',
        layout: root.verticalLayout,
        paddingLeft: 0,
        paddingRight: 0,
      }),
    );
    chart.zoomOutButton.set('forceHidden', true);

    const createTooltip = () => {
      const tooltip = am5.Tooltip.new(root, {
        getFillFromSprite: false,
        autoTextColor: false,
        pointerOrientation: 'horizontal',
      });
      tooltip.get('background')?.setAll({ fill: tokens.tooltip, stroke: tokens.tooltip });
      tooltip.label.setAll({ fill: tokens.tooltipText, maxWidth: 480, oversizedBehavior: 'wrap' });
      return tooltip;
    };

    // Category axis on Y, inversed so the first row is on top.
    const yRenderer = am5xy.AxisRendererY.new(root, {
      inversed: true,
      minGridDistance: 1,
      cellStartLocation: 0.2,
      cellEndLocation: 0.8,
    });
    yRenderer.grid.template.set('visible', false);
    yRenderer.labels.template.setAll({
      fill: tokens.text,
      fontSize: 13,
      oversizedBehavior: 'truncate',
      ellipsis: '…',
      maxWidth: labelWidth,
    });
    yRenderer.labels.template.adapters.add('text', (text, target) => {
      const row = target.dataItem?.dataContext as ChartRow | undefined;
      return row ? escapeChartText(row.label) : text;
    });

    const yAxis = chart.yAxes.push(
      am5xy.CategoryAxis.new(root, { categoryField: 'key', renderer: yRenderer }),
    );
    yAxis.data.setAll(data);

    const xRenderer = am5xy.AxisRendererX.new(root, { minGridDistance: 80 });
    xRenderer.labels.template.setAll({ fill: tokens.textLow, fontSize: 12 });
    xRenderer.grid.template.setAll({ stroke: tokens.grid, strokeOpacity: 1 });

    const xAxis = chart.xAxes.push(
      am5xy.ValueAxis.new(root, {
        min: 0,
        max: 100,
        strictMinMax: true,
        calculateTotals: true,
        numberFormat: "#'%'",
        renderer: xRenderer,
      }),
    );

    const createSeries = (field: 'covered' | 'flagged' | 'missing', name: string, fill?: am5.Color) => {
      const series = chart.series.push(
        am5xy.ColumnSeries.new(root, {
          name,
          xAxis,
          yAxis,
          stacked: true,
          categoryYField: 'key',
          valueXField: field,
          valueXShow: 'valueXTotalPercent',
          tooltip: createTooltip(),
          ...(fill ? { fill, stroke: fill } : {}),
        }),
      );
      series.columns.template.setAll({
        tooltipText: '{tooltip}',
        height: am5.percent(100),
        strokeOpacity: 0,
      });
      series.data.setAll(data);
      void series.appear(600);
      return series;
    };

    const covered = createSeries('covered', captions.covered, coveredColor);
    if (captions.flagged) {
      createSeries('flagged', captions.flagged, flaggedColor ? am5.color(flaggedColor) : undefined);
    }
    createSeries('missing', captions.missing, missingColor ? am5.color(missingColor) : undefined);

    // Share label inside the covered part, when there is room.
    covered.bullets.push(() =>
      am5.Bullet.new(root, {
        locationX: 0,
        sprite: am5.Label.new(root, {
          text: "{valueXTotalPercent.formatNumber('0.0')}%",
          fill: tokens.surface,
          fontSize: 12,
          centerY: am5.p50,
          centerX: am5.p0,
          dx: 6,
          populateText: true,
          oversizedBehavior: 'hide',
        }),
      }),
    );

    const legend = chart.children.push(
      am5.Legend.new(root, {
        centerX: am5.p50,
        x: am5.p50,
        marginTop: 12,
      }),
    );
    legend.labels.template.setAll({ fill: tokens.text, fontSize: 13 });
    legend.data.setAll(chart.series.values);

    void chart.appear(600, 100);

    return () => {
      root.dispose();
    };
  }, [chartId, data, captions.covered, captions.flagged, captions.missing]);

  return (
    <div
      id={chartId}
      role="img"
      aria-label={ariaLabel}
      className="SimpleStats-chart SimpleStats-chart--ranked"
      style={{ height }}
    />
  );
});
