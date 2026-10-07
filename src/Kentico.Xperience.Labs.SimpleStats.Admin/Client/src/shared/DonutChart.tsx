import * as am5 from '@amcharts/amcharts5';
import * as am5percent from '@amcharts/amcharts5/percent';
import type { Colors } from '@kentico/xperience-admin-components';
import React, { useId, useLayoutEffect, useMemo } from 'react';

import { createChartRoot, getChartTokens, getSeriesPalette, resolveToken } from './chartTheme';
import { StatsShareSlice } from './types';
import { useStableValue } from './useStableValue';

export interface DonutChartProps {
  /**
   * Slices in display order. Slices with a fixed `color` use it; the others take the series palette colors in this order.
   */
  readonly slices: readonly StatsShareSlice[];
  /** Big text in the center, for example a share or a total. */
  readonly centerValue?: string;
  /** Small text under `centerValue`. */
  readonly centerCaption?: string;
  /** Accessible name for the chart. */
  readonly ariaLabel: string;
}

/**
 * Lightening (share of the way to white) of the 1st, 2nd, 3rd, ... slice with the same fixed color,
 * so for example two "done" statuses are still told apart. Later slices keep the last step.
 */
const sameColorLightening = [0, 0.3, 0.5, 0.65, 0.75];

interface ChartRow {
  readonly category: string;
  readonly value: number;
  /** Fixed color token, resolved in the effect. */
  readonly color?: Colors;
}

/**
 * Slice fills: the fixed color of a slice (lighter for each earlier slice with the same color),
 * else the next palette color. `undefined` lets amCharts pick (no palette).
 */
function getFills(
  rows: readonly ChartRow[],
  palette: readonly am5.Color[],
  element: Element,
): (am5.Color | undefined)[] {
  const sameColorCount = new Map<Colors, number>();
  let paletteIndex = 0;

  return rows.map((row) => {
    const fixed = row.color ? resolveToken(row.color, element) : undefined;
    if (row.color && fixed) {
      const count = sameColorCount.get(row.color) ?? 0;
      sameColorCount.set(row.color, count + 1);
      const step = sameColorLightening[Math.min(count, sameColorLightening.length - 1)];
      return am5.Color.lighten(am5.color(fixed), step);
    }
    // No fixed color, or its token is missing at runtime: next palette color.
    return palette.length > 0 ? palette[paletteIndex++ % palette.length] : undefined;
  });
}

/** amCharts reads `[...]` as text formatting; double the brackets so data shows as typed. */
function escapeChartText(text: string): string {
  return text.replace(/\[/g, '[[').replace(/\]/g, ']]');
}

/**
 * Donut chart (amCharts 5 percent pie with an inner radius): one slice per item,
 * center label, legend with shares, tooltip with value and share.
 * Slice colors follow the series palette in order, so they match `StackedColumnChart` series colors,
 * except slices with a fixed `color` (see `StatsShareSlice.color`).
 * The root is created in `useLayoutEffect` and disposed on unmount or data change.
 */
export const DonutChart = React.memo(function DonutChart({
  slices,
  centerValue,
  centerCaption,
  ariaLabel,
}: DonutChartProps) {
  const chartId = `stats-chart-${useId().replace(/:/g, '')}`;

  const rows = useMemo<ChartRow[]>(
    () =>
      slices.map((slice) => ({
        category: escapeChartText(slice.name),
        value: slice.value,
        color: slice.color,
      })),
    [slices],
  );
  // Rebuild the chart only when the rows change by content, not on every new prop identity.
  const data = useStableValue(rows);

  useLayoutEffect(() => {
    const root = createChartRoot(chartId);
    root.numberFormatter.set('numberFormat', '#,##0');

    const tokens = getChartTokens(root.dom);
    // Colors are read from the chart element (the admin theme wrapper defines the tokens).
    const fills = getFills(data, getSeriesPalette(root.dom), root.dom);

    const chart = root.container.children.push(
      am5percent.PieChart.new(root, {
        layout: root.verticalLayout,
        innerRadius: am5.percent(62),
        radius: am5.percent(90),
      }),
    );

    const tooltip = am5.Tooltip.new(root, {
      getFillFromSprite: false,
      autoTextColor: false,
    });
    tooltip.get('background')?.setAll({
      fill: tokens.tooltip,
      stroke: tokens.tooltip,
    });
    tooltip.label.set('fill', tokens.tooltipText);

    const series = chart.series.push(
      am5percent.PieSeries.new(root, {
        categoryField: 'category',
        valueField: 'value',
        fillField: 'fill',
        alignLabels: false,
        tooltip,
        legendValueText: "{valuePercentTotal.formatNumber('0.0')}%",
      }),
    );
    series.labels.template.set('forceHidden', true);
    series.ticks.template.set('forceHidden', true);
    series.slices.template.setAll({
      tooltipText: "[bold]{category}[/]\n{value} ({valuePercentTotal.formatNumber('0.0')}%)",
      strokeWidth: 2,
      stroke: tokens.surface,
      // Slices do not pull out on click; the chart is read-only.
      toggleKey: 'none',
    });
    series.data.setAll(
      data.map((row, index) => ({ category: row.category, value: row.value, fill: fills[index] })),
    );

    if (centerValue) {
      series.children.push(
        am5.Label.new(root, {
          text: `[fontSize:24px bold]${escapeChartText(centerValue)}[/]${
            centerCaption ? `\n[fontSize:13px]${escapeChartText(centerCaption)}[/]` : ''
          }`,
          fill: tokens.text,
          textAlign: 'center',
          centerX: am5.p50,
          centerY: am5.p50,
          populateText: false,
        }),
      );
    }

    const legend = chart.children.push(
      am5.Legend.new(root, {
        centerX: am5.percent(50),
        x: am5.percent(50),
        marginTop: 16,
        layout: root.gridLayout,
      }),
    );
    legend.labels.template.setAll({ fill: tokens.text, fontSize: 13 });
    legend.valueLabels.template.setAll({ fill: tokens.textLow, fontSize: 13 });
    legend.data.setAll(series.dataItems);

    void series.appear(600, 100);

    return () => {
      root.dispose();
    };
  }, [chartId, data, centerValue, centerCaption]);

  return (
    <div
      id={chartId}
      role="img"
      aria-label={ariaLabel}
      className="SimpleStats-chart SimpleStats-chart--donut"
    />
  );
});
