using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.CampaignSources;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class CampaignSourcesServiceTests
{
    private static readonly CampaignSourcesQuery query = new(new(new(2026, 9, 1), new(2026, 9, 30), StatsGrouping.Day, null), null, null);

    // Sources and contents the data knows (the fake returns the same data for every query).
    private static readonly CampaignSourcesData known = CampaignSourcesData.Empty with
    {
        Sources = [new("newsletter", 3, 2, 0), new("google", 1, 1, 0)],
        Contents = [new("newsletter", "footer", 2, 1), new("newsletter", null, 1, 1)],
    };

    private FakeRepository repository = null!;
    private FakeCache cache = null!;
    private FakeClock clock = null!;
    private CampaignSourcesService service = null!;

    [SetUp]
    public void SetUp()
    {
        repository = new FakeRepository { Data = known };
        cache = new FakeCache();
        clock = new FakeClock(new DateTimeOffset(2026, 9, 30, 10, 0, 0, TimeSpan.Zero));
        service = new CampaignSourcesService(repository, repository, cache, cache, clock);
    }

    [Test]
    public async Task GetReport_PassesQueryToRepository()
    {
        var filtered = query with { Source = "newsletter", Content = "footer", Range = query.Range with { ChannelId = 2 } };

        var result = await service.GetReport(filtered, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            // All sources (the source options), the source (its contents), then the filter.
            Assert.That(repository.Queries, Is.EqualTo(new[]
            {
                filtered with { Source = null, Content = null },
                filtered with { Content = null },
                filtered,
            }));
            Assert.That(result.Source, Is.EqualTo("newsletter"));
            Assert.That(result.Content, Is.EqualTo("footer"));
            Assert.That(result.ChannelId, Is.EqualTo(2));
            Assert.That(result.UpdatedAt, Is.EqualTo(clock.Now));
            Assert.That(result.Pages.UpdatedAt, Is.EqualTo(clock.Now));
        });
    }

    [Test]
    public async Task GetReport_GroupingDoesNotSplitCache_FiltersDo()
    {
        await service.GetReport(query, refresh: false, CancellationToken.None);
        await service.GetReport(query with { Range = query.Range with { Grouping = StatsGrouping.Week } }, refresh: false, CancellationToken.None);
        await service.GetReport(query with { Source = "newsletter" }, refresh: false, CancellationToken.None);
        await service.GetReport(query with { Source = "newsletter", Content = string.Empty }, refresh: false, CancellationToken.None);
        await service.GetReport(query with { Source = "newsletter", Content = "footer" }, refresh: false, CancellationToken.None);

        Assert.That(repository.Queries, Has.Count.EqualTo(4));
    }

    [Test]
    public async Task GetReport_KnownValues_MatchCaseInsensitively_UseStoredValue()
    {
        var result = await service.GetReport(query with { Source = "NEWSLETTER", Content = "Footer" }, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(repository.Queries[^1], Is.EqualTo(query with { Source = "newsletter", Content = "footer" }));
            Assert.That(result.Source, Is.EqualTo("newsletter"));
            Assert.That(result.Content, Is.EqualTo("footer"));
        });
    }

    [Test]
    public async Task GetReport_UnknownSource_MeansAll_AndAddsNoCacheEntry()
    {
        await service.GetReport(query, refresh: false, CancellationToken.None);
        int entries = cache.Count;

        var result = await service.GetReport(query with { Source = "random-1", Content = "x" }, refresh: false, CancellationToken.None);
        await service.GetReport(query with { Source = "random-2" }, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(repository.Queries, Is.EqualTo(new[] { query }));
            Assert.That(cache.Count, Is.EqualTo(entries));
            Assert.That(result.Source, Is.Null);
            Assert.That(result.Content, Is.Null);
        });
    }

    [Test]
    public async Task GetReport_UnknownContent_MeansAllContents_AndAddsNoCacheEntry()
    {
        await service.GetReport(query with { Source = "newsletter" }, refresh: false, CancellationToken.None);
        int entries = cache.Count;

        var result = await service.GetReport(query with { Source = "newsletter", Content = "random" }, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(repository.Queries, Has.Count.EqualTo(2));
            Assert.That(repository.Queries.Select(q => q.Content), Has.None.EqualTo("random"));
            Assert.That(cache.Count, Is.EqualTo(entries));
            Assert.That(result.Source, Is.EqualTo("newsletter"));
            Assert.That(result.Content, Is.Null);
        });
    }

    [Test]
    public async Task GetReport_NoContent_IsKept()
    {
        repository.Data = known with { Contents = [new("newsletter", "footer", 2, 1)] };

        var result = await service.GetReport(query with { Source = "newsletter", Content = string.Empty }, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(repository.Queries[^1].Content, Is.Empty);
            Assert.That(result.Content, Is.Empty);
        });
    }

    [Test]
    public async Task GetReport_Refresh_ReadsAgain()
    {
        await service.GetReport(query, refresh: false, CancellationToken.None);
        await service.GetReport(query, refresh: true, CancellationToken.None);

        Assert.That(repository.Queries, Has.Count.EqualTo(2));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task GetReport_NoCampaignLandings_ChecksSiteForUtmData(bool hasAnyUtmData)
    {
        repository.HasUtmData = hasAnyUtmData;

        var result = await service.GetReport(query, refresh: false, CancellationToken.None);
        await service.GetReport(query with { Source = "google" }, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasAnyUtmData, Is.EqualTo(hasAnyUtmData));
            // One site-wide cache item.
            Assert.That(repository.UtmDataCalls, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task GetReport_WithCampaignLandings_SkipsUtmDataCheck()
    {
        repository.Data = CampaignSourcesData.Empty with { Totals = CampaignSourcesTotalsRow.Empty with { Landings = 2, AllCampaignLandings = 1 } };

        var result = await service.GetReport(query with { Source = "other" }, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasAnyUtmData, Is.True);
            Assert.That(repository.UtmDataCalls, Is.Zero);
        });
    }

    private sealed class FakeRepository : ICampaignSourcesRepository, IStatsUtmDataRepository
    {
        public CampaignSourcesData Data { get; set; } = CampaignSourcesData.Empty;

        public bool HasUtmData { get; set; }

        public int UtmDataCalls { get; private set; }

        public List<CampaignSourcesQuery> Queries { get; } = [];

        public Task<CampaignSourcesData> GetData(CampaignSourcesQuery query, CancellationToken cancellationToken)
        {
            Queries.Add(query);
            return Task.FromResult(Data);
        }

        public Task<bool> HasAnyUtmData(CancellationToken cancellationToken)
        {
            UtmDataCalls++;
            return Task.FromResult(HasUtmData);
        }
    }
}
