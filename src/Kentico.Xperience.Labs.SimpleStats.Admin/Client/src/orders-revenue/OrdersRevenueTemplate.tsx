import { ButtonColor, Colors, LinkButton } from '@kentico/xperience-admin-components';
import React, { useMemo, useState } from 'react';

import { toAdminHref } from '../shared/adminLinks';
import { ComboChart } from '../shared/ComboChart';
import {
  CommerceOrderStatusOption,
  commerceDocsUrl,
  OrderStatusSelect,
  rawAmountNote,
  toRawCsvSeries,
} from '../shared/commerce';
import { ComparisonInfoCard } from '../shared/ComparisonInfoCard';
import { toRankedCsv, toShareCsv, toTimeSeriesCsv } from '../shared/csv';
import { DataRetentionNote } from '../shared/DataRetentionNote';
import { DonutChart } from '../shared/DonutChart';

import { formatPreviousPeriod, formatValue } from '../shared/format';
import { RankedBarChart } from '../shared/RankedBarChart';
import { RankedTable } from '../shared/RankedTable';
import { ShareTable } from '../shared/ShareTable';
import { StatsFilterBar } from '../shared/StatsFilterBar';
import { StatsTile } from '../shared/StatsTile';
import { toValueSeries } from '../shared/timeSeries';
import { TimeSeriesTable } from '../shared/TimeSeriesTable';
import {
  StatsFilter,
  StatsGrouping,
  StatsPeriod,
  StatsRankedCaptions,
  StatsRankedResult,
  StatsShareSlice,
  StatsTone,
  StatsValueComparison,
  StatsValueSeries,
} from '../shared/types';
import { useCsvExport } from '../shared/useCsvExport';
import { useStatsCommand } from '../shared/useStatsCommand';
import '../shared/stats.css';

/**
 * Traffic-light slice colors of the status tones the server guesses from the status names.
 * Neutral statuses take palette colors.
 */
const toneColors: Partial<Record<StatsTone, Colors>> = {
  Problem: Colors.AlertBackgroundHighEmphasis,
  Caution: Colors.WarningBackgroundHighEmphasis,
  Done: Colors.SuccessBackgroundHighEmphasis,
};

/** Mirrors `OrdersRevenueTotals`. */
interface OrdersRevenueTotals {
  readonly orders: StatsValueComparison;
  readonly revenue: StatsValueComparison;
  /** Values are `null` for a period without orders. */
  readonly averageOrderValue: StatsValueComparison;
  readonly itemsSold: StatsValueComparison;
}

/** Mirrors `OrdersRevenueResult`. */
interface OrdersRevenueResult {
  readonly from: string;
  readonly to: string;
  readonly grouping: StatsGrouping;
  /** Applied status filter, `null` for all statuses. */
  readonly orderStatusId: number | null;
  /** Statuses for the status filter, in status order. */
  readonly statuses: readonly CommerceOrderStatusOption[];
  readonly periods: readonly StatsPeriod[];
  readonly orders: StatsValueSeries;
  readonly revenue: StatsValueSeries;
  readonly totals: OrdersRevenueTotals;
  /** Value = orders, secondary value = revenue. Not affected by the status filter. */
  readonly byStatus: StatsRankedResult;
  /** Value = revenue, secondary value = quantity, with previous revenue and change. */
  readonly topProducts: StatsRankedResult;
  /** `false` when the commerce tables do not exist. */
  readonly commerceAvailable: boolean;
  /** Native Orders listing, relative to the admin root. */
  readonly ordersPath: string | null;
  /** ISO timestamp of when the data was read from the database. */
  readonly updatedAt: string;
}

/** Mirrors `OrdersRevenueFilter`. */
interface OrdersRevenueFilter {
  readonly range: StatsFilter;
  readonly orderStatusId: number | null;
}

/** Mirrors `OrdersRevenueClientProperties`. */
interface OrdersRevenueTemplateProps {
  readonly report: OrdersRevenueResult;
  readonly today: string;
  /** Path of this page relative to the admin root, used to build admin links. */
  readonly pagePath: string | null;
}


