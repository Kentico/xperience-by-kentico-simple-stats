using CMS.ContentEngine;
using CMS.DataEngine;

using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PublishingCalendar;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.IntegrationTests;

public class PublishingCalendarIntegrationTests : IntegrationFixture
{
    private const int Article = SeedBuilder.FirstSharedId + 1;
    private const int Snippet = SeedBuilder.FirstSharedId + 2;
    private const int Folder = SeedBuilder.FirstSharedId + 3;

    private const PublishingAction Publish = PublishingAction.Publish;
    private const PublishingAction Unpublish = PublishingAction.Unpublish;

    // Window of 7 days: upcoming from Mar 10 12:00 to Mar 17 12:00 (both included); recently published from Mar 3 12:00.
    private static readonly DateTime now = At(10, 12);

    private readonly PublishingCalendarRepository repository = new();

    // Variants (metadata ID = item ID + 10), all in English:
    // 11 Article: publish Mar 11 09:00, unpublish Mar 15 09:00; last published Mar 5.
    // 12 Article: publish exactly now, unpublish exactly at the window end; last published exactly at the recent limit.
    // 13 Article: publish after the window, unpublish just before now; its latest version is a draft, so its older published version's
    //    last publish (Mar 9) does not count, and the latest one (Mar 3 11:59) is before the recent limit.
    // 14 Snippet (reusable): publish Mar 12 08:00; last published Mar 9.
    // 15 page folder: publish Mar 11, not counted.
    // 16 Article in channel 2: unpublish Mar 13; last published Mar 8.
    // 17 Article: publish Mar 11 09:00, same time as 11.
    // 18 Article: publish and unpublish both Mar 14 10:00.
    private protected override SeedBuilder CreateSeed()
    {
        int published = (int)VersionStatus.Published;
        var created = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

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
            .Item(3, Article, channelId: 1)
            .Item(4, Snippet, workspaceId: MainWorkspace)
            .Item(5, Folder, channelId: 1)
            .Item(6, Article, channelId: 2)
            .Item(7, Article, channelId: 1)
            .Item(8, Article, channelId: 1)
            .Variant(11, 1, English, "One", created, scheduledPublish: At(11, 9), scheduledUnpublish: At(15, 9))
            .Variant(12, 2, English, "Two", created, scheduledPublish: now, scheduledUnpublish: At(17, 12))
            .Variant(13, 3, English, "Three", created, scheduledPublish: At(18, 9), scheduledUnpublish: now.AddMinutes(-1))
            .Variant(14, 4, English, "Four", created, scheduledPublish: At(12, 8))
            .Variant(15, 5, English, "Five", created, scheduledPublish: At(11, 9))
            .Variant(16, 6, English, "Six", created, scheduledUnpublish: At(13, 9))
            .Variant(17, 7, English, "Seven", created, scheduledPublish: At(11, 9))
            .Variant(18, 8, English, "Eight", created, scheduledPublish: At(14, 10), scheduledUnpublish: At(14, 10))
            .CommonData(21, 1, English, published, isLatest: true, created, At(5, 9))
            .CommonData(22, 2, English, published, isLatest: true, created, At(3, 12))
            .CommonData(23, 3, English, published, isLatest: false, created, At(9, 9))
            .CommonData(33, 3, English, (int)VersionStatus.Draft, isLatest: true, created, At(3, 12).AddMinutes(-1))
            .CommonData(24, 4, English, published, isLatest: true, created, At(9, 9))
            .CommonData(25, 5, English, published, isLatest: true, created, At(9, 9))
            .CommonData(26, 6, English, published, isLatest: true, created, At(8, 9));
    }

