import * as am5 from '@amcharts/amcharts5';
import * as am5xy from '@amcharts/amcharts5/xy';
import React, { useId, useLayoutEffect, useMemo } from 'react';

import { createChartRoot, getChartTokens, getContrastingColor, getSeriesPalette, resolveToken } from './chartTheme';
import { chartNumberFormat, formatValue } from './format';
import { StatsPeriod, StatsSeries } from './types';
import { useStableValue } from './useStableValue';

export interface ComboChartProps {
  readonly periods: readonly StatsPeriod[];
  /**
   * Columns on the left axis (for example orders). Formatted by its `kind`.
   * Omit for a line-only chart (for example a point-in-time count over time); the line then uses the left axis.
   */
  readonly columns?: StatsSeries;
  /** Line on the right axis (for example revenue), or on the left axis without columns. Formatted by its `kind`. */
  readonly line: StatsSeries;
  /** Accessible name for the chart. */
  readonly ariaLabel: string;
}

interface ChartRow {
  readonly category: string;
  readonly column: number | null;
  readonly line: number;
  readonly tooltip: string;
}

/** Periods up to this count get a bullet on each line point; longer lines stay plain. */
const maxBulletPeriods = 45;

/** amCharts reads `[...]` as text formatting; double the brackets so data shows as typed. */
function escapeChartText(text: string): string {
  return text.replace(/\[/g, '[[').replace(/\]/g, ']]');
}

/**
 * Column + line chart with two value axes (amCharts 5): one column per period on the left axis
 * and a line on the right axis, for two measures of one trend (for example orders and revenue).
 * One tooltip per period shows both values, formatted by their kinds. Without `columns` it is a line chart on one axis.
 * The root is created in `useLayoutEffect` and disposed on unmount or data change.
 */
