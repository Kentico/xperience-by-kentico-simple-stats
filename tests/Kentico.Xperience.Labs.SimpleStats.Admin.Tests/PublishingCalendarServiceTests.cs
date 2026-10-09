using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.Base.UIPages;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PublishingCalendar;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class PublishingCalendarServiceTests
{
    private static readonly PublishingCalendarQuery all = new(new StatsSnapshotQuery(null, null), 30);

    private FakeRepository repository = null!;
    private FakeCache cache = null!;
    private FakeClock clock = null!;
    private FakeAdminLinks adminLinks = null!;
    private PublishingCalendarService service = null!;

    [SetUp]
    public void SetUp()
    {
        repository = new FakeRepository();
        cache = new FakeCache();
        clock = new FakeClock(new DateTimeOffset(2026, 10, 6, 10, 0, 0, TimeSpan.Zero));
        adminLinks = new FakeAdminLinks();
        service = new PublishingCalendarService(repository, cache, cache, adminLinks, clock);
    }

    [Test]
    public async Task GetReport_PassesQueryAndServerTimeToRepository()
    {
        var query = new PublishingCalendarQuery(new StatsSnapshotQuery("Website", 2), 7);

        var result = await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.That(repository.Queries.Single(), Is.EqualTo(query));
        Assert.That(repository.Nows.Single(), Is.EqualTo(clock.GetLocalNow().DateTime));
        Assert.That(result.Window, Is.EqualTo(7));
        Assert.That(result.Kind, Is.EqualTo("Website"));
        Assert.That(result.ChannelId, Is.EqualTo(2));
    }

    [Test]
    public async Task GetReport_LinksItemsWhereTheyAreEdited()
    {
        var when = clock.GetLocalNow().DateTime.AddDays(1);
        repository.Data = PublishingCalendarData.Empty with
        {
            Upcoming =
            [
                new(1, when, PublishingAction.Publish, "Banner", "Banner", "Spanish", null, null) { Link = new(ContentItemLocation.ContentHub, 3, 5, "es") },
                new(2, when, PublishingAction.Publish, "Home", "Page", "English", "Site", null) { Link = new(ContentItemLocation.WebPage, 1, 42, "en") },
                new(3, when, PublishingAction.Send, "Mail", "Newsletter", "English", "Mails", null) { Link = new(ContentItemLocation.Email, 2, 7, "en") },
            ],
            Recent = [new(4, when.AddDays(-2), null, "No link", "Page", "English", "Site", null)],
        };

        var result = await service.GetReport(all, refresh: false, CancellationToken.None);

        Assert.That(
            result.Upcoming.Select(i => i.AdminPath),
            Is.EqualTo(new[] { "/ContentItemEdit/3/es/all/5", "/webpages-1/en_42/content", "/emails-2/en/list/7" }));
        Assert.That(result.Recent.Single().AdminPath, Is.Null);
        Assert.That(
            adminLinks.Calls.Select(c => c.ParameterPage),
            Is.EqualTo(new[] { typeof(ContentHubWorkspace), typeof(ContentHubContentLanguage), typeof(ContentHubFolder), typeof(ContentItemEditSection) }));
    }

    [Test]
    public async Task GetReport_UsesCachedData_WhenNotRefreshing()
    {
        var first = await service.GetReport(all, refresh: false, CancellationToken.None);

        repository.Data = PublishingCalendarData.Empty with { Counts = new(1, 0, 0, 0) };
        clock.Now = clock.Now.AddMinutes(1);
        var second = await service.GetReport(all, refresh: false, CancellationToken.None);

        Assert.That(repository.Queries, Has.Count.EqualTo(1));
        Assert.That(second.UpcomingPublish, Is.Zero);
        Assert.That(second.UpdatedAt, Is.EqualTo(first.UpdatedAt));
    }

    [Test]
    public async Task GetReport_Refresh_ReadsDatabaseAndSetsUpdatedAtEverywhere()
    {
        await service.GetReport(all, refresh: false, CancellationToken.None);

        repository.Data = PublishingCalendarData.Empty with { Counts = new(1, 0, 0, 0) };
        clock.Now = clock.Now.AddMinutes(1);
        var refreshed = await service.GetReport(all, refresh: true, CancellationToken.None);

        Assert.That(repository.Queries, Has.Count.EqualTo(2));
        Assert.That(refreshed.UpcomingPublish, Is.EqualTo(1));
        Assert.That(refreshed.UpdatedAt, Is.EqualTo(clock.Now));
        Assert.That(refreshed.Days.UpdatedAt, Is.EqualTo(clock.Now));
    }

    [Test]
    public async Task GetReport_CachesPerKindChannelAndWindow()
    {
        await service.GetReport(all, refresh: false, CancellationToken.None);
        await service.GetReport(all, refresh: false, CancellationToken.None);
        await service.GetReport(all with { Window = 7 }, refresh: false, CancellationToken.None);
        await service.GetReport(new(new("Website", null), 30), refresh: false, CancellationToken.None);
        await service.GetReport(new(new("Website", 2), 30), refresh: false, CancellationToken.None);
        await service.GetReport(new(new("Website", 2), 30), refresh: false, CancellationToken.None);

        Assert.That(repository.Queries, Has.Count.EqualTo(4));
    }

    [Test]
    public async Task GetReport_CachesWithExpiryOnly_UnderOwnKey()
    {
        await service.GetReport(all, refresh: false, CancellationToken.None);

        var settings = cache.Settings.Single();
        Assert.That(settings.GetCacheDependency, Is.Null);
        Assert.That(settings.CacheMinutes, Is.EqualTo(StatsCache.CacheMinutes));
        Assert.That(settings.CacheItemName, Does.Contain("publishing-calendar"));
    }

    private sealed class FakeRepository : IPublishingCalendarRepository
    {
        public PublishingCalendarData Data { get; set; } = PublishingCalendarData.Empty;

        public List<PublishingCalendarQuery> Queries { get; } = [];

        public List<DateTime> Nows { get; } = [];

        public Task<PublishingCalendarData> GetData(PublishingCalendarQuery query, DateTime now, CancellationToken cancellationToken)
        {
            Queries.Add(query);
            Nows.Add(now);
            return Task.FromResult(Data with { Now = now });
        }
    }

    private sealed class FakeAdminLinks : IStatsAdminLinks
    {
        public List<(Type Page, Type ParameterPage, object Value)> Calls { get; } = [];

        public string? GetPath<TPage>(PageParameterValues? parameters = null)
        {
            var values = parameters!.ToList();
            foreach (var parameter in values)
            {
                Calls.Add((typeof(TPage), parameter.Key, parameter.Value));
            }
            return $"/{typeof(TPage).Name}/{string.Join("/", values.Select(v => v.Value))}";
        }
    }
}
