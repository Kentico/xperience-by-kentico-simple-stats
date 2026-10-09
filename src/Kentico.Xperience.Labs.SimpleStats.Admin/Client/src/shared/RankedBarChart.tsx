import * as am5 from '@amcharts/amcharts5';
import * as am5xy from '@amcharts/amcharts5/xy';
import { Colors } from '@kentico/xperience-admin-components';
import React, { useId, useLayoutEffect, useMemo } from 'react';

import { chartLogoSpace, createChartRoot, getChartTokens, getSeriesPalette, resolveToken } from './chartTheme';
import { chartNumberFormat, formatItemChange, formatShare, formatValue } from './format';
import { StatsRankedCaptions, StatsRankedItem } from './types';
import { useStableValue } from './useStableValue';

export interface RankedBarChartProps {
  readonly items: readonly StatsRankedItem[];
  readonly captions: StatsRankedCaptions;
  /** Accessible name for the chart. */
  readonly ariaLabel: string;
  /** Optional link per item. Clicking its bar opens it in the same tab. */
  readonly getHref?: (item: StatsRankedItem) => string | null;
  /** Shows the share of the total in tooltips. Hide it when values do not add up (for example days). Default `true`. */
  readonly showShare?: boolean;
  /** Bars with a value at or above this are drawn in the alert color (for example items waiting too long). */
  readonly highlightFrom?: number;
}

interface ChartRow {
  readonly key: string;
  readonly label: string;
  readonly value: number;
  readonly tooltip: string;
  readonly href: string | null;
  readonly highlight: boolean;
  /** Bar-end label formatted on the server (for example an amount with currency). `null`: the number is formatted by the chart. */
  readonly valueLabel: string | null;
}

const rowHeight = 32;
const chartPadding = 48;
const minHeight = 160;

/** Share of the chart width that axis labels may use before they are truncated. */
const labelWidthRatio = 0.4;
const minLabelWidth = 120;

/** Tick step of the value axis for ratios (20%). */
const ratioStep = 0.2;

/** amCharts reads `[...]` as text formatting; double the brackets so data shows as typed. */
function escapeChartText(text: string): string {
  return text.replace(/\[/g, '[[').replace(/\]/g, ']]');
}

/**
 * Horizontal ranked bar chart (amCharts 5): one bar per item, largest on top,
 * value labels at bar ends. Long labels are truncated; the full text shows in the tooltip.
 * The root is created in `useLayoutEffect` and disposed on unmount or data change.
 */
