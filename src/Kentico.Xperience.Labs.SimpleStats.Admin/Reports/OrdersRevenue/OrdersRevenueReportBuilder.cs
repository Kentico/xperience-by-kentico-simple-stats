using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.Commerce;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.OrdersRevenue;

/// <summary>
/// Turns aggregated commerce data into the orders and revenue report.
/// </summary>
internal static class OrdersRevenueReportBuilder
{
    /// <summary>
    /// Rows of the top products list.
    /// </summary>
    public const int TopLimit = 10;

    public static StatsSeriesDefinition OrdersSeries { get; } = new("orders", "Orders");

    public static StatsSeriesDefinition RevenueSeries { get; } = new("revenue", "Revenue");

    /// <summary>
    /// Label of products without a SKU and name.
    /// </summary>
    public const string UnnamedProductLabel = "(No name)";

    /// <summary>
    /// Builds the report.
    /// </summary>
    /// <param name="query">Normalized filter with a status ID that exists (or <c>null</c>).</param>
    /// <param name="data">Data from the start of the previous period (see <see cref="StatsComparison.GetPreviousRange"/>) to the end of the range.</param>
    /// <param name="statuses">Order statuses for the status filter.</param>
    public static OrdersRevenueResult Build(OrdersRevenueQuery query, OrdersRevenueReportData data, IReadOnlyList<CommerceOrderStatusOption> statuses)
    {
        var range = query.Range with { ChannelId = null };
        var (previousFrom, previousTo) = StatsComparison.GetPreviousRange(range);

        var dailyValues = data.Daily
            .SelectMany(row => new StatsDailyValue[]
            {
                new(OrdersSeries.Key, row.Date, row.Orders),
                new(RevenueSeries.Key, row.Date, row.Revenue),
            })
            .ToList();

        // Rows before the range are the previous period; the series use only the range.
        var orders = StatsTimeSeriesBuilder.BuildValueSeries(range, dailyValues, OrdersSeries, StatsValueKind.Count);
        var revenue = StatsTimeSeriesBuilder.BuildValueSeries(range, dailyValues, RevenueSeries, StatsValueKind.Amount);

        (int Orders, decimal Revenue) Sum(DateOnly from, DateOnly to)
        {
            var rows = data.Daily.Where(row => row.Date >= from && row.Date <= to).ToList();
            return (rows.Sum(row => Math.Max(row.Orders, 0)), rows.Sum(row => Math.Max(row.Revenue, 0)));
        }

        var (Orders, Revenue) = Sum(range.From, range.To);
        var previous = Sum(previousFrom, previousTo);

        var totals = new OrdersRevenueTotals(
            StatsValueComparison.Create(range, StatsValueKind.Count, Orders, previous.Orders),
            StatsValueComparison.Create(range, StatsValueKind.Amount, Revenue, previous.Revenue),
            StatsValueComparison.Create(
                range,
                StatsValueKind.Amount,
                StatsValues.Divide(Revenue, Orders),
                StatsValues.Divide(previous.Revenue, previous.Orders)),
            StatsValueComparison.Create(range, StatsValueKind.Count, Math.Max(data.ItemsSold.Current, 0), Math.Max(data.ItemsSold.Previous, 0)));

        return new(
            range.From,
            range.To,
            range.Grouping,
            query.OrderStatusId,
            statuses,
            StatsPeriods.Build(range.From, range.To, range.Grouping),
            orders,
            revenue,
            totals,
            BuildByStatus(range, data.ByStatus),
            BuildProducts(range, data),
            data.CommerceAvailable);
    }

    private static StatsRankedResult BuildByStatus(StatsQuery range, IReadOnlyList<OrdersRevenueStatusRow> rows)
    {
        var entries = rows.Select(row => new StatsRankedEntry(
            Key: $"status:{row.StatusId}",
            Label: row.DisplayName,
            SecondaryLabel: null,
            Value: row.Orders,
            SecondaryValue: StatsValues.Round(row.Revenue, StatsValueKind.Amount),
            Url: null)
        {
            Tone = OrderStatusTones.Get(row.CodeName, row.DisplayName),
        });

        // Status order (not by value), so colors and rows stay in the order of the project's statuses.
        var result = StatsRankedBuilder.Build(range, entries, total: 0, itemCount: 0, limit: int.MaxValue, keepOrder: true);

        return result with { ValueKind = StatsValueKind.Count, SecondaryValueKind = StatsValueKind.Amount };
    }

    private static StatsRankedResult BuildProducts(StatsQuery range, OrdersRevenueReportData data)
    {
        var entries = data.Products.Select(row => new StatsRankedEntry(
            Key: row.Key,
            Label: row.Name ?? row.Sku ?? UnnamedProductLabel,
            SecondaryLabel: row.Sku,
            Value: StatsValues.Round(row.Revenue, StatsValueKind.Amount),
            SecondaryValue: Math.Max(row.Quantity, 0),
            Url: null)
        {
            PreviousValue = StatsValues.Round(Math.Max(row.PreviousRevenue, 0), StatsValueKind.Amount),
        });

        var result = StatsRankedBuilder.Build(
            range,
            entries,
            StatsValues.Round(data.ProductRevenue, StatsValueKind.Amount),
            data.ProductCount,
            TopLimit);

        return result with { ValueKind = StatsValueKind.Amount, SecondaryValueKind = StatsValueKind.Count };
    }
}
