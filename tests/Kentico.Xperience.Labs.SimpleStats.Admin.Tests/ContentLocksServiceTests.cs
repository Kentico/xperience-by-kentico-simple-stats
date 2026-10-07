using CMS.Core;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.Base.UIPages;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContentLocks;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class ContentLocksServiceTests
{
    private static readonly StatsSnapshotQuery all = new(null, null);

    private FakeRepository repository = null!;
    private FakeSettings settings = null!;
    private FakeCache cache = null!;
    private FakeClock clock = null!;
    private FakeAdminLinks adminLinks = null!;
    private ContentLocksService service = null!;

    [SetUp]
    public void SetUp()
    {
        repository = new FakeRepository();
        settings = new FakeSettings { Value = "True" };
        cache = new FakeCache();
        clock = new FakeClock(new DateTimeOffset(2026, 10, 6, 10, 0, 0, TimeSpan.Zero));
        adminLinks = new FakeAdminLinks();
        service = new ContentLocksService(repository, settings, cache, cache, adminLinks, clock);
    }

    [Test]
    public async Task GetReport_PassesQueryAndServerTimeToRepository()
    {
        var query = new StatsSnapshotQuery("Headless", 3);

        var result = await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(repository.Queries.Single(), Is.EqualTo(query));
            Assert.That(repository.Nows.Single(), Is.EqualTo(clock.GetLocalNow().DateTime));
            Assert.That(result.Kind, Is.EqualTo("Headless"));
            Assert.That(result.ChannelId, Is.EqualTo(3));
        });
    }

    [TestCase("True", true)]
    [TestCase("true", true)]
    [TestCase("False", false)]
    [TestCase("", false)]
    [TestCase("yes", false)]
    public async Task GetReport_ReadsTheLockingSetting(string value, bool expected)
    {
        settings.Value = value;

        var result = await service.GetReport(all, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.LockingEnabled, Is.EqualTo(expected));
            Assert.That(settings.LastKey, Is.EqualTo(ContentLocksService.LockingSettingsKey));
        });
    }

    [Test]
    public async Task GetReport_LockingDisabledWithLocks_ShowsTheLocks()
    {
        settings.Value = "False";
        var lockedWhen = clock.GetLocalNow().DateTime.AddHours(-1);
        repository.Data = new ContentLocksData(
            0,
            [new(53, "Admin", 1, lockedWhen)],
            [new(1, "Home", "Page", "English", 53, "Admin", lockedWhen, lockedWhen)]);

        var result = await service.GetReport(all, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.LockingEnabled, Is.False);
            Assert.That(result.LockedCount, Is.EqualTo(1));
            Assert.That(result.Items, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task GetReport_SettingChange_ShowsWithoutRefresh()
    {
        await service.GetReport(all, refresh: false, CancellationToken.None);

        settings.Value = "False";
        var second = await service.GetReport(all, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(repository.Queries, Has.Count.EqualTo(1));
            Assert.That(second.LockingEnabled, Is.False);
        });
    }

    [Test]
    public async Task GetReport_LinksItemsWhereTheyAreEdited_AndUsersInTheUsersApplication()
    {
        var lockedWhen = clock.GetLocalNow().DateTime.AddHours(-1);
        repository.Data = new ContentLocksData(
            0,
            [new(53, "Admin", 2, lockedWhen), new(null, null, 1, lockedWhen)],
            [
                new(1, "Banner", "Banner", "Spanish", 53, "Admin", lockedWhen, lockedWhen) { Link = new(ContentItemLocation.ContentHub, 3, 5, "es") },
                new(2, "Home", "Page", "English", 53, "Admin", lockedWhen, lockedWhen) { Link = new(ContentItemLocation.WebPage, 1, 42, "en") },
            ]);

        var result = await service.GetReport(all, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Items.Select(i => i.AdminPath), Is.EqualTo(new[] { "/ContentItemEdit/3/es/all/5", "/webpages-1/en_42/content" }));
            Assert.That(result.ByUser.Items.Select(i => i.AdminPath), Is.EqualTo(new[] { "/UserEdit/53", null }));
            Assert.That(adminLinks.Calls.Where(c => c.Page == typeof(UserEdit)).Select(c => (c.ParameterPage, c.Value)), Is.EqualTo(new[] { (typeof(UserEditSection), (object)53) }));
        });
    }

    [Test]
    public async Task GetReport_UsesCachedData_WhenNotRefreshing()
    {
        var first = await service.GetReport(all, refresh: false, CancellationToken.None);

        repository.Data = new ContentLocksData(0, [new(53, "Admin", 1, clock.GetLocalNow().DateTime)], []);
        clock.Now = clock.Now.AddMinutes(1);
        var second = await service.GetReport(all, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(repository.Queries, Has.Count.EqualTo(1));
            Assert.That(second.LockedCount, Is.Zero);
            Assert.That(second.UpdatedAt, Is.EqualTo(first.UpdatedAt));
        });
    }

    [Test]
    public async Task GetReport_Refresh_ReadsDatabaseAndSetsUpdatedAtEverywhere()
    {
        await service.GetReport(all, refresh: false, CancellationToken.None);

        repository.Data = new ContentLocksData(0, [new(53, "Admin", 1, clock.GetLocalNow().DateTime)], []);
        clock.Now = clock.Now.AddMinutes(1);
        var refreshed = await service.GetReport(all, refresh: true, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(repository.Queries, Has.Count.EqualTo(2));
            Assert.That(refreshed.LockedCount, Is.EqualTo(1));
            Assert.That(refreshed.UpdatedAt, Is.EqualTo(clock.Now));
            Assert.That(refreshed.ByUser.UpdatedAt, Is.EqualTo(clock.Now));
        });
    }

    [Test]
    public async Task GetReport_CachesPerKindAndChannel_WithExpiryOnly()
    {
        await service.GetReport(all, refresh: false, CancellationToken.None);
        await service.GetReport(all, refresh: false, CancellationToken.None);
        await service.GetReport(new("Website", null), refresh: false, CancellationToken.None);
        await service.GetReport(new("Website", 2), refresh: false, CancellationToken.None);
        await service.GetReport(new("Website", 2), refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(repository.Queries, Has.Count.EqualTo(3));
            Assert.That(cache.Settings.Select(s => s.GetCacheDependency), Is.All.Null);
            Assert.That(cache.Settings.Select(s => s.CacheMinutes), Is.All.EqualTo(StatsCache.CacheMinutes));
            Assert.That(cache.Settings[0].CacheItemName, Does.Contain("content-locks"));
        });
    }

    private sealed class FakeRepository : IContentLocksRepository
    {
        public ContentLocksData Data { get; set; } = ContentLocksData.Empty;

        public List<StatsSnapshotQuery> Queries { get; } = [];

        public List<DateTime> Nows { get; } = [];

        public Task<ContentLocksData> GetData(StatsSnapshotQuery query, DateTime now, CancellationToken cancellationToken)
        {
            Queries.Add(query);
            Nows.Add(now);
            return Task.FromResult(Data with { Now = now });
        }
    }

    private sealed class FakeSettings : ISettingsService
    {
        public string Value { get; set; } = string.Empty;

        public string? LastKey { get; private set; }

        public string this[string keyName]
        {
            get
            {
                LastKey = keyName;
                return Value;
            }
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