export const RankedBarChart = React.memo(function RankedBarChart({
  items,
  captions,
  ariaLabel,
  getHref,
  showShare = true,
  highlightFrom,
}: RankedBarChartProps) {
  const chartId = `stats-chart-${useId().replace(/:/g, '')}`;

  const rows = useMemo<ChartRow[]>(
    () =>
      items.map((item) => {
        const lines = [
          `[bold]${escapeChartText(item.label)}[/]`,
          ...(captions.secondaryLabel && item.secondaryLabel
            ? [escapeChartText(item.secondaryLabel)]
            : []),
          `${captions.value}: ${escapeChartText(formatValue(item.value, captions.valueKind, item.valueText))}${showShare ? ` (${formatShare(item.share)})` : ''}`,
          ...(captions.secondaryValue && item.secondaryValue !== null
            ? [`${captions.secondaryValue}: ${escapeChartText(formatValue(item.secondaryValue, captions.secondaryValueKind, item.secondaryValueText))}`]
            : []),
          ...(captions.tertiaryValue && item.tertiaryValue !== null && item.tertiaryValue !== undefined
            ? [`${captions.tertiaryValue}: ${escapeChartText(formatValue(item.tertiaryValue, captions.tertiaryValueKind, item.tertiaryValueText))}`]
            : []),
          ...(captions.previousValue && item.previousValue !== null && item.previousValue !== undefined
            ? [`${captions.previousValue}: ${escapeChartText(formatValue(item.previousValue, captions.valueKind, item.previousValueText))}`]
            : []),
          ...(captions.change ? [`${captions.change}: ${formatItemChange(item)}`] : []),
        ];
        return {
          key: item.key,
          label: item.label,
          value: item.value,
          tooltip: lines.join('\n'),
          href: getHref?.(item) ?? null,
          highlight: highlightFrom !== undefined && item.value >= highlightFrom,
          valueLabel: item.valueText ? escapeChartText(item.valueText) : null,
        };
      }),
    [items, captions, getHref, showShare, highlightFrom],
  );
  // Rebuild the chart only when the rows change by content, not on every new prop identity.
  const data = useStableValue(rows);

  const height = Math.max(minHeight, data.length * rowHeight + chartPadding) + chartLogoSpace;

  useLayoutEffect(() => {
    const root = createChartRoot(chartId);
    // Value labels at the bar ends and axis labels, formatted by the value kind (counts as before).
    root.numberFormatter.set('numberFormat', captions.valueKind ? chartNumberFormat(captions.valueKind) : '#,###');

    const isRatio = captions.valueKind === 'Ratio';
    const tokens = getChartTokens(root.dom);
    const barColor = getSeriesPalette(root.dom)[0];
    const highlightValue = resolveToken(Colors.AlertBackgroundHighEmphasis, root.dom);
    const highlightColor = highlightValue ? am5.color(highlightValue) : undefined;

    const chart = root.container.children.push(
      am5xy.XYChart.new(root, {
        panX: false,
        panY: false,
        wheelX: 'none',
        wheelY: 'none',
        layout: root.verticalLayout,
        paddingLeft: 0,
        // Room for the last value axis label (centered on the right edge) and, for ratios, a bar-end label near the end of the fixed scale.
        paddingRight: isRatio ? 44 : 24,
      }),
    );
    chart.zoomOutButton.set('forceHidden', true);

    const createTooltip = () => {
      const tooltip = am5.Tooltip.new(root, {
        getFillFromSprite: false,
        autoTextColor: false,
        pointerOrientation: 'horizontal',
      });
      tooltip.get('background')?.setAll({
        fill: tokens.tooltip,
        stroke: tokens.tooltip,
      });
      tooltip.label.setAll({ fill: tokens.tooltipText, maxWidth: 480, oversizedBehavior: 'wrap' });
      return tooltip;
    };

    // Category axis on Y, inversed so the first (largest) item is on top.
    const yRenderer = am5xy.AxisRendererY.new(root, {
      inversed: true,
      minGridDistance: 1,
      cellStartLocation: 0.15,
      cellEndLocation: 0.85,
    });
    yRenderer.grid.template.set('visible', false);
    yRenderer.labels.template.setAll({
      fill: tokens.text,
      fontSize: 13,
      oversizedBehavior: 'truncate',
      ellipsis: '…',
      maxWidth: minLabelWidth,
      // Replaced by the adapter below; any text enables the hover tooltip.
      tooltipText: '{category}',
      tooltip: createTooltip(),
    });
    // Show the label (not the unique key) and keep the full text for the tooltip.
    yRenderer.labels.template.adapters.add('text', (text, target) => {
      const row = target.dataItem?.dataContext as ChartRow | undefined;
      return row ? escapeChartText(row.label) : text;
    });
    yRenderer.labels.template.adapters.add('tooltipText', (text, target) => {
      const row = target.dataItem?.dataContext as ChartRow | undefined;
      return row ? escapeChartText(row.label) : text;
    });

    const yAxis = chart.yAxes.push(
      am5xy.CategoryAxis.new(root, {
        categoryField: 'key',
        renderer: yRenderer,
      }),
    );
    yAxis.data.setAll(data);

    const xRenderer = am5xy.AxisRendererX.new(root, { minGridDistance: 80 });
    xRenderer.labels.template.setAll({ fill: tokens.textLow, fontSize: 12 });
    xRenderer.grid.template.setAll({ stroke: tokens.grid, strokeOpacity: 1 });

    // Ratios use a fixed 0-100% scale (more when a rate is over 100%) with a tick every 20%.
    const ratioMax = isRatio
      ? Math.max(1, Math.ceil(Math.max(0, ...data.map((row) => row.value)) / ratioStep - 1e-9) * ratioStep)
      : 0;
    if (isRatio) {
      // The axis's own grid would pick its own steps; ticks are drawn as axis ranges below.
      xRenderer.labels.template.set('forceHidden', true);
      xRenderer.grid.template.set('forceHidden', true);
    }

    const xAxis = chart.xAxes.push(
      am5xy.ValueAxis.new(root, {
        min: 0,
        ...(isRatio
          ? { max: ratioMax, strictMinMax: true }
          : {
              maxPrecision: 0,
              // Room for the value labels at the bar ends.
              extraMax: 0.12,
            }),
        renderer: xRenderer,
      }),
    );

    if (isRatio) {
      for (let step = 0; step * ratioStep <= ratioMax + 1e-9; step++) {
        const range = xAxis.createAxisRange(xAxis.makeDataItem({ value: step * ratioStep }));
        range.get('grid')?.setAll({ stroke: tokens.grid, strokeOpacity: 1, forceHidden: false });
        range.get('label')?.setAll({
          text: `${Math.round(step * ratioStep * 100)}%`,
          fill: tokens.textLow,
          fontSize: 12,
          forceHidden: false,
        });
      }
    }

    const series = chart.series.push(
      am5xy.ColumnSeries.new(root, {
        name: captions.value,
        xAxis,
        yAxis,
        categoryYField: 'key',
        valueXField: 'value',
        // The fixed ratio scale has no extra room, so bar-end labels may draw past the plot area.
        maskBullets: !isRatio,
        tooltip: createTooltip(),
      }),
    );
    series.columns.template.setAll({
      tooltipText: '{tooltip}',
      height: am5.percent(100),
      strokeOpacity: 0,
      cornerRadiusTR: 2,
      cornerRadiusBR: 2,
      ...(barColor ? { fill: barColor } : {}),
      // Per-row overrides (the highlight fill below).
      templateField: 'columnSettings',
    });

    series.bullets.push(() =>
      am5.Bullet.new(root, {
        locationX: 1,
        sprite: am5.Label.new(root, {
          // Server-formatted values (amounts with currency) when every row has one; else the chart formats the number.
          text: data.length > 0 && data.every((row) => row.valueLabel) ? '{valueLabel}' : '{valueX}',
          fill: tokens.text,
          fontSize: 12,
          centerY: am5.p50,
          centerX: am5.p0,
          dx: 6,
          populateText: true,
        }),
      }),
    );

    // Highlighted rows (see `highlightFrom`) use the alert color, set per row through `templateField`
    // (a `fill` adapter on the template did not take effect).
    series.data.setAll(
      data.map((row) =>
        row.highlight && highlightColor ? { ...row, columnSettings: { fill: highlightColor } } : row,
      ),
    );

    // Bars of linked items open the link, like the table's link cells.
    if (data.some((row) => row.href)) {
      const hrefOf = (target: am5.Sprite) =>
        (target.dataItem?.dataContext as ChartRow | undefined)?.href ?? null;
      series.columns.template.adapters.add('cursorOverStyle', (style, target) =>
        hrefOf(target) ? 'pointer' : style,
      );
      series.columns.template.events.on('click', (ev) => {
        const href = hrefOf(ev.target);
        if (href) {
          window.location.assign(href);
        }
      });
    }

    // Truncate labels relative to the chart width, so wide screens show more text.
    chart.events.on('boundschanged', () => {
      const maxWidth = Math.max(minLabelWidth, Math.round(chart.width() * labelWidthRatio));
      if (yRenderer.labels.template.get('maxWidth') !== maxWidth) {
        yRenderer.labels.template.set('maxWidth', maxWidth);
      }
    });

    void series.appear(600);
    void chart.appear(600, 100);

    return () => {
      root.dispose();
    };
  }, [chartId, data, captions.value, captions.valueKind]);

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