export const ComboChart = React.memo(function ComboChart({
  periods,
  columns: columnsProp,
  line: lineProp,
  ariaLabel,
}: ComboChartProps) {
  const columns = useStableValue(columnsProp);
  const line = useStableValue(lineProp);
  const chartId = `stats-chart-${useId().replace(/:/g, '')}`;

  const rows = useMemo<ChartRow[]>(
    () =>
      periods.map((period, index) => {
        const columnValue = columns ? (columns.values[index] ?? 0) : null;
        const lineValue = line.values[index] ?? 0;
        return {
          category: period.label,
          column: columnValue,
          line: lineValue,
          tooltip: [
            `[bold]${escapeChartText(period.label)}[/]`,
            ...(columns
              ? [`${escapeChartText(columns.name)}: ${escapeChartText(formatValue(columnValue, columns.kind, columns.texts?.[index]))}`]
              : []),
            `${escapeChartText(line.name)}: ${escapeChartText(formatValue(lineValue, line.kind, line.texts?.[index]))}`,
          ].join('\n'),
        };
      }),
    [periods, columns, line],
  );
  // Rebuild the chart only when the rows change by content, not on every new prop identity.
  const data = useStableValue(rows);

  useLayoutEffect(() => {
    const root = createChartRoot(chartId);

    const tokens = getChartTokens(root.dom);
    const palette = getSeriesPalette(root.dom);
    const colorOf = (series: StatsSeries, index: number) => {
      const fixed = series.color ? resolveToken(series.color, root.dom) : undefined;
      return fixed ? am5.color(fixed) : palette[index];
    };
    const columnColor = columns ? colorOf(columns, 0) : undefined;
    // Without a fixed color, the line takes the palette color that differs most from the columns,
    // so the two series are easy to tell apart. A line without columns takes the first palette color.
    const lineColor = line.color || !columnColor ? colorOf(line, columns ? 1 : 0) : getContrastingColor(palette, columnColor);

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

    const createValueAxis = (series: StatsSeries, opposite: boolean) => {
      const renderer = am5xy.AxisRendererY.new(root, { minGridDistance: 40, opposite });
      renderer.labels.template.setAll({ fill: tokens.textLow, fontSize: 12 });
      renderer.grid.template.setAll({ stroke: tokens.grid, strokeOpacity: 1 });

      const axis = chart.yAxes.push(
        am5xy.ValueAxis.new(root, {
          min: 0,
          extraMax: 0.05,
          numberFormat: chartNumberFormat(series.kind),
          ...(series.kind === 'Amount' ? {} : { maxPrecision: 0 }),
          renderer,
        }),
      );

      // Axis title, so each axis says which series it belongs to.
      const title = am5.Label.new(root, {
        text: escapeChartText(series.name),
        rotation: opposite ? 90 : -90,
        y: am5.p50,
        centerX: am5.p50,
        fill: tokens.textLow,
        fontSize: 12,
      });
      if (opposite) {
        axis.children.push(title);
      } else {
        axis.children.unshift(title);
      }

      return axis;
    };

    const leftAxis = createValueAxis(columns ?? line, false);
    let lineAxis = leftAxis;
    if (columns) {
      lineAxis = createValueAxis(line, true);
      // One set of grid lines: the right axis follows the left axis steps.
      lineAxis.set('syncWithAxis', leftAxis);
      lineAxis.get('renderer').grid.template.set('forceHidden', true);
    }

    const tooltip = am5.Tooltip.new(root, {
      labelText: '{tooltip}',
      getFillFromSprite: false,
      autoTextColor: false,
      pointerOrientation: 'horizontal',
    });
    tooltip.get('background')?.setAll({ fill: tokens.tooltip, stroke: tokens.tooltip });
    tooltip.label.set('fill', tokens.tooltipText);

    const columnSeries = columns
      ? chart.series.push(
          am5xy.ColumnSeries.new(root, {
            name: columns.name,
            xAxis,
            yAxis: leftAxis,
            categoryXField: 'category',
            valueYField: 'column',
            // The cursor shows this one tooltip for both series.
            tooltip,
            ...(columnColor ? { fill: columnColor, stroke: columnColor } : {}),
          }),
        )
      : undefined;
    columnSeries?.columns.template.setAll({
      width: am5.percent(90),
      strokeOpacity: 0,
      cornerRadiusTL: 2,
      cornerRadiusTR: 2,
    });
    columnSeries?.data.setAll(data);

    const lineSeries = chart.series.push(
      am5xy.LineSeries.new(root, {
        name: line.name,
        xAxis,
        yAxis: lineAxis,
        categoryXField: 'category',
        valueYField: 'line',
        // Without columns, the line shows the tooltip.
        ...(columns ? {} : { tooltip }),
        ...(lineColor ? { fill: lineColor, stroke: lineColor } : {}),
      }),
    );
    lineSeries.strokes.template.setAll({ strokeWidth: 2 });
    lineSeries.data.setAll(data);

    const cursor = chart.set('cursor', am5xy.XYCursor.new(root, { behavior: 'none', xAxis }));
    cursor.lineY.set('visible', false);
    cursor.lineX.setAll({ stroke: tokens.grid, strokeOpacity: 1, strokeDasharray: [] });

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

    // Bullets are added after the legend, so the line's legend marker is the plain solid line (same color and width),
    // like the columns' marker is a plain square. Bullets added later still apply to the existing data items.
    if (data.length <= maxBulletPeriods) {
      lineSeries.bullets.push(() =>
        am5.Bullet.new(root, {
          sprite: am5.Circle.new(root, {
            radius: 3,
            fill: lineColor,
            stroke: tokens.surface,
            strokeWidth: 1.5,
          }),
        }),
      );
    }

    void columnSeries?.appear(600);
    void lineSeries.appear(600);
    void chart.appear(600, 100);

    return () => {
      root.dispose();
    };
  }, [chartId, data, columns, line]);

  return <div id={chartId} role="img" aria-label={ariaLabel} className="SimpleStats-chart" />;
});
