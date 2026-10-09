using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.Commerce;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.OrdersRevenue;

/// <summary>
/// Filter of the orders and revenue report, sent by the admin client: the shared range and grouping (<see cref="StatsFilter"/>)
/// plus an optional order status. The shared filter is wrapped, not changed (same approach as the event log type filter),
/// so other reports keep their filter and cache keys.
/// </summary>
public sealed record OrdersRevenueFilter
{
    /// <summary>
    /// Range and grouping. The channel is ignored (orders have no channel). <c>null</c> means defaults.
    /// </summary>
    public StatsFilter? Range { get; init; }

    /// <summary>
    /// Optional order status ID (<c>Commerce_OrderStatus.OrderStatusID</c>). <c>null</c>, a value &lt;= 0,
    /// or an ID that does not exist (checked by the report service) means all statuses.
    /// </summary>
    public int? OrderStatusId { get; init; }

    /// <summary>
    /// Applies defaults and limits and returns a query that is safe to run.
    /// </summary>
    /// <param name="today">Current date used for the default range.</param>
    public OrdersRevenueQuery Normalize(DateOnly today) =>
        new((Range ?? new StatsFilter()).Normalize(today) with { ChannelId = null }, OrderStatusId is > 0 ? OrderStatusId : null);
}

/// <summary>
/// Normalized orders and revenue filter.
/// </summary>
/// <param name="Range">Range and grouping. <see cref="StatsQuery.ChannelId"/> is always <c>null</c>.</param>
/// <param name="OrderStatusId">Order status ID, or <c>null</c> for all statuses. Unknown IDs are treated as all statuses by the service.</param>
public sealed record OrdersRevenueQuery(StatsQuery Range, int? OrderStatusId);

/// <summary>
/// Input of the orders and revenue <c>LOAD</c> page command.
/// </summary>
public sealed record OrdersRevenueLoadRequest
{
    /// <summary>
    /// Report filter. <c>null</c> means defaults.
    /// </summary>
    public OrdersRevenueFilter? Filter { get; init; }

    /// <summary>
    /// When <c>true</c>, cached data for the filter is dropped and read again from the database.
    /// </summary>
    public bool Refresh { get; init; }
}

/// <summary>
/// KPIs of the range compared with the previous period. With a status filter, only orders in that status.
/// </summary>
/// <param name="Orders">Number of orders.</param>
/// <param name="Revenue">Sum of order grand totals (as stored; incl. shipping and tax).</param>
/// <param name="AverageOrderValue">Revenue / orders. Values are <c>null</c> for a period without orders.</param>
/// <param name="ItemsSold">Sum of order item quantities.</param>
public sealed record OrdersRevenueTotals(
    StatsValueComparison Orders,
    StatsValueComparison Revenue,
    StatsValueComparison AverageOrderValue,
    StatsValueComparison ItemsSold);

