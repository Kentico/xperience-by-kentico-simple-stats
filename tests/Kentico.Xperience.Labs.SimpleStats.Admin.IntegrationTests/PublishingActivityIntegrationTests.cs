using CMS.ContentEngine;
using CMS.ContentEngine.Internal;
using CMS.DataEngine;

using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PublishingActivity;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.IntegrationTests;

public class PublishingActivityIntegrationTests : IntegrationFixture
{
    // Range Mar 1–7, previous period Feb 22–28.
    private static readonly StatsQuery range = new(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 7), StatsGrouping.Day, null);

    private const string Created = PublishingActivitySeriesKeys.Created;
    private const string FirstPublished = PublishingActivitySeriesKeys.FirstPublished;
    private const string Updates = PublishingActivitySeriesKeys.Updates;

    private static readonly int publish = (int)ContentItemVersionAction.Publish;
    private static readonly int otherAction = (int)Enum.GetValues<ContentItemVersionAction>().First(a => a != ContentItemVersionAction.Publish);

    private const int Article = SeedBuilder.FirstSharedId + 1;
    private const int Snippet = SeedBuilder.FirstSharedId + 2;
    private const int Folder = SeedBuilder.FirstSharedId + 3;

    private readonly PublishingActivityRepository repository = new();

    // Variants (metadata ID = item ID + 10):
    // 11 Article, created Mar 1 08:00, first published Mar 2 08:00 (its publish row 1 s later), updated Mar 4 and Mar 5.
    // 12 Article, created Feb 10, first published Feb 23 (previous period), updated Feb 26 (previous) and Mar 3.
    // 13 Snippet (reusable, no channel), created Mar 2, first published Mar 6 (publish row 30 s later, within the tolerance).
    // 14 Article, published but no first publish date (migrated); its only publish row Mar 3 counts as an update.
    // 16 Article, created Mar 3 08:00, first published Mar 3 09:00; two publish rows at the same time: the second (higher ID) is an update.
    // 17 page folder (no content type type): not counted. 18 Article in channel 2, created Mar 5, never published.
    private protected override SeedBuilder CreateSeed()
    {
        int published = (int)VersionStatus.Published;

        return new SeedBuilder()
            .Language(English, "en", "English")
            .Workspace(MainWorkspace, "Main")
            .WebsiteChannel(1, "Site One", English)
            .WebsiteChannel(2, "Site Two", English)
            .ContentType(Article, "Article", ClassContentTypeType.WEBSITE)
            .ContentType(Snippet, "Snippet", ClassContentTypeType.REUSABLE)
            .ContentType(Folder, "Folder", null)
            .Item(1, Article, channelId: 1)
            .Item(2, Article, channelId: 1)
            .Item(3, Snippet, workspaceId: MainWorkspace)
            .Item(4, Article, channelId: 1)
            .Item(6, Article, channelId: 1)
            .Item(7, Folder, channelId: 1)
            .Item(8, Article, channelId: 2)
            .Variant(11, 1, English, "One", At(3, 1, 8))
            .Variant(12, 2, English, "Two", At(2, 10, 8))
            .Variant(13, 3, English, "Three", At(3, 2, 0))
            .Variant(14, 4, English, "Four", At(1, 1, 0))
            .Variant(16, 6, English, "Six", At(3, 3, 8))
            .Variant(17, 7, English, "Seven", At(3, 2, 0))
            .Variant(18, 8, English, "Eight", At(3, 5, 0))
            .CommonData(21, 1, English, published, isLatest: true, At(3, 2, 8), At(3, 5, 12))
            .CommonData(22, 2, English, published, isLatest: true, At(2, 23, 10), At(3, 3, 0))
            .CommonData(23, 3, English, published, isLatest: true, At(3, 6, 0), At(3, 6, 0))
            .CommonData(24, 4, English, published, isLatest: true)
            .CommonData(26, 6, English, published, isLatest: true, At(3, 3, 9), At(3, 3, 9))
            .CommonData(27, 7, English, published, isLatest: true, At(3, 2, 0), At(3, 2, 0))
            .CommonData(28, 8, English, (int)VersionStatus.Draft, isLatest: true)
            .Version(101, 1, English, publish, At(3, 2, 8).AddSeconds(1))
            .Version(102, 1, English, publish, At(3, 4, 12))
            .Version(103, 1, English, publish, At(3, 5, 12))
            .Version(104, 1, English, otherAction, At(3, 4, 11))
            .Version(201, 2, English, publish, At(2, 23, 10))
            .Version(202, 2, English, publish, At(2, 26, 10))
            .Version(203, 2, English, publish, At(3, 3, 0))
            .Version(301, 3, English, publish, At(3, 6, 0).AddSeconds(30))
            .Version(401, 4, English, publish, At(3, 3, 0))
            .Version(601, 6, English, publish, At(3, 3, 9))
            .Version(602, 6, English, publish, At(3, 3, 9))
            .Version(701, 7, English, publish, At(3, 2, 0));
    }

    private static DateTime At(int month, int day, int hour) => new(2026, month, day, hour, 0, 0, DateTimeKind.Unspecified);

    [Test]
    public async Task GetData_NoFilter_SplitsFirstPublishesFromUpdates()
    {
        var data = await repository.GetData(new PublishingActivityQuery(range, null), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(data.Daily, Is.EquivalentTo(new[]
            {
                Day(Created, 3, 1, 1),
                Day(Created, 3, 2, 1),
                Day(Created, 3, 3, 1),
                Day(Created, 3, 5, 1),
                Day(FirstPublished, 2, 23, 1),
                Day(FirstPublished, 3, 2, 1),
                Day(FirstPublished, 3, 3, 1),
                Day(FirstPublished, 3, 6, 1),
                Day(Updates, 2, 26, 1),
                Day(Updates, 3, 3, 3),
                Day(Updates, 3, 4, 1),
                Day(Updates, 3, 5, 1),
            }));
            Assert.That(data.ContentTypes.Select(t => (t.DisplayName, t.Created, t.FirstPublished, t.Updates)), Is.EquivalentTo(new[]
            {
                ("Article", 3, 2, 5),
                ("Snippet", 1, 1, 0),
            }));
            Assert.That(data.ContentTypes.Single(t => t.ClassId == Article).MedianDays, Is.EqualTo((1 + (1 / 24.0)) / 2).Within(0.0001));
            Assert.That(data.ContentTypes.Single(t => t.ClassId == Snippet).MedianDays, Is.EqualTo(4.0).Within(0.0001));
            Assert.That(data.Totals.PublishedDateUnknown, Is.EqualTo(1));
            Assert.That(data.Totals.MedianDays, Is.EqualTo(1.0).Within(0.0001));
            Assert.That(data.Totals.Percentile90Days, Is.EqualTo(3.4).Within(0.0001));
            Assert.That(data.Slowest.Select(s => (s.VariantId, s.DisplayName, s.ContentType)), Is.EqualTo(new[]
            {
                (13, "Three", "Snippet"),
                (11, "One", "Article"),
                (16, "Six", "Article"),
            }));
            Assert.That(data.Slowest[0].Workspace, Is.EqualTo("Main"));
            Assert.That(data.Slowest[0].IsReusable, Is.True);
            Assert.That(data.Slowest[1].Channel, Is.EqualTo("Site One"));
        });
    }

    [Test]
    public async Task GetData_Channel_KeepsOnlyItemsOfTheChannel()
    {
        var data = await repository.GetData(new PublishingActivityQuery(range with { ChannelId = 1 }, null), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(Total(data, Created, current: true), Is.EqualTo(2));
            Assert.That(Total(data, FirstPublished, current: true), Is.EqualTo(2));
            Assert.That(Total(data, Updates, current: true), Is.EqualTo(5));
            Assert.That(data.Slowest.Select(s => s.VariantId), Is.EqualTo(new[] { 11, 16 }));
        });
    }

    [Test]
    public async Task GetData_Kind_KeepsOnlyItemsOfTheKind()
    {
        var data = await repository.GetData(new PublishingActivityQuery(range, ClassContentTypeType.REUSABLE), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(data.Daily, Is.EquivalentTo(new[] { Day(Created, 3, 2, 1), Day(FirstPublished, 3, 6, 1) }));
            Assert.That(data.Totals.PublishedDateUnknown, Is.Zero);
            Assert.That(data.Slowest.Select(s => s.VariantId), Is.EqualTo(new[] { 13 }));
        });
    }

    [Test]
    public async Task Build_ComparesRangeWithPreviousPeriod()
    {
        var query = new PublishingActivityQuery(range, null);
        var data = await repository.GetData(query, CancellationToken.None);

        var result = PublishingActivityReportBuilder.Build(query, data, versionHistoryEnabled: true, versionHistoryLength: 10);

        Assert.Multiple(() =>
        {
            Assert.That((result.Created.Current, result.Created.Previous), Is.EqualTo((4, 0)));
            Assert.That((result.FirstPublished.Current, result.FirstPublished.Previous), Is.EqualTo((3, 1)));
            Assert.That((result.Updates?.Current, result.Updates?.Previous), Is.EqualTo(((int?)5, (int?)1)));
            Assert.That(result.MedianDaysToPublish, Is.EqualTo(1m));
            Assert.That(result.PublishedDateUnknown, Is.EqualTo(1));
            Assert.That(result.Slowest, Has.Count.EqualTo(3));
        });
    }

    private static StatsDailyCount Day(string series, int month, int day, int count) => new(series, new DateOnly(2026, month, day), count);

    private static int Total(PublishingActivityData data, string series, bool current) =>
        data.Daily.Where(d => d.SeriesKey == series && (d.Date >= range.From) == current).Sum(d => d.Count);
}
