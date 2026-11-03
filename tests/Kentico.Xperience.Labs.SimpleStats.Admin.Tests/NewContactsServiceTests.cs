using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.NewContacts;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class NewContactsServiceTests
{
    private static readonly StatsQuery query = new(new(2026, 9, 1), new(2026, 9, 30), StatsGrouping.Day, null);

    private FakeRepository repository = null!;
    private FakeCache cache = null!;
    private FakeClock clock = null!;
    private NewContactsService service = null!;

    [SetUp]
    public void SetUp()
    {
        repository = new FakeRepository();
        cache = new FakeCache();
        clock = new FakeClock(new DateTimeOffset(2026, 9, 30, 10, 0, 0, TimeSpan.Zero));
        service = new NewContactsService(repository, cache, cache, clock);
    }

    [Test]
    public async Task GetReport_PassesPreviousPeriodAndRangeToRepository()
    {
        await service.GetReport(query, refresh: false, CancellationToken.None);

        // Sep 1–30 (30 days): the previous period starts Aug 2.
        Assert.That(repository.LastCall, Is.EqualTo((new DateOnly(2026, 8, 2), query.To)));
    }

    [Test]
    public async Task GetReport_UsesCachedData_WhenNotRefreshing()
    {
        var first = await service.GetReport(query, refresh: false, CancellationToken.None);

        repository.Rows = [new(new(2026, 9, 2), 1, 1)];
        clock.Now = clock.Now.AddMinutes(1);
        var second = await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.That(repository.Calls, Is.EqualTo(1));
        Assert.That(second.Trend.Total, Is.Zero);
        Assert.That(second.Trend.UpdatedAt, Is.EqualTo(first.Trend.UpdatedAt));
    }

    [Test]
    public async Task GetReport_Refresh_ReadsDatabaseAndCachesFreshResult()
    {
        await service.GetReport(query, refresh: false, CancellationToken.None);

        repository.Rows = [new(new(2026, 9, 2), 3, 1)];
        clock.Now = clock.Now.AddMinutes(1);
        var refreshed = await service.GetReport(query, refresh: true, CancellationToken.None);

        Assert.That(refreshed.Identified, Is.EqualTo(3));
        Assert.That(refreshed.Anonymous, Is.EqualTo(1));
        Assert.That(refreshed.IdentifiedShare, Is.EqualTo(0.75).Within(1e-9));
        Assert.That(refreshed.Trend.UpdatedAt, Is.EqualTo(clock.Now));

        var afterRefresh = await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.That(repository.Calls, Is.EqualTo(2));
        Assert.That(afterRefresh.Trend.Total, Is.EqualTo(4));
        Assert.That(afterRefresh.Trend.UpdatedAt, Is.EqualTo(refreshed.Trend.UpdatedAt));
    }

    [Test]
    public async Task GetReport_IgnoresChannelAndGrouping_InCacheKeyAndResult()
    {
        await service.GetReport(query, refresh: false, CancellationToken.None);
        var withChannel = await service.GetReport(query with { ChannelId = 2 }, refresh: false, CancellationToken.None);
        var byMonth = await service.GetReport(query with { Grouping = StatsGrouping.Month }, refresh: false, CancellationToken.None);

        Assert.That(repository.Calls, Is.EqualTo(1));
        Assert.That(withChannel.Trend.ChannelId, Is.Null);
        Assert.That(byMonth.Trend.Grouping, Is.EqualTo(StatsGrouping.Month));

        await service.GetReport(query with { From = new(2026, 8, 1) }, refresh: false, CancellationToken.None);
        Assert.That(repository.Calls, Is.EqualTo(2));
    }

    [Test]
    public async Task GetReport_Refresh_OnlyDropsQueriedRange()
    {
        var otherQuery = query with { From = new(2026, 8, 1) };
        await service.GetReport(query, refresh: false, CancellationToken.None);
        await service.GetReport(otherQuery, refresh: false, CancellationToken.None);

        await service.GetReport(query, refresh: true, CancellationToken.None);
        await service.GetReport(otherQuery, refresh: false, CancellationToken.None);

        Assert.That(repository.Calls, Is.EqualTo(3));
    }

    [Test]
    public async Task GetReport_CachesWithExpiryOnly_UnderOwnKey()
    {
        await service.GetReport(query, refresh: false, CancellationToken.None);

        var settings = cache.Settings.Single();
        Assert.That(settings.GetCacheDependency, Is.Null);
        Assert.That(settings.CacheMinutes, Is.EqualTo(StatsCache.CacheMinutes));
        Assert.That(settings.CacheItemName, Does.Contain("new-contacts"));
    }

    private sealed class FakeRepository : INewContactsRepository
    {
        public IReadOnlyList<NewContactsDailyCount> Rows { get; set; } = [];

        public int Calls { get; private set; }

        public (DateOnly From, DateOnly To)? LastCall { get; private set; }

        public Task<IReadOnlyList<NewContactsDailyCount>> GetDailyCounts(DateOnly from, DateOnly to, CancellationToken cancellationToken)
        {
            Calls++;
            LastCall = (from, to);
            return Task.FromResult(Rows);
        }
    }
}
