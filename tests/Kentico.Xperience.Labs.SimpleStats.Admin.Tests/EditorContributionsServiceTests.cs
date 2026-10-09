using CMS.Core;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.EditorContributions;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PublishingActivity;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class EditorContributionsServiceTests
{
    private static readonly PublishingActivityQuery query = new(new(new(2026, 9, 8), new(2026, 10, 7), StatsGrouping.Day, null), null);

    private FakeRepository repository = null!;
    private FakeSettings settings = null!;
    private FakeCache cache = null!;
    private FakeClock clock = null!;
    private EditorContributionsService service = null!;

    [SetUp]
    public void SetUp()
    {
        repository = new FakeRepository();
        settings = new FakeSettings();
        settings.Values[StatsContentVersionHistory.EnabledSettingsKey] = "True";
        settings.Values[StatsContentVersionHistory.LengthSettingsKey] = "20";
        cache = new FakeCache();
        clock = new FakeClock(new DateTimeOffset(2026, 10, 7, 10, 0, 0, TimeSpan.Zero));
        service = new EditorContributionsService(repository, settings, cache, cache, new FakeAdminLinks(), clock);
    }

    [Test]
    public async Task GetReport_PassesQueryAndHistoryToRepository()
    {
        var filtered = query with { Kind = "Website", Range = query.Range with { ChannelId = 2 } };

        var result = await service.GetReport(filtered, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(repository.Calls.Single(), Is.EqualTo((filtered, true)));
            Assert.That(result.Kind, Is.EqualTo("Website"));
            Assert.That(result.ChannelId, Is.EqualTo(2));
            Assert.That(result.VersionHistoryEnabled, Is.True);
            Assert.That(result.VersionHistoryLength, Is.EqualTo(20));
            Assert.That(result.Published, Is.Not.Null);
        });
    }

    [Test]
    public async Task GetReport_HistoryDisabled_DoesNotReadPublishes()
    {
        settings.Values[StatsContentVersionHistory.EnabledSettingsKey] = "False";

        var result = await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(repository.Calls.Single().WithPublished, Is.False);
            Assert.That(result.Published, Is.Null);
        });
    }

    [Test]
    public async Task GetReport_LinksUsers()
    {
        repository.Data = EditorContributionsData.Empty with { Users = [new(53, "Admin", false, 1, 0, 0, 1)] };

        var result = await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.That(result.ByUser.Single().AdminPath, Is.EqualTo("/UserEdit"));
    }

    [Test]
    public async Task GetReport_Refresh_ReadsDatabaseAndSetsUpdatedAtEverywhere()
    {
        await service.GetReport(query, refresh: false, CancellationToken.None);

        repository.Data = EditorContributionsData.Empty with { Totals = EditorTotalsRow.Empty with { Created = 1 } };
        clock.Now = clock.Now.AddMinutes(1);
        var cached = await service.GetReport(query, refresh: false, CancellationToken.None);
        var refreshed = await service.GetReport(query, refresh: true, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(cached.Created.Current, Is.Zero);
            Assert.That(repository.Calls, Has.Count.EqualTo(2));
            Assert.That(refreshed.Created.Current, Is.EqualTo(1));
            Assert.That(refreshed.UpdatedAt, Is.EqualTo(clock.Now));
            Assert.That(refreshed.Series.UpdatedAt, Is.EqualTo(clock.Now));
        });
    }

    [Test]
    public async Task GetReport_CachesPerRangeKindChannelAndHistory_NotGrouping()
    {
        await service.GetReport(query, refresh: false, CancellationToken.None);
        await service.GetReport(query with { Range = query.Range with { Grouping = StatsGrouping.Month } }, refresh: false, CancellationToken.None);
        await service.GetReport(query with { Kind = "Website" }, refresh: false, CancellationToken.None);
        await service.GetReport(query with { Kind = "Website", Range = query.Range with { ChannelId = 2 } }, refresh: false, CancellationToken.None);
        settings.Values[StatsContentVersionHistory.EnabledSettingsKey] = "False";
        await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(repository.Calls, Has.Count.EqualTo(4));
            Assert.That(cache.Settings.Select(s => s.GetCacheDependency), Is.All.Null);
            Assert.That(cache.Settings.Select(s => s.CacheMinutes), Is.All.EqualTo(StatsCache.CacheMinutes));
            Assert.That(cache.Settings[0].CacheItemName, Does.Contain("editor-contributions"));
        });
    }

    private sealed class FakeRepository : IEditorContributionsRepository
    {
        public EditorContributionsData Data { get; set; } = EditorContributionsData.Empty;

        public List<(PublishingActivityQuery Query, bool WithPublished)> Calls { get; } = [];

        public Task<EditorContributionsData> GetData(PublishingActivityQuery query, bool withPublished, CancellationToken cancellationToken)
        {
            Calls.Add((query, withPublished));
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