/// <summary>
/// Orders and revenue report.
/// </summary>
/// <param name="From">Applied range start (inclusive).</param>
/// <param name="To">Applied range end (inclusive).</param>
/// <param name="Grouping">Applied grouping.</param>
/// <param name="OrderStatusId">Applied status filter, <c>null</c> for all statuses.</param>
/// <param name="Statuses">Order statuses for the status filter, in status order.</param>
/// <param name="Periods">Period axis of <paramref name="Orders"/> and <paramref name="Revenue"/>.</param>
/// <param name="Orders">Orders per period (status filter applied).</param>
/// <param name="Revenue">Revenue (order grand totals) per period (status filter applied).</param>
/// <param name="Totals">KPIs vs the previous period (status filter applied).</param>
/// <param name="ByStatus">
/// Orders per status in the range, in status order. Value = orders, secondary value = revenue. Not affected by the status filter.
/// </param>
/// <param name="TopProducts">
/// Products with the most revenue (item total prices) in the range, by SKU (item name when there is no SKU), status filter applied.
/// Value = revenue, secondary value = quantity, with the previous period revenue and change.
/// </param>
/// <param name="CommerceAvailable"><c>false</c> when the commerce tables do not exist; the report is then empty.</param>
public sealed record OrdersRevenueResult(
    DateOnly From,
    DateOnly To,
    StatsGrouping Grouping,
    int? OrderStatusId,
    IReadOnlyList<CommerceOrderStatusOption> Statuses,
    IReadOnlyList<StatsPeriod> Periods,
    StatsValueSeries Orders,
    StatsValueSeries Revenue,
    OrdersRevenueTotals Totals,
    StatsRankedResult ByStatus,
    StatsRankedResult TopProducts,
    bool CommerceAvailable)
{
    /// <summary>
    /// Path of the native Orders application's listing, relative to the admin root. <c>null</c> when it is not available.
    /// </summary>
    public string? OrdersPath { get; init; }

    /// <summary>
    /// When the data was read from the database. Can be older than the request when served from cache.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// Orders and revenue (order grand totals) on one day, as returned by the SQL aggregate.
/// </summary>
public sealed record OrdersRevenueDailyRow(DateOnly Date, int Orders, decimal Revenue);

/// <summary>
/// Orders and revenue of one status in the range.
/// </summary>
/// <param name="StatusId">Status ID.</param>
/// <param name="CodeName">Status code name (<c>OrderStatusName</c>).</param>
/// <param name="DisplayName">Status display name.</param>
/// <param name="Orders">Orders in the status.</param>
/// <param name="Revenue">Revenue of the orders.</param>
public sealed record OrdersRevenueStatusRow(int StatusId, string CodeName, string DisplayName, int Orders, decimal Revenue);

/// <summary>
/// One product (grouped by SKU, or by item name when there is no SKU) in the range and in the previous period.
/// </summary>
/// <param name="Key">Group key: <c>sku:</c> + SKU, <c>name:</c> + item name, or empty for items without both.</param>
/// <param name="Sku">SKU snapshot, <c>null</c> when empty.</param>
/// <param name="Name">Item name snapshot (latest-looking via <c>MAX</c>), <c>null</c> when empty.</param>
/// <param name="Revenue">Sum of item total prices in the range.</param>
/// <param name="Quantity">Sum of item quantities in the range.</param>
/// <param name="PreviousRevenue">Sum of item total prices in the previous period.</param>
public sealed record OrdersRevenueProductRow(string Key, string? Sku, string? Name, decimal Revenue, decimal Quantity, decimal PreviousRevenue);

/// <summary>
/// Aggregated commerce data. <see cref="Daily"/> starts at the previous period.
/// </summary>
/// <param name="CommerceAvailable"><c>false</c> when the commerce tables do not exist.</param>
/// <param name="Daily">Orders and revenue per day (status filter applied), previous period + range.</param>
/// <param name="ItemsSold">Item quantities in the range and in the previous period (status filter applied).</param>
/// <param name="ByStatus">Orders per status in the range (no status filter), in status order.</param>
/// <param name="Products">Top products (status filter applied).</param>
/// <param name="ProductCount">Number of products with revenue in the range (not only <paramref name="Products"/>).</param>
/// <param name="ProductRevenue">Item revenue of all products in the range (not only <paramref name="Products"/>).</param>
public sealed record OrdersRevenueReportData(
    bool CommerceAvailable,
    IReadOnlyList<OrdersRevenueDailyRow> Daily,
    (decimal Current, decimal Previous) ItemsSold,
    IReadOnlyList<OrdersRevenueStatusRow> ByStatus,
    IReadOnlyList<OrdersRevenueProductRow> Products,
    int ProductCount,
    decimal ProductRevenue)
{
    public static OrdersRevenueReportData Empty { get; } = new(true, [], (0, 0), [], [], 0, 0);

    public static OrdersRevenueReportData Unavailable { get; } = Empty with { CommerceAvailable = false };
}

/// <summary>
/// Commerce data with the time it was read. This is the cached value.
/// </summary>
internal sealed record OrdersRevenueSnapshot(OrdersRevenueReportData Data, DateTimeOffset ReadAt);