function toFilter(report: OrdersRevenueResult): OrdersRevenueFilter {
  // Orders have no channel; the range filter type is shared with channel reports.
  return {
    range: { from: report.from, to: report.to, grouping: report.grouping, channelId: null },
    orderStatusId: report.orderStatusId,
  };
}


export const OrdersRevenueTemplate = (props: OrdersRevenueTemplateProps) => {
  const saveCsv = useCsvExport();
  const { data: report, isLoading, hasError, load } = useStatsCommand<
    OrdersRevenueResult,
    OrdersRevenueFilter
  >(props.report);
  const [filter, setFilter] = useState<OrdersRevenueFilter>(() => toFilter(props.report));

  const handleFilterChange = (next: OrdersRevenueFilter) => {
    setFilter(next);
    void load(next);
  };

  const handleRefresh = () => {
    void load(filter, { refresh: true });
  };

  const { totals, byStatus, topProducts, periods } = report;
  const ordersHref = toAdminHref(report.ordersPath, props.pagePath);
  const rangeText = `${report.from} – ${report.to}`;
  const statusName = report.statuses.find((s) => s.id === report.orderStatusId)?.displayName;
  const statusSuffix = report.orderStatusId === null ? '' : `_status-${report.orderStatusId}`;

  const emptyMessage = !report.commerceAvailable
    ? 'Digital commerce tables were not found in this project, so there are no orders to show.'
    : `No orders${statusName ? ` in status ${statusName}` : ''} in the selected range. Try a longer range or another status.`;

  const ordersSeries = useMemo(() => toValueSeries(report.orders), [report.orders]);
  const revenueSeries = useMemo(() => toValueSeries(report.revenue), [report.revenue]);
  const trendSeries = useMemo(() => [ordersSeries, revenueSeries], [ordersSeries, revenueSeries]);
  const hasOrders = report.orders.total > 0;

  const slices = useMemo<StatsShareSlice[]>(
    () =>
      byStatus.items.map((item) => ({
        key: item.key,
        name: item.label,
        value: item.value,
        secondaryValue: item.secondaryValue,
        secondaryValueText: item.secondaryValueText ?? null,
        color: item.tone ? toneColors[item.tone] : undefined,
      })),
    [byStatus.items],
  );

  const productCaptions = useMemo<StatsRankedCaptions>(
    () => ({
      label: 'Product',
      secondaryLabel: 'SKU',
      value: 'Revenue',
      valueKind: topProducts.valueKind ?? 'Amount',
      secondaryValue: 'Quantity',
      secondaryValueKind: topProducts.secondaryValueKind ?? 'Count',
      previousValue: `Revenue ${formatPreviousPeriod(totals.revenue)}`,
      change: 'Change',
    }),
    [topProducts.valueKind, topProducts.secondaryValueKind, totals.revenue],
  );

  const exportTrendCsv = () => {
    saveCsv(
      'orders-revenue',
      `orders-revenue${statusSuffix}_${report.from}_${report.to}_${report.grouping.toLowerCase()}.csv`,
      toTimeSeriesCsv(periods, trendSeries.map(toRawCsvSeries), { includeTotal: false }),
    );
  };

  const exportStatusCsv = () => {
    saveCsv(
      'orders-by-status',
      `orders-by-status_${report.from}_${report.to}.csv`,
      toShareCsv(slices, 'Status', 'Orders', `Revenue ${rawAmountNote}`),
    );
  };

  const exportProductsCsv = () => {
    saveCsv(
      'orders-top-products',
      `orders-top-products${statusSuffix}_${topProducts.from}_${topProducts.to}.csv`,
      toRankedCsv(topProducts.items, {
        ...productCaptions,
        value: `${productCaptions.value} ${rawAmountNote}`,
        previousValue: `${productCaptions.previousValue ?? ''} ${rawAmountNote}`,
      }),
    );
  };

  const filteredHint = statusName ? ` Only orders in status ${statusName}.` : '';
  const changeHint = `Change compares with the ${formatPreviousPeriod(totals.orders)} (${totals.orders.previousFrom} – ${totals.orders.previousTo}). "New" means no revenue in that period.`;

  return (
    <div className="SimpleStats-root">
      <StatsFilterBar
        filter={filter.range}
        today={props.today}
        onChange={(range) => handleFilterChange({ ...filter, range })}
        onRefresh={handleRefresh}
        isLoading={isLoading}
        updatedAt={report.updatedAt}
        showChannel={false}
        actions={
          ordersHref && (
            <LinkButton
              label="Open orders"
              color={ButtonColor.Tertiary}
              href={ordersHref}
              title="Open the Orders application"
            />
          )
        }
      >
        <OrderStatusSelect
          statuses={report.statuses}
          value={filter.orderStatusId}
          onChange={(orderStatusId) => handleFilterChange({ ...filter, orderStatusId })}
        />
      </StatsFilterBar>

      <div className="SimpleStats-kpis">
        <ComparisonInfoCard
          caption="Orders"
          tooltip={`Orders created in the selected range (${rangeText}).${filteredHint}`}
          comparison={totals.orders}
          noun="orders"
        />
        <ComparisonInfoCard
          caption="Revenue"
          tooltip={`Sum of order grand totals (incl. shipping and tax, as stored at order time; not converted between currencies) in the selected range.${filteredHint}`}
          comparison={totals.revenue}
          noun="revenue"
        />
        <ComparisonInfoCard
          caption="Average order value"
          tooltip={`Revenue divided by orders in the selected range.${filteredHint}`}
          comparison={totals.averageOrderValue}
          noun="orders"
        />
        <ComparisonInfoCard
          caption="Items sold"
          tooltip={`Sum of order item quantities in the selected range.${filteredHint}`}
          comparison={totals.itemsSold}
          noun="items"
        />
      </div>

      <div className="SimpleStats-tiles">
        <StatsTile
          headline="Orders and revenue over time"
          description={`Orders (columns, left axis) and revenue (line, right axis) per ${report.grouping.toLowerCase()}.${filteredHint}`}
          isLoading={isLoading}
          hasError={hasError}
          isEmpty={!hasOrders}
          emptyMessage={emptyMessage}
          onExportCsv={exportTrendCsv}
          renderChart={() => (
            <ComboChart
              periods={periods}
              columns={ordersSeries}
              line={revenueSeries}
              ariaLabel={`Orders and revenue over time, ${rangeText}`}
            />
          )}
          renderTable={() => (
            <TimeSeriesTable
              grouping={report.grouping}
              periods={periods}
              series={trendSeries}
              showTotal={false}
            />
          )}
        />

        <StatsTile
          headline="Orders by status"
          description="Current status of the orders created in the selected range. Shows every status, also when the Order status filter is set."
          isLoading={isLoading}
          hasError={hasError}
          isEmpty={slices.length === 0}
          emptyMessage={emptyMessage}
          onExportCsv={exportStatusCsv}
          renderChart={() => (
            <DonutChart
              slices={slices}
              centerValue={formatValue(byStatus.total)}
              centerCaption="orders"
              ariaLabel={`Orders by status, ${rangeText}`}
            />
          )}
          renderTable={() => (
            <ShareTable
              slices={slices}
              labelCaption="Status"
              valueCaption="Orders"
              secondaryValueCaption="Revenue"
              secondaryValueKind={byStatus.secondaryValueKind ?? 'Amount'}
            />
          )}
        />
      </div>

      <StatsTile
        headline="Top products"
        description={`Products with the most revenue (order item totals), by SKU. ${changeHint}${filteredHint}`}
        isLoading={isLoading}
        hasError={hasError}
        isEmpty={topProducts.items.length === 0}
        emptyMessage={emptyMessage}
        onExportCsv={exportProductsCsv}
        renderChart={() => (
          <RankedBarChart
            items={topProducts.items}
            captions={productCaptions}
            ariaLabel={`Top products by revenue, ${rangeText}`}
          />
        )}
        renderTable={() => <RankedTable items={topProducts.items} captions={productCaptions} />}
      />

      <DataRetentionNote
        message="Revenue is the order grand total (incl. shipping and tax) as stored when the order was placed. Amounts are formatted by the project's price formatter (its currency) and are not converted between currencies. CSV exports have the raw numbers. Orders are grouped by the date they were created; deleted orders are not counted."
        link={{ href: commerceDocsUrl, label: 'Learn how to manage commerce stores' }}
      />
    </div>
  );
};
