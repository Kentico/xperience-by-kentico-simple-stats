using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.Commerce;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.OrdersRevenue;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class OrdersRevenueReportBuilderTests
{
    private static readonly StatsQuery range = new(new(2026, 9, 1), new(2026, 9, 30), StatsGrouping.Day, null);
    private static readonly OrdersRevenueQuery query = new(range, null);
    private static readonly CommerceOrderStatusOption[] statuses = [new(4, "Pending"), new(1, "Fulfilled")];

    [Test]
    public void Build_NoOrders_EmptySeries_NullAverageAndChanges()
    {
        var result = OrdersRevenueReportBuilder.Build(query, OrdersRevenueReportData.Empty, statuses);

        Assert.That(result.CommerceAvailable, Is.True);
        Assert.That(result.Periods, Has.Count.EqualTo(30));
        Assert.That(result.Orders.Values, Has.Count.EqualTo(30).And.All.Zero);
        Assert.That(result.Revenue.Values, Has.Count.EqualTo(30).And.All.Zero);
        Assert.That(result.Orders.Kind, Is.EqualTo(StatsValueKind.Count));
        Assert.That(result.Revenue.Kind, Is.EqualTo(StatsValueKind.Amount));
        Assert.That(result.Totals.Orders.Current, Is.Zero);
        Assert.That(result.Totals.Orders.Change, Is.Null);
        Assert.That(result.Totals.Revenue.Current, Is.Zero);
        Assert.That(result.Totals.AverageOrderValue.Current, Is.Null);
        Assert.That(result.Totals.AverageOrderValue.Previous, Is.Null);
        Assert.That(result.Totals.AverageOrderValue.Change, Is.Null);
        Assert.That(result.Totals.ItemsSold.Current, Is.Zero);
        Assert.That(result.ByStatus.Items, Is.Empty);
        Assert.That(result.TopProducts.Items, Is.Empty);
        Assert.That(result.Statuses, Is.EqualTo(statuses));
    }

    [Test]
    public void Build_CommerceUnavailable_IsFlagged()
    {
        var result = OrdersRevenueReportBuilder.Build(query, OrdersRevenueReportData.Unavailable, []);

        Assert.That(result.CommerceAvailable, Is.False);
        Assert.That(result.Totals.Orders.Current, Is.Zero);
    }

    [Test]
    public void Build_SplitsRangeAndPreviousPeriod_AndComputesKpis()
    {
        var data = OrdersRevenueReportData.Empty with
        {
            Daily =
            [
                new(new(2026, 8, 15), 2, 100m),      // previous period
                new(new(2026, 9, 1), 3, 100.005m),
                new(new(2026, 9, 30), 1, 49.995m),
                new(new(2026, 10, 1), 9, 999m),      // after the range, ignored
            ],
            ItemsSold = (7.5m, 3m),
        };

        var result = OrdersRevenueReportBuilder.Build(query, data, statuses);

        Assert.That(result.Orders.Values[0], Is.EqualTo(3m));
        Assert.That(result.Orders.Values[29], Is.EqualTo(1m));
        Assert.That(result.Revenue.Values[0], Is.EqualTo(100.01m));
        Assert.That(result.Revenue.Total, Is.EqualTo(150.00m));

        Assert.That(result.Totals.Orders.Current, Is.EqualTo(4m));
        Assert.That(result.Totals.Orders.Previous, Is.EqualTo(2m));
        Assert.That(result.Totals.Orders.Change, Is.EqualTo(1.0).Within(1e-9));
        Assert.That(result.Totals.Revenue.Current, Is.EqualTo(150.00m));
        Assert.That(result.Totals.Revenue.Previous, Is.EqualTo(100m));
        Assert.That(result.Totals.AverageOrderValue.Current, Is.EqualTo(37.50m));
        Assert.That(result.Totals.AverageOrderValue.Previous, Is.EqualTo(50m));
        Assert.That(result.Totals.AverageOrderValue.Kind, Is.EqualTo(StatsValueKind.Amount));
        Assert.That(result.Totals.ItemsSold.Current, Is.EqualTo(7.5m));
        Assert.That(result.Totals.ItemsSold.Change, Is.EqualTo(1.5).Within(1e-9));
    }

    [Test]
    public void Build_PreviousPeriodWithoutOrders_ChangesNull_AverageOfPreviousNull()
    {
        var data = OrdersRevenueReportData.Empty with { Daily = [new(new(2026, 9, 2), 2, 30m)] };

        var result = OrdersRevenueReportBuilder.Build(query, data, statuses);

        Assert.That(result.Totals.Orders.Change, Is.Null);
        Assert.That(result.Totals.Revenue.Change, Is.Null);
        Assert.That(result.Totals.AverageOrderValue.Current, Is.EqualTo(15m));
        Assert.That(result.Totals.AverageOrderValue.Previous, Is.Null);
        Assert.That(result.Totals.AverageOrderValue.Change, Is.Null);
    }

    [Test]
    public void Build_OrdersWithoutGrandTotal_CountButAddNoRevenue()
    {
        // The SQL reads a missing grand total as 0, so the day has orders but no revenue.
        var data = OrdersRevenueReportData.Empty with { Daily = [new(new(2026, 9, 2), 2, 0m)] };

        var result = OrdersRevenueReportBuilder.Build(query, data, statuses);

        Assert.That(result.Totals.Orders.Current, Is.EqualTo(2m));
        Assert.That(result.Totals.Revenue.Current, Is.Zero);
        Assert.That(result.Totals.AverageOrderValue.Current, Is.Zero);
    }

    [Test]
    public void Build_KeepsStatusFilter_AndGrouping()
    {
        var result = OrdersRevenueReportBuilder.Build(query with { Range = range with { Grouping = StatsGrouping.Month }, OrderStatusId = 4 }, OrdersRevenueReportData.Empty, statuses);

        Assert.That(result.OrderStatusId, Is.EqualTo(4));
        Assert.That(result.Grouping, Is.EqualTo(StatsGrouping.Month));
        Assert.That(result.Periods, Has.Count.EqualTo(1));
        Assert.That(result.Orders.Values, Has.Count.EqualTo(1));
    }

    [Test]
    public void Build_ByStatus_StatusOrder_OrdersAndRevenue_SkipsEmptyStatuses()
    {
        var data = OrdersRevenueReportData.Empty with
        {
            ByStatus =
            [
                new(4, "Pending", "Pending", 2, 20.005m),
                new(2, "PaymentFailed", "Payment failed", 0, 0m),
                new(1, "Fulfilled", "Fulfilled", 5, 500m),
            ],
        };

        var result = OrdersRevenueReportBuilder.Build(query, data, statuses);

        Assert.That(result.ByStatus.Items.Select(i => i.Tone), Is.EqualTo(new StatsTone?[] { StatsTone.Caution, StatsTone.Done }));
        Assert.That(result.ByStatus.Items.Select(i => i.Label), Is.EqualTo(new[] { "Pending", "Fulfilled" }));
        Assert.That(result.ByStatus.Items.Select(i => i.Key), Is.EqualTo(new[] { "status:4", "status:1" }));
        Assert.That(result.ByStatus.Items[0].Value, Is.EqualTo(2m));
        Assert.That(result.ByStatus.Items[0].SecondaryValue, Is.EqualTo(20.01m));
        Assert.That(result.ByStatus.Total, Is.EqualTo(7m));
        Assert.That(result.ByStatus.ValueKind, Is.EqualTo(StatsValueKind.Count));
        Assert.That(result.ByStatus.SecondaryValueKind, Is.EqualTo(StatsValueKind.Amount));
    }

    [Test]
    public void Build_TopProducts_RevenueRanked_WithQuantityAndChange()
    {
        var data = OrdersRevenueReportData.Empty with
        {
            Products =
            [
                new("sku:A", "A", "AeroPress", 51.8m, 2m, 25.9m),
                new("name:Gift", null, "Gift", 10m, 1m, 0m),
                new("", null, null, 5.555m, 1m, 0m),
                new("sku:ZERO", "ZERO", "Free", 0m, 1m, 0m),
            ],
            ProductCount = 12,
            ProductRevenue = 200.004m,
        };

        var result = OrdersRevenueReportBuilder.Build(query, data, statuses);
        var items = result.TopProducts.Items;

        Assert.That(items.Select(i => i.Label), Is.EqualTo(new[] { "AeroPress", "Gift", OrdersRevenueReportBuilder.UnnamedProductLabel }));
        Assert.That(items[0].SecondaryLabel, Is.EqualTo("A"));
        Assert.That(items[1].SecondaryLabel, Is.Null);
        Assert.That(items[0].SecondaryValue, Is.EqualTo(2m));
        Assert.That(items[0].PreviousValue, Is.EqualTo(25.9m));
        Assert.That(items[0].Change, Is.EqualTo(1.0).Within(1e-9));
        Assert.That(items[1].Change, Is.Null);
        Assert.That(items[1].PreviousValue, Is.Zero);
        Assert.That(items[2].Value, Is.EqualTo(5.56m));
        Assert.That(result.TopProducts.Total, Is.EqualTo(200.00m));
        Assert.That(result.TopProducts.ItemCount, Is.EqualTo(12));
        Assert.That(items[0].Share, Is.EqualTo(51.8 / 200.0).Within(1e-9));
        Assert.That(result.TopProducts.ValueKind, Is.EqualTo(StatsValueKind.Amount));
        Assert.That(result.TopProducts.SecondaryValueKind, Is.EqualTo(StatsValueKind.Count));
    }

    [Test]
    public void Build_TopProducts_SkuOnly_UsesSkuAsLabel()
    {
        var data = OrdersRevenueReportData.Empty with { Products = [new("sku:X-1", "X-1", null, 5m, 1m, 0m)] };

        var result = OrdersRevenueReportBuilder.Build(query, data, statuses);

        Assert.That(result.TopProducts.Items.Single().Label, Is.EqualTo("X-1"));
    }
}
