using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.TranslationStatus;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class TranslationStatusServiceTests
{
    private static readonly StatsSnapshotQuery all = new(null, null);

    private static readonly DateTime modified = new(2026, 10, 6, 12, 0, 0, DateTimeKind.Unspecified);

    private FakeRepository repository = null!;
    private FakeCache cache = null!;
    private FakeClock clock = null!;
    private TranslationStatusService service = null!;

    [SetUp]
    public void SetUp()
    {
        repository = new FakeRepository();
        cache = new FakeCache();
        clock = new FakeClock(new DateTimeOffset(2026, 10, 7, 10, 0, 0, TimeSpan.Zero));
        service = new TranslationStatusService(repository, cache, cache, new FakeAdminLinks(), clock);
    }

    [Test]
    public async Task GetReport_PassesKindAndChannelToRepository()
    {
        var query = new StatsSnapshotQuery("Website", 3);

        var result = await service.GetReport(query, null, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(repository.Queries.Single(), Is.EqualTo(query));
            Assert.That(result.Kind, Is.EqualTo("Website"));
            Assert.That(result.ChannelId, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task GetReport_LanguageFilter_UsesTheSameCachedData()
    {
        var allLanguages = await service.GetReport(all, null, refresh: false, CancellationToken.None);
        var spanish = await service.GetReport(all, 2, refresh: false, CancellationToken.None);
        var unknown = await service.GetReport(all, 99, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(repository.Queries, Has.Count.EqualTo(1));
            Assert.That(allLanguages.Languages, Has.Count.EqualTo(2));
            Assert.That(spanish.LanguageId, Is.EqualTo(2));
            Assert.That(spanish.Languages.Select(l => l.Key), Is.EqualTo(new[] { "es" }));
            Assert.That(unknown.LanguageId, Is.Null);
        });
    }

    [Test]
    public async Task GetReport_CachesPerKindAndChannel_WithExpiryOnly()
    {
        await service.GetReport(all, null, refresh: false, CancellationToken.None);
        await service.GetReport(new("Website", null), null, refresh: false, CancellationToken.None);
        await service.GetReport(new("Website", 2), null, refresh: false, CancellationToken.None);
        await service.GetReport(new("Website", 2), 3, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(repository.Queries, Has.Count.EqualTo(3));
            Assert.That(cache.Settings.Select(s => s.GetCacheDependency), Is.All.Null);
            Assert.That(cache.Settings.Select(s => s.CacheMinutes), Is.All.EqualTo(StatsCache.CacheMinutes));
            Assert.That(cache.Settings[0].CacheItemName, Does.Contain("translation-status"));
        });
    }

    [Test]
    public async Task GetReport_Refresh_ReadsDatabaseAndSetsUpdatedAtEverywhere()
    {
        var first = await service.GetReport(all, null, refresh: false, CancellationToken.None);

        clock.Now = clock.Now.AddMinutes(1);
        var cached = await service.GetReport(all, null, refresh: false, CancellationToken.None);
        var refreshed = await service.GetReport(all, null, refresh: true, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(repository.Queries, Has.Count.EqualTo(2));
            Assert.That(cached.UpdatedAt, Is.EqualTo(first.UpdatedAt));
            Assert.That(refreshed.UpdatedAt, Is.EqualTo(clock.Now));
            Assert.That(refreshed.ByContentType.UpdatedAt, Is.EqualTo(clock.Now));
        });
    }

    [Test]
    public async Task GetReport_LinksItemsWhereTheyAreEdited_AndContentTypes()
    {
        repository.Data = repository.Data with
        {
            Counts = [new(2, 10, "Article", "Article", 2, 2, 1)],
            Outdated =
            [
                new(1, 2, "Banner", "Banner", "Spanish", 53, "Admin", modified, modified.AddDays(-3)) { Link = new(ContentItemLocation.ContentHub, 3, 5, "es") },
                new(2, 2, "Home", "Page", "Spanish", 53, "Admin", modified, modified.AddDays(-2)) { Link = new(ContentItemLocation.WebPage, 1, 42, "es") },
            ],
        };

        var result = await service.GetReport(all, null, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outdated.Select(i => i.AdminPath), Is.EqualTo(new[] { "/ContentItemEdit/3/es/all/5", "/webpages-1/es_42/content" }));
            Assert.That(result.ByContentType.Items.Single().AdminPath, Is.EqualTo("/ContentTypeGeneral/10"));
        });
    }

    private sealed class FakeRepository : ITranslationStatusRepository
    {
        public TranslationStatusData Data { get; set; } = new(
            [new(1, "en", "English", true), new(2, "es", "Spanish", false), new(3, "cs", "Czech", false)],
            [],
            []);

        public List<StatsSnapshotQuery> Queries { get; } = [];

        public Task<TranslationStatusData> GetData(StatsSnapshotQuery query, CancellationToken cancellationToken)
        {
            Queries.Add(query);
            return Task.FromResult(Data);
        }
    }

    private sealed class FakeAdminLinks : IStatsAdminLinks
    {
        public string? GetPath<TPage>(PageParameterValues? parameters = null) =>
            $"/{typeof(TPage).Name}/{string.Join("/", parameters!.Select(v => v.Value))}";
    }
}
