import * as am5 from '@amcharts/amcharts5';
import * as am5xy from '@amcharts/amcharts5/xy';
import React, { useId, useLayoutEffect, useMemo } from 'react';

import { createChartRoot, getChartTokens, getSeriesPalette, resolveToken } from './chartTheme';
import { StatsPeriod, StatsSeries } from './types';
import { useStableValue } from './useStableValue';

export interface StackedColumnChartProps {
  readonly periods: readonly StatsPeriod[];
  readonly series: readonly StatsSeries[];
  /** Accessible name for the chart. */
  readonly ariaLabel: string;
  /**
   * Stacks the series (default). With `false`, the series stand side by side in each period, for series that overlap
   * (for example sent emails, opens and clicks).
   */
  readonly stacked?: boolean;
}

type ChartRow = Record<string, string | number>;

/**
 * Column chart (amCharts 5): one column per period, one stack segment per series (or one column per series side by side, see `stacked`).
 * The root is created in `useLayoutEffect` and disposed on unmount or data change.
 */
export const StackedColumnChart = React.memo(function StackedColumnChart({
  periods,
  series: seriesProp,
  ariaLabel,
  stacked = true,
}: StackedColumnChartProps) {
  const series = useStableValue(seriesProp);
  const chartId = `stats-chart-${useId().replace(/:/g, '')}`;

  const rows = useMemo<ChartRow[]>(
    () =>
      periods.map((period, index) => {
        const row: ChartRow = { category: period.label };
        series.forEach((s, seriesIndex) => {
          row[`s${seriesIndex}`] = s.values[index] ?? 0;
        });
        return row;
      }),
    [periods, series],
  );
  // Rebuild the chart only when the rows change by content, not on every new prop identity.
  const data = useStableValue(rows);

  useLayoutEffect(() => {
    const root = createChartRoot(chartId);
    root.numberFormatter.set('numberFormat', '#,###');

    const tokens = getChartTokens(root.dom);

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

    const palette = getSeriesPalette(root.dom);
    if (palette.length > 0) {
      chart.get('colors')?.set('colors', palette);
    }

    const xRenderer = am5xy.AxisRendererX.new(root, {
      minGridDistance: 50,
      cellStartLocation: 0.1,
      cellEndLocation: 0.9,
    });
    xRenderer.labels.template.setAll({ fill: tokens.textLow, fontSize: 12 });
    xRenderer.grid.template.set('visible', false);

    const xAxis = chart.xAxes.push(
      am5xy.CategoryAxis.new(root, {
        categoryField: 'category',
        renderer: xRenderer,
      }),
    );
    xAxis.data.setAll(data);

    const yRenderer = am5xy.AxisRendererY.new(root, { minGridDistance: 40 });
    yRenderer.labels.template.setAll({ fill: tokens.textLow, fontSize: 12 });
    yRenderer.grid.template.setAll({
      stroke: tokens.grid,
      strokeOpacity: 1,
    });

    const yAxis = chart.yAxes.push(
      am5xy.ValueAxis.new(root, {
        min: 0,
        maxPrecision: 0,
        extraMax: 0.05,
        renderer: yRenderer,
      }),
    );

    series.forEach((item, index) => {
      const tooltip = am5.Tooltip.new(root, {
        labelText: '[bold]{name}[/]\n{categoryX}: {valueY}',
        getFillFromSprite: false,
        autoTextColor: false,
      });
      tooltip.get('background')?.setAll({
        fill: tokens.tooltip,
        stroke: tokens.tooltip,
      });
      tooltip.label.set('fill', tokens.tooltipText);

      const columnSeries = chart.series.push(
        am5xy.ColumnSeries.new(root, {
          name: item.name,
          xAxis,
          yAxis,
          stacked,
          categoryXField: 'category',
          valueYField: `s${index}`,
          tooltip,
        }),
      );
      // A fixed series color (for example error / warning / information) overrides the palette.
      const fixedColor = item.color ? resolveToken(item.color, root.dom) : undefined;
      if (fixedColor) {
        columnSeries.set('fill', am5.color(fixedColor));
        columnSeries.set('stroke', am5.color(fixedColor));
      }
      columnSeries.columns.template.setAll({
        tooltipText: '[bold]{name}[/]\n{categoryX}: {valueY}',
        width: am5.percent(90),
        strokeOpacity: 0,
      });
      columnSeries.data.setAll(data);
      void columnSeries.appear(600);
    });

    const legend = chart.children.push(
      am5.Legend.new(root, {
        centerX: am5.percent(50),
        x: am5.percent(50),
        marginTop: 16,
        layout: root.gridLayout,
      }),
    );
    legend.labels.template.setAll({ fill: tokens.text, fontSize: 13 });
    legend.valueLabels.template.set('forceHidden', true);
    legend.data.setAll(chart.series.values);

    void chart.appear(600, 100);

    return () => {
      root.dispose();
    };
  }, [chartId, data, series, stacked]);

  return (
    <div
      id={chartId}
      role="img"
      aria-label={ariaLabel}
      className="SimpleStats-chart"
    />
  );
});
