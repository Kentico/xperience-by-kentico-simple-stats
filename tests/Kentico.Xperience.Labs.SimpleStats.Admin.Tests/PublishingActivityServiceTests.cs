using CMS.Core;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PublishingActivity;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class PublishingActivityServiceTests
{
    private static readonly PublishingActivityQuery query = new(new(new(2026, 9, 8), new(2026, 10, 7), StatsGrouping.Day, null), null);

    private FakeRepository repository = null!;
    private FakeSettings settings = null!;
    private FakeCache cache = null!;
    private FakeClock clock = null!;
    private PublishingActivityService service = null!;

    [SetUp]
    public void SetUp()
    {
        repository = new FakeRepository();
        settings = new FakeSettings();
        settings.Values[PublishingActivityService.VersionHistoryEnabledSettingsKey] = "True";
        settings.Values[PublishingActivityService.VersionHistoryLengthSettingsKey] = "20";
        cache = new FakeCache();
        clock = new FakeClock(new DateTimeOffset(2026, 10, 7, 10, 0, 0, TimeSpan.Zero));
        service = new PublishingActivityService(repository, settings, cache, cache, new FakeAdminLinks(), clock);
    }

    [Test]
    public async Task GetReport_PassesQueryToRepository()
    {
        var filtered = query with { Kind = "Website", Range = query.Range with { ChannelId = 2 } };

        var result = await service.GetReport(filtered, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(repository.Queries.Single(), Is.EqualTo(filtered));
            Assert.That(result.Kind, Is.EqualTo("Website"));
            Assert.That(result.ChannelId, Is.EqualTo(2));
        });
    }

    [TestCase("True", "20", true, 20)]
    [TestCase("true", "5", true, 5)]
    [TestCase("False", "20", false, 20)]
    [TestCase("", "", false, 0)]
    [TestCase("yes", "abc", false, 0)]
    public async Task GetReport_ReadsTheVersionHistorySettings(string enabled, string length, bool expectedEnabled, int expectedLength)
    {
        settings.Values[PublishingActivityService.VersionHistoryEnabledSettingsKey] = enabled;
        settings.Values[PublishingActivityService.VersionHistoryLengthSettingsKey] = length;

        var result = await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.VersionHistoryEnabled, Is.EqualTo(expectedEnabled));
            Assert.That(result.VersionHistoryLength, Is.EqualTo(expectedLength));
            Assert.That(result.Updates is not null, Is.EqualTo(expectedEnabled));
        });
    }

    [Test]
    public void SettingsKeys_MatchTheProduct() =>
        Assert.Multiple(() =>
        {
            Assert.That(PublishingActivityService.VersionHistoryEnabledSettingsKey, Is.EqualTo("CMSContentVersionHistoryEnable"));
            Assert.That(PublishingActivityService.VersionHistoryLengthSettingsKey, Is.EqualTo("CMSContentVersionHistoryLength"));
        });

    [Test]
    public async Task GetReport_UsesCachedData_WhenNotRefreshing()
    {
        var first = await service.GetReport(query, refresh: false, CancellationToken.None);

        repository.Data = PublishingActivityData.Empty with { Daily = [new(PublishingActivitySeriesKeys.Created, new(2026, 10, 1), 1)] };
        clock.Now = clock.Now.AddMinutes(1);
        var second = await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(repository.Queries, Has.Count.EqualTo(1));
            Assert.That(second.Created.Current, Is.Zero);
            Assert.That(second.UpdatedAt, Is.EqualTo(first.UpdatedAt));
        });
    }

    [Test]
    public async Task GetReport_Refresh_ReadsDatabaseAndSetsUpdatedAtEverywhere()
    {
        await service.GetReport(query, refresh: false, CancellationToken.None);

        repository.Data = PublishingActivityData.Empty with { Daily = [new(PublishingActivitySeriesKeys.Created, new(2026, 10, 1), 1)] };
        clock.Now = clock.Now.AddMinutes(1);
        var refreshed = await service.GetReport(query, refresh: true, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(repository.Queries, Has.Count.EqualTo(2));
            Assert.That(refreshed.Created.Current, Is.EqualTo(1));
            Assert.That(refreshed.UpdatedAt, Is.EqualTo(clock.Now));
            Assert.That(refreshed.Series.UpdatedAt, Is.EqualTo(clock.Now));
        });
    }

    [Test]
    public async Task GetReport_CachesPerRangeKindAndChannel_NotGrouping_WithExpiryOnly()
    {
        await service.GetReport(query, refresh: false, CancellationToken.None);
        await service.GetReport(query with { Range = query.Range with { Grouping = StatsGrouping.Month } }, refresh: false, CancellationToken.None);
        await service.GetReport(query with { Kind = "Website" }, refresh: false, CancellationToken.None);
        await service.GetReport(query with { Kind = "Website", Range = query.Range with { ChannelId = 2 } }, refresh: false, CancellationToken.None);
        await service.GetReport(query with { Range = query.Range with { From = query.Range.From.AddDays(1) } }, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(repository.Queries, Has.Count.EqualTo(4));
            Assert.That(cache.Settings.Select(s => s.GetCacheDependency), Is.All.Null);
            Assert.That(cache.Settings.Select(s => s.CacheMinutes), Is.All.EqualTo(StatsCache.CacheMinutes));
            Assert.That(cache.Settings[0].CacheItemName, Does.Contain("publishing-activity"));
        });
    }

    private sealed class FakeRepository : IPublishingActivityRepository
    {
        public PublishingActivityData Data { get; set; } = PublishingActivityData.Empty;

        public List<PublishingActivityQuery> Queries { get; } = [];

        public Task<PublishingActivityData> GetData(PublishingActivityQuery query, CancellationToken cancellationToken)
        {
            Queries.Add(query);
            return Task.FromResult(Data);
        }
    }

    private sealed class FakeSettings : ISettingsService
    {
        public Dictionary<string, string> Values { get; } = [];

        public string this[string keyName] => Values.TryGetValue(keyName, out string? value) ? value : string.Empty;
    }

    private sealed class FakeAdminLinks : IStatsAdminLinks
    {
        public string? GetPath<TPage>(PageParameterValues? parameters = null) => $"/{typeof(TPage).Name}";
    }
}
