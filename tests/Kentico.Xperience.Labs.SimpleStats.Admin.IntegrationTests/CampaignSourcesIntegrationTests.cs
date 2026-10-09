using CMS.Activities;
using CMS.DataEngine;

using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.CampaignSources;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.IntegrationTests;

public class CampaignSourcesIntegrationTests : IntegrationFixture
{
    // Range Mar 1–7, previous period Feb 22–28.
    private static readonly StatsQuery range = new(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 7), StatsGrouping.Day, null);

    private static readonly Guid home = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid about = new("00000000-0000-0000-0000-000000000002");
    private static readonly Guid shop = new("00000000-0000-0000-0000-000000000003");

    private const string L = PredefinedActivityType.LANDING_PAGE;
    private const string P = PredefinedActivityType.PAGE_VISIT;

    private readonly CampaignSourcesRepository repository = new();

    // Campaign landings in the range: 1, 2, 3, 4, 5, 8.
    private protected override SeedBuilder CreateSeed() => new SeedBuilder()
        .Language(English, "en", "English")
        .WebsiteChannel(1, "Site One", English)
        .WebsiteChannel(2, "Site Two", English)
        .ContentType(SeedBuilder.FirstSharedId + 1, "Page", ClassContentTypeType.WEBSITE)
        .Item(101, SeedBuilder.FirstSharedId + 1, channelId: 1)
        .Item(102, SeedBuilder.FirstSharedId + 1, channelId: 1)
        .Item(103, SeedBuilder.FirstSharedId + 1, channelId: 2)
        .WebPage(1, home, 1, 101)
        .WebPage(2, about, 1, 102)
        .WebPage(3, shop, 2, 103)
        .Variant(1101, 101, English, "Home", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified))
        .Variant(1102, 102, English, "About", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified))
        .Variant(1103, 103, English, "Shop", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified))
        .Activity(1, 1, At(3, 1, 10), L, 1, home, English, "https://one.test/?utm_source=news", "news", "a")
        .Activity(2, 2, At(3, 2, 10), L, 1, home, English, "https://one.test/?utm_source=news", "news", "b")
        .Activity(3, 2, At(3, 2, 11), L, 1, about, English, "https://one.test/about?utm_source=news", "news", null)
        .Activity(4, 1, At(3, 3, 10), L, 1, home, English, "https://one.test/?utm_source=news", "news", "a")
        .Activity(5, 3, At(3, 3, 12), L, 1, about, English, "https://one.test/about", " social ", "a")
        .Activity(6, 4, At(3, 4, 10), L, 1, home, English, "https://one.test/", null, null)
        .Activity(7, 5, At(3, 5, 10), L, 2, shop, English, "https://two.test/", string.Empty, null)
        .Activity(8, 6, new DateTime(2026, 3, 7, 23, 59, 59, DateTimeKind.Unspecified), L, 2, shop, English, "https://two.test/#top", "social", null)
        // After the range, previous period, before the previous period, other type.
        .Activity(9, 7, At(3, 8, 0), L, 1, home, English, "https://one.test/", "news", "a")
        .Activity(10, 7, At(2, 25, 10), L, 1, home, English, "https://one.test/", "news", "a")
        .Activity(11, 8, new DateTime(2026, 2, 21, 23, 59, 59, DateTimeKind.Unspecified), L, 1, home, English, "https://one.test/", "news", "a")
        .Activity(12, 1, At(3, 1, 10), P, 1, home, English, "https://one.test/", "news", "a");

    private static DateTime At(int month, int day, int hour) => new(2026, month, day, hour, 0, 0, DateTimeKind.Unspecified);

    [Test]
    public async Task GetData_NoFilter_CountsLandingsOfTheRangeAndPreviousPeriod()
    {
        var data = await repository.GetData(new CampaignSourcesQuery(range, null, null), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(data.Totals, Is.EqualTo(new CampaignSourcesTotalsRow(
                Landings: 8, PreviousLandings: 1, AllCampaignLandings: 6, CampaignLandings: 6, PreviousCampaignLandings: 1,
                CampaignVisitors: 4, Sources: 2, PreviousSources: 1)));
            Assert.That(data.Sources, Is.EqualTo(new[]
            {
                new CampaignSourcesSourceRow("news", 4, 2, 1),
                new CampaignSourcesSourceRow("social", 2, 2, 0),
            }));
            Assert.That(data.Daily, Is.EquivalentTo(new[]
            {
                new CampaignSourcesDailyRow("news", new DateOnly(2026, 3, 1), 1),
                new CampaignSourcesDailyRow("news", new DateOnly(2026, 3, 2), 2),
                new CampaignSourcesDailyRow("news", new DateOnly(2026, 3, 3), 1),
                new CampaignSourcesDailyRow("social", new DateOnly(2026, 3, 3), 1),
                new CampaignSourcesDailyRow("social", new DateOnly(2026, 3, 7), 1),
            }));
            Assert.That(data.Contents, Is.EqualTo(new[]
            {
                new CampaignSourcesContentRow("news", "a", 2, 1),
                new CampaignSourcesContentRow("news", null, 1, 1),
                new CampaignSourcesContentRow("news", "b", 1, 1),
                new CampaignSourcesContentRow("social", null, 1, 1),
                new CampaignSourcesContentRow("social", "a", 1, 1),
            }));
            Assert.That(data.ContentCount, Is.EqualTo(5));
            Assert.That(data.Pages, Is.EqualTo(new[]
            {
                new CampaignSourcesPageRow(home, English, 3, 2, "https://one.test/", "Home", "English", "Site One"),
                new CampaignSourcesPageRow(about, English, 2, 2, "https://one.test/about", "About", "English", "Site One"),
                new CampaignSourcesPageRow(shop, English, 1, 1, "https://two.test/", "Shop", "English", "Site Two"),
            }));
            Assert.That(data.PageCount, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task GetData_Source_FiltersKpisSeriesPagesAndContentsButNotTheSourceList()
    {
        var data = await repository.GetData(new CampaignSourcesQuery(range, "news", null), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(data.Totals, Is.EqualTo(new CampaignSourcesTotalsRow(8, 1, 6, 4, 1, 2, 2, 1)));
            Assert.That(data.Daily.Select(d => d.Source).Distinct(), Is.EqualTo(new[] { "news" }));
            Assert.That(data.Daily.Sum(d => d.Landings), Is.EqualTo(4));
            Assert.That(data.Sources.Select(s => s.Source), Is.EqualTo(new[] { "news", "social" }));
            Assert.That(data.Pages.Select(p => (p.PageGuid, p.Landings)), Is.EqualTo(new[] { ((Guid?)home, 3), (about, 1) }));
            Assert.That(data.Contents.Select(c => (c.Source, c.Content, c.Landings)), Is.EqualTo(new[]
            {
                ("news", "a", 2),
                ("news", null, 1),
                ("news", "b", 1),
            }));
        });
    }

    [Test]
    public async Task GetData_SourceAndContent_FiltersKpisSeriesAndPagesButNotContents()
    {
        var data = await repository.GetData(new CampaignSourcesQuery(range, "news", "a"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(data.Totals, Is.EqualTo(new CampaignSourcesTotalsRow(8, 1, 6, 2, 1, 1, 2, 1)));
            Assert.That(data.Daily, Is.EquivalentTo(new[]
            {
                new CampaignSourcesDailyRow("news", new DateOnly(2026, 3, 1), 1),
                new CampaignSourcesDailyRow("news", new DateOnly(2026, 3, 3), 1),
            }));
            Assert.That(data.Pages.Select(p => (p.PageGuid, p.Landings)), Is.EqualTo(new[] { ((Guid?)home, 2) }));
            Assert.That(data.ContentCount, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task GetData_SourceWithoutContent_SelectsLandingsWithNoContent()
    {
        var data = await repository.GetData(new CampaignSourcesQuery(range, "news", string.Empty), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(data.Totals.CampaignLandings, Is.EqualTo(1));
            Assert.That(data.Totals.PreviousCampaignLandings, Is.Zero);
            Assert.That(data.Pages.Select(p => (p.PageGuid, p.Landings)), Is.EqualTo(new[] { ((Guid?)about, 1) }));
            Assert.That(data.ContentCount, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task GetData_TrimmedSource_MatchesStoredValueWithSpaces()
    {
        var data = await repository.GetData(new CampaignSourcesQuery(range, "social", "a"), CancellationToken.None);

        Assert.That(data.Totals.CampaignLandings, Is.EqualTo(1));
    }

    [Test]
    public async Task GetData_Channel_KeepsOnlyLandingsOfTheChannel()
    {
        var data = await repository.GetData(new CampaignSourcesQuery(range with { ChannelId = 2 }, null, null), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(data.Totals, Is.EqualTo(new CampaignSourcesTotalsRow(2, 0, 1, 1, 0, 1, 1, 0)));
            Assert.That(data.Sources, Is.EqualTo(new[] { new CampaignSourcesSourceRow("social", 1, 1, 0) }));
            Assert.That(data.Pages.Select(p => p.PageGuid), Is.EqualTo(new Guid?[] { shop }));
        });
    }

    [Test]
    public async Task Build_Source_ReturnsOptionsShareAndPages()
    {
        var query = new CampaignSourcesQuery(range, "news", null);
        var data = await repository.GetData(query, CancellationToken.None);

        var result = CampaignSourcesReportBuilder.Build(query, data, hasAnyUtmData: true);

        Assert.Multiple(() =>
        {
            Assert.That(result.Landings.Current, Is.EqualTo(8));
            Assert.That(result.CampaignLandings.Current, Is.EqualTo(4));
            Assert.That(result.CampaignLandings.Previous, Is.EqualTo(1));
            Assert.That(result.CampaignShare, Is.EqualTo(0.5));
            Assert.That(result.Series.Total, Is.EqualTo(4));
            Assert.That(result.SourceOptions, Is.EqualTo(new[] { "news", "social" }));
            Assert.That(result.ContentOptions, Is.EquivalentTo(new[] { "a", "b", string.Empty }));
            Assert.That(result.Pages.Items.Select(i => (i.Label, i.Value)), Is.EqualTo(new[] { ("Home", 3m), ("About", 1m) }));
        });
    }
}
