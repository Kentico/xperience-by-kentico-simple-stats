using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.CampaignSources;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class CampaignSourcesServiceTests
{
    private static readonly CampaignSourcesQuery query = new(new(new(2026, 9, 1), new(2026, 9, 30), StatsGrouping.Day, null), null, null);

    private FakeRepository repository = null!;
    private FakeCache cache = null!;
    private FakeClock clock = null!;
    private CampaignSourcesService service = null!;

    [SetUp]
    public void SetUp()
    {
        repository = new FakeRepository();
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
            Assert.That(repository.Queries.Single(), Is.EqualTo(filtered));
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
