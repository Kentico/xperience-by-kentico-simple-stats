using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContentInventory;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PageFreshness;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class PageFreshnessServiceTests
{
    private static readonly StatsQuery query = new(new(2026, 9, 8), new(2026, 10, 7), StatsGrouping.Day, null);

    private FakeRepository repository = null!;
    private FakeCache cache = null!;
    private FakeClock clock = null!;
    private PageFreshnessService service = null!;

    [SetUp]
    public void SetUp()
    {
        repository = new FakeRepository();
        cache = new FakeCache();
        clock = new FakeClock(new DateTimeOffset(2026, 10, 7, 10, 0, 0, TimeSpan.Zero));
        service = new PageFreshnessService(repository, cache, cache, clock);
    }

    [Test]
    public async Task GetReport_PassesQueryAndServerTimeToRepository()
    {
        var result = await service.GetReport(query with { ChannelId = 2 }, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(repository.Queries.Single(), Is.EqualTo(query with { ChannelId = 2 }));
            Assert.That(repository.Nows.Single(), Is.EqualTo(clock.GetLocalNow().DateTime));
            Assert.That(result.ChannelId, Is.EqualTo(2));
            Assert.That(result.From, Is.EqualTo(query.From));
            Assert.That(result.To, Is.EqualTo(query.To));
        });
    }

    [Test]
    public async Task GetReport_UsesCachedData_WhenNotRefreshing()
    {
        var first = await service.GetReport(query, refresh: false, CancellationToken.None);

        repository.Data = PageFreshnessData.Empty with { Totals = new(1, 0, 0, 0, 0, 1) };
        clock.Now = clock.Now.AddMinutes(1);
        var second = await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(repository.Queries, Has.Count.EqualTo(1));
            Assert.That(second.PublishedPages, Is.Zero);
            Assert.That(second.UpdatedAt, Is.EqualTo(first.UpdatedAt));
        });
    }

    [Test]
    public async Task GetReport_Refresh_ReadsDatabaseAndSetsUpdatedAtEverywhere()
    {
        await service.GetReport(query, refresh: false, CancellationToken.None);

        repository.Data = PageFreshnessData.Empty with { Totals = new(1, 0, 0, 0, 0, 1), AgePages = new(1, 0, 0, 0) };
        clock.Now = clock.Now.AddMinutes(1);
        var refreshed = await service.GetReport(query, refresh: true, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(repository.Queries, Has.Count.EqualTo(2));
            Assert.That(refreshed.PublishedPages, Is.EqualTo(1));
            Assert.That(refreshed.UpdatedAt, Is.EqualTo(clock.Now));
            Assert.That(refreshed.AgeBuckets.UpdatedAt, Is.EqualTo(clock.Now));
        });
    }

    [Test]
    public async Task GetReport_CachesPerRangeAndChannel_NotGrouping_WithExpiryOnly()
    {
        await service.GetReport(query, refresh: false, CancellationToken.None);
        await service.GetReport(query with { Grouping = StatsGrouping.Month }, refresh: false, CancellationToken.None);
        await service.GetReport(query with { ChannelId = 2 }, refresh: false, CancellationToken.None);
        await service.GetReport(query with { From = query.From.AddDays(1) }, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(repository.Queries, Has.Count.EqualTo(3));
            Assert.That(cache.Settings.Select(s => s.GetCacheDependency), Is.All.Null);
            Assert.That(cache.Settings.Select(s => s.CacheMinutes), Is.All.EqualTo(StatsCache.CacheMinutes));
            Assert.That(cache.Settings[0].CacheItemName, Does.Contain("page-freshness"));
        });
    }

    private sealed class FakeRepository : IPageFreshnessRepository
    {
        public PageFreshnessData Data { get; set; } = PageFreshnessData.Empty with { AgePages = ContentAgeRow.Empty };

        public List<StatsQuery> Queries { get; } = [];

        public List<DateTime> Nows { get; } = [];

        public Task<PageFreshnessData> GetData(StatsQuery query, DateTime now, CancellationToken cancellationToken)
        {
            Queries.Add(query);
            Nows.Add(now);
            return Task.FromResult(Data);
        }
    }
}