    [Test]
    public async Task GetData_AllKinds_ReturnsEventsInTheWindowAndRecentlyPublished()
    {
        var data = await GetData(kind: null, channelId: null);

        Assert.Multiple(() =>
        {
            Assert.That(data.Counts, Is.EqualTo(new PublishingCountsRow(UpcomingPublish: 5, UpcomingUnpublish: 4, UpcomingSend: 0, RecentlyPublished: 4)));
            Assert.That(data.Days, Is.EquivalentTo(new[]
            {
                Day(10, 1, 0),
                Day(11, 2, 0),
                Day(12, 1, 0),
                Day(13, 0, 1),
                Day(14, 1, 1),
                Day(15, 0, 1),
                Day(17, 0, 1),
            }));
            Assert.That(data.Upcoming.Select(r => (r.Id, r.Action)), Is.EqualTo(new (int, PublishingAction?)[]
            {
                (12, Publish),
                (11, Publish),
                (17, Publish),
                (14, Publish),
                (16, Unpublish),
                (18, Publish),
                (18, Unpublish),
                (11, Unpublish),
                (12, Unpublish),
            }));
            Assert.That(data.Recent.Select(r => (r.Id, r.When)), Is.EqualTo(new[]
            {
                (14, At(9, 9)),
                (16, At(8, 9)),
                (11, At(5, 9)),
                (12, At(3, 12)),
            }));
            Assert.That(data.Upcoming[0], Is.EqualTo(new PublishingRow(12, now, Publish, "Two", "Article", "English", "Site One", null)
            {
                Link = data.Upcoming[0].Link,
            }));
            Assert.That(data.Upcoming[3].IsReusable, Is.True);
            Assert.That(data.Upcoming[3].Workspace, Is.EqualTo("Main"));
        });
    }

    [Test]
    public async Task GetData_KindAndChannel_KeepsOnlyMatchingItems()
    {
        var data = await GetData(ClassContentTypeType.WEBSITE, channelId: 1);

        Assert.Multiple(() =>
        {
            Assert.That(data.Counts, Is.EqualTo(new PublishingCountsRow(4, 3, 0, 2)));
            Assert.That(data.Upcoming.Select(r => r.Id).Distinct(), Is.EqualTo(new[] { 12, 11, 17, 18 }));
            Assert.That(data.Recent.Select(r => r.Id), Is.EqualTo(new[] { 11, 12 }));
        });
    }

    [Test]
    public async Task GetData_Reusable_KeepsOnlyReusableItems()
    {
        var data = await GetData(ClassContentTypeType.REUSABLE, channelId: null);

        Assert.Multiple(() =>
        {
            Assert.That(data.Counts, Is.EqualTo(new PublishingCountsRow(1, 0, 0, 1)));
            Assert.That(data.Upcoming.Select(r => r.Id), Is.EqualTo(new[] { 14 }));
        });
    }

    [Test]
    public async Task Build_FillsEveryDayOfTheWindow()
    {
        var query = new PublishingCalendarQuery(new StatsSnapshotQuery(null, null), 7);
        var data = await repository.GetData(query, now, CancellationToken.None);

        var result = PublishingCalendarReportBuilder.Build(query, data);

        Assert.Multiple(() =>
        {
            Assert.That((result.UpcomingPublish, result.UpcomingUnpublish, result.RecentlyPublished), Is.EqualTo((5, 4, 4)));
            Assert.That(result.Days.Periods, Has.Count.EqualTo(8));
            Assert.That(result.Days.Total, Is.EqualTo(9));
            Assert.That(result.Upcoming.Select(i => i.Label).First(), Is.EqualTo("Two"));
        });
    }

    private Task<PublishingCalendarData> GetData(string? kind, int? channelId) =>
        repository.GetData(new PublishingCalendarQuery(new StatsSnapshotQuery(kind, channelId), 7), now, CancellationToken.None);

    private static PublishingDayRow Day(int day, int publish, int unpublish) => new(new DateOnly(2026, 3, day), publish, unpublish, 0);

    private static DateTime At(int day, int hour) => new(2026, 3, day, hour, 0, 0, DateTimeKind.Unspecified);
}
