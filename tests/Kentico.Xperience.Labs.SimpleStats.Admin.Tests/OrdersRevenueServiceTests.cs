using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.DigitalCommerce.UIPages;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.Commerce;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.OrdersRevenue;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class OrdersRevenueServiceTests
{
    private static readonly StatsQuery range = new(new(2026, 9, 1), new(2026, 9, 30), StatsGrouping.Day, null);
    private static readonly OrdersRevenueQuery query = new(range, null);

    private FakeRepository repository = null!;
    private FakeCache cache = null!;
    private FakeClock clock = null!;
    private FakeAdminLinks adminLinks = null!;
    private FakeAmountFormatter amountFormatter = null!;
    private OrdersRevenueService service = null!;

    [SetUp]
    public void SetUp()
    {
        repository = new FakeRepository();
        cache = new FakeCache();
        clock = new FakeClock(new DateTimeOffset(2026, 9, 30, 10, 0, 0, TimeSpan.Zero));
        adminLinks = new FakeAdminLinks();
        amountFormatter = new FakeAmountFormatter();
        service = new OrdersRevenueService(repository, cache, cache, adminLinks, amountFormatter, clock);
    }

    [Test]
    public async Task GetReport_ReadsPreviousPeriodAndRangeInOneCall()
    {
        var result = await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.That(repository.DataCalls, Is.EqualTo(1));
        Assert.That(repository.LastCall, Is.EqualTo((new DateOnly(2026, 8, 2), range.From, range.To, (int?)null, OrdersRevenueReportBuilder.TopLimit)));
        Assert.That(result.Statuses, Is.EqualTo(repository.Statuses));
    }

    [Test]
    public async Task GetReport_KnownStatus_IsApplied()
    {
        var result = await service.GetReport(query with { OrderStatusId = 4 }, refresh: false, CancellationToken.None);

        Assert.That(repository.LastCall!.Value.StatusId, Is.EqualTo(4));
        Assert.That(result.OrderStatusId, Is.EqualTo(4));
    }

    [Test]
    public async Task GetReport_UnknownStatus_MeansAll()
    {
        var result = await service.GetReport(query with { OrderStatusId = 99 }, refresh: false, CancellationToken.None);

        Assert.That(repository.LastCall!.Value.StatusId, Is.Null);
        Assert.That(result.OrderStatusId, Is.Null);
    }

    [Test]
    public async Task GetReport_TablesMissing_EmptyReportWithoutStatuses()
    {
        repository.Statuses = [];
        repository.Data = OrdersRevenueReportData.Unavailable;

        var result = await service.GetReport(query with { OrderStatusId = 4 }, refresh: false, CancellationToken.None);

        Assert.That(result.CommerceAvailable, Is.False);
        Assert.That(result.OrderStatusId, Is.Null);
        Assert.That(result.Statuses, Is.Empty);
        Assert.That(result.Totals.Orders.Current, Is.Zero);
    }

    [Test]
    public async Task GetReport_LinksOrdersListing()
    {
        var result = await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.That(result.OrdersPath, Is.EqualTo("/OrdersList"));
        Assert.That(adminLinks.Pages, Does.Contain(typeof(OrdersList)));
    }

    [Test]
    public async Task GetReport_WorksWithoutLinks()
    {
        adminLinks.ReturnNull = true;

        var result = await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.That(result.OrdersPath, Is.Null);
    }

    [Test]
    public async Task GetReport_UsesCachedData_WhenNotRefreshing()
    {
        var first = await service.GetReport(query, refresh: false, CancellationToken.None);

        repository.Data = OrdersRevenueReportData.Empty with { Daily = [new(new(2026, 9, 2), 3, 30m)] };
        clock.Now = clock.Now.AddMinutes(1);
        var second = await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.That(repository.DataCalls, Is.EqualTo(1));
        Assert.That(repository.StatusCalls, Is.EqualTo(1));
        Assert.That(second.Totals.Orders.Current, Is.Zero);
        Assert.That(second.UpdatedAt, Is.EqualTo(first.UpdatedAt));
    }

    [Test]
    public async Task GetReport_Refresh_ReadsDataAndStatusesAgain_AndSetsUpdatedAtEverywhere()
    {
        await service.GetReport(query, refresh: false, CancellationToken.None);

        repository.Data = OrdersRevenueReportData.Empty with { Daily = [new(new(2026, 9, 2), 3, 30m)] };
        clock.Now = clock.Now.AddMinutes(1);
        var refreshed = await service.GetReport(query, refresh: true, CancellationToken.None);

        Assert.That(repository.DataCalls, Is.EqualTo(2));
        Assert.That(repository.StatusCalls, Is.EqualTo(2));
        Assert.That(refreshed.Totals.Orders.Current, Is.EqualTo(3m));
        Assert.That(refreshed.UpdatedAt, Is.EqualTo(clock.Now));
        Assert.That(refreshed.ByStatus.UpdatedAt, Is.EqualTo(clock.Now));
        Assert.That(refreshed.TopProducts.UpdatedAt, Is.EqualTo(clock.Now));
    }

    [Test]
    public async Task GetReport_CacheKey_SplitsOnStatus_NotOnChannelOrGrouping()
    {
        await service.GetReport(query, refresh: false, CancellationToken.None);
        await service.GetReport(query with { Range = range with { ChannelId = 2 } }, refresh: false, CancellationToken.None);
        var byWeek = await service.GetReport(query with { Range = range with { Grouping = StatsGrouping.Week } }, refresh: false, CancellationToken.None);
        await service.GetReport(query with { OrderStatusId = 99 }, refresh: false, CancellationToken.None);

        Assert.That(repository.DataCalls, Is.EqualTo(1));
        Assert.That(byWeek.Grouping, Is.EqualTo(StatsGrouping.Week));

        await service.GetReport(query with { OrderStatusId = 1 }, refresh: false, CancellationToken.None);
        Assert.That(repository.DataCalls, Is.EqualTo(2));
    }

    [Test]
    public async Task GetReport_CachesWithExpiryOnly_UnderOwnKeys()
    {
        await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.That(cache.Settings, Has.Count.EqualTo(2));
        Assert.That(cache.Settings.Select(s => s.GetCacheDependency), Is.All.Null);
        Assert.That(cache.Settings.Select(s => s.CacheMinutes), Is.All.EqualTo(StatsCache.CacheMinutes));
        Assert.That(cache.Settings.Select(s => s.CacheItemName), Has.Some.Contains("orders-revenue-statuses"));
        Assert.That(cache.Settings.Select(s => s.CacheItemName), Has.Some.Contains("orders-revenue|"));
    }

    [Test]
    public async Task GetReport_FormatsAmountsOnly_WithTheFormatter()
    {
        repository.Data = OrdersRevenueReportData.Empty with
        {
            Daily = [new(new(2026, 8, 20), 1, 10m), new(new(2026, 9, 2), 2, 30m)],
            ItemsSold = (4m, 1m),
            ByStatus = [new(1, "Fulfilled", "Fulfilled", 2, 30m)],
            Products = [new("sku:A", "A", "AeroPress", 25.9m, 1m, 12.5m)],
            ProductCount = 1,
            ProductRevenue = 25.9m,
        };

        var result = await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.That(result.Totals.Revenue.CurrentText, Is.EqualTo("$30.00"));
        Assert.That(result.Totals.Revenue.PreviousText, Is.EqualTo("$10.00"));
        Assert.That(result.Totals.AverageOrderValue.CurrentText, Is.EqualTo("$15.00"));
        Assert.That(result.Totals.Orders.CurrentText, Is.Null);
        Assert.That(result.Totals.ItemsSold.CurrentText, Is.Null);
        Assert.That(result.Revenue.Texts, Has.Count.EqualTo(30));
        Assert.That(result.Revenue.Texts![1], Is.EqualTo("$30.00"));
        Assert.That(result.Revenue.TotalText, Is.EqualTo("$30.00"));
        Assert.That(result.Orders.Texts, Is.Null);
        Assert.That(result.ByStatus.Items.Single().SecondaryValueText, Is.EqualTo("$30.00"));
        Assert.That(result.ByStatus.Items.Single().ValueText, Is.Null);
        Assert.That(result.TopProducts.Items.Single().ValueText, Is.EqualTo("$25.90"));
        Assert.That(result.TopProducts.Items.Single().PreviousValueText, Is.EqualTo("$12.50"));
        Assert.That(result.TopProducts.Items.Single().SecondaryValueText, Is.Null);
        Assert.That(result.TopProducts.TotalText, Is.EqualTo("$25.90"));
    }

    [Test]
    public async Task GetReport_WithoutFormatter_SendsNoTexts()
    {
        amountFormatter.ReturnNull = true;
        repository.Data = OrdersRevenueReportData.Empty with
        {
            Daily = [new(new(2026, 9, 2), 2, 30m)],
            Products = [new("sku:A", "A", "AeroPress", 25.9m, 1m, 0m)],
        };

        var result = await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.That(result.Totals.Revenue.CurrentText, Is.Null);
        Assert.That(result.Revenue.Texts, Is.Null);
        Assert.That(result.TopProducts.Items.Single().ValueText, Is.Null);
        Assert.That(result.Totals.Revenue.Current, Is.EqualTo(30m));
    }

    private sealed class FakeAmountFormatter : IStatsAmountFormatter
    {
        public bool ReturnNull { get; set; }

        public string? Format(decimal amount) =>
            ReturnNull ? null : "$" + amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed class FakeRepository : IOrdersRevenueRepository
    {
        public OrdersRevenueReportData Data { get; set; } = OrdersRevenueReportData.Empty;

        public IReadOnlyList<CommerceOrderStatusOption> Statuses { get; set; } = [new(4, "Pending"), new(1, "Fulfilled")];

        public int DataCalls { get; private set; }

        public int StatusCalls { get; private set; }

        public (DateOnly PreviousFrom, DateOnly From, DateOnly To, int? StatusId, int Limit)? LastCall { get; private set; }

        public Task<OrdersRevenueReportData> GetData(DateOnly previousFrom, DateOnly from, DateOnly to, int? orderStatusId, int limit, CancellationToken cancellationToken)
        {
            DataCalls++;
            LastCall = (previousFrom, from, to, orderStatusId, limit);
            return Task.FromResult(Data);
        }

        public Task<IReadOnlyList<CommerceOrderStatusOption>> GetStatuses(CancellationToken cancellationToken)
        {
            StatusCalls++;
            return Task.FromResult(Statuses);
        }
    }

    private sealed class FakeAdminLinks : IStatsAdminLinks
    {
        public bool ReturnNull { get; set; }

        public List<Type> Pages { get; } = [];

        public string? GetPath<TPage>(PageParameterValues? parameters = null)
        {
            Pages.Add(typeof(TPage));
            return ReturnNull ? null : $"/{typeof(TPage).Name}";
        }
    }
}
