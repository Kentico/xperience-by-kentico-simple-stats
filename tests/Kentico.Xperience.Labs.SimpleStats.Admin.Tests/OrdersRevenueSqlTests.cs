using CMS.Commerce;

using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.OrdersRevenue;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class OrdersRevenueSqlTests
{
    private static readonly DateOnly today = new(2026, 9, 30);

    [Test]
    public void BuildReport_ChecksTablesFirst_AndReturnsEarly()
    {
        string sql = OrdersRevenueSql.BuildReport(filterByStatus: false);

        int check = sql.IndexOf("OBJECT_ID(N'[Commerce_Order]', N'U') IS NULL", StringComparison.Ordinal);
        Assert.That(check, Is.GreaterThan(0));
        Assert.That(sql, Does.Contain("OBJECT_ID(N'[Commerce_OrderItem]', N'U') IS NULL"));
        Assert.That(sql, Does.Contain("OBJECT_ID(N'[Commerce_OrderStatus]', N'U') IS NULL"));
        Assert.That(sql.IndexOf("RETURN;", StringComparison.Ordinal), Is.LessThan(sql.IndexOf("FROM [Commerce_Order] O", StringComparison.Ordinal)));
    }

    [Test]
    public void BuildReport_WithoutStatus_HasNoStatusParameter() =>
        Assert.That(OrdersRevenueSql.BuildReport(filterByStatus: false), Does.Not.Contain(OrdersRevenueSql.OrderStatusIdParameter));

    [Test]
    public void BuildReport_WithStatus_FiltersEveryResultExceptTheStatusBreakdown()
    {
        string sql = OrdersRevenueSql.BuildReport(filterByStatus: true);

        // Daily, items sold and top products; the status breakdown always shows every status.
        Assert.That(CountOf(sql, "AND O.[OrderOrderStatusID] = @OrderStatusId"), Is.EqualTo(3));

        int byStatus = sql.IndexOf("FROM [Commerce_OrderStatus] S", StringComparison.Ordinal);
        int nextStatement = sql.IndexOf(';', byStatus);
        Assert.That(sql[byStatus..nextStatement], Does.Not.Contain("@OrderStatusId"));
    }

    [Test]
    public void BuildReport_TreatsMissingTotalsAsZero_AndUsesParameters()
    {
        string sql = OrdersRevenueSql.BuildReport(filterByStatus: true);

        Assert.That(sql, Does.Contain("SUM(ISNULL(O.[OrderGrandTotal], 0))"));
        Assert.That(sql, Does.Contain("ISNULL(I.[OrderItemQuantity], 0)"));
        Assert.That(sql, Does.Contain("ISNULL(I.[OrderItemTotalPrice], 0)"));
        Assert.That(sql, Does.Contain("TOP (@Limit)"));
        Assert.That(sql, Does.Contain("@PreviousFrom").And.Contain("@From").And.Contain("@ToExclusive"));
        Assert.That(sql, Does.Contain("ORDER BY S.[OrderStatusOrder], S.[OrderStatusID]"));
    }

    [Test]
    public void BuildStatuses_ChecksTables_AndSortsByStatusOrder()
    {
        string sql = OrdersRevenueSql.BuildStatuses();

        Assert.That(sql, Does.Contain("OBJECT_ID(N'[Commerce_OrderStatus]', N'U') IS NULL"));
        Assert.That(sql, Does.Contain("ORDER BY S.[OrderStatusOrder], S.[OrderStatusID]"));
    }

    /// <summary>
    /// The SQL reads these columns; the product's Info classes must still have them.
    /// </summary>
    [Test]
    public void Sql_UsesColumnsOfTheCommerceInfoClasses()
    {
        string sql = OrdersRevenueSql.BuildReport(filterByStatus: true) + OrdersRevenueSql.BuildStatuses();

        string[] columns =
        [
            nameof(OrderInfo.OrderID),
            nameof(OrderInfo.OrderCreatedWhen),
            nameof(OrderInfo.OrderOrderStatusID),
            nameof(OrderInfo.OrderGrandTotal),
            nameof(OrderItemInfo.OrderItemOrderID),
            nameof(OrderItemInfo.OrderItemSKU),
            nameof(OrderItemInfo.OrderItemName),
            nameof(OrderItemInfo.OrderItemQuantity),
            nameof(OrderItemInfo.OrderItemTotalPrice),
            nameof(OrderStatusInfo.OrderStatusID),
            nameof(OrderStatusInfo.OrderStatusName),
            nameof(OrderStatusInfo.OrderStatusDisplayName),
            nameof(OrderStatusInfo.OrderStatusOrder),
        ];

        Assert.That(columns, Is.All.Matches<string>(column => sql.Contains($"[{column}]", StringComparison.Ordinal)));
    }

    [Test]
    public void Filter_Normalize_Defaults_AllStatusesLast30Days()
    {
        var query = new OrdersRevenueFilter().Normalize(today);

        Assert.That(query.OrderStatusId, Is.Null);
        Assert.That(query.Range, Is.EqualTo(new StatsFilter().Normalize(today)));
    }

    [TestCase(3, 3)]
    [TestCase(0, null)]
    [TestCase(-1, null)]
    [TestCase(null, null)]
    public void Filter_Normalize_StatusIdPositiveOnly(int? value, int? expected) =>
        Assert.That(new OrdersRevenueFilter { OrderStatusId = value }.Normalize(today).OrderStatusId, Is.EqualTo(expected));

    [Test]
    public void Filter_Normalize_DropsChannel_KeepsRangeAndGrouping()
    {
        var filter = new OrdersRevenueFilter
        {
            Range = new StatsFilter { From = new(2026, 9, 1), To = new(2026, 9, 10), Grouping = StatsGrouping.Week, ChannelId = 3 },
        };

        var query = filter.Normalize(today);

        Assert.That(query.Range, Is.EqualTo(new StatsQuery(new(2026, 9, 1), new(2026, 9, 10), StatsGrouping.Week, null)));
    }

    private static int CountOf(string text, string value)
    {
        int count = 0;
        for (int index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
