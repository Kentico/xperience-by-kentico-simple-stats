using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ReusableUsage;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class ReusableUsageServiceTests
{
    private FakeRepository repository = null!;
    private FakeCache cache = null!;
    private FakeClock clock = null!;
    private FakeAdminLinks adminLinks = null!;
    private ReusableUsageService service = null!;

    [SetUp]
    public void SetUp()
    {
        repository = new FakeRepository();
        cache = new FakeCache();
        clock = new FakeClock(new DateTimeOffset(2026, 10, 7, 10, 0, 0, TimeSpan.Zero));
        adminLinks = new FakeAdminLinks();
        service = new ReusableUsageService(repository, cache, cache, adminLinks, clock);
    }

    [Test]
    public async Task GetReport_All_ReadsOnce_AndReturnsOptions()
    {
        var result = await service.GetReport(null, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(repository.Queries, Is.EqualTo(new int?[] { null }));
            Assert.That(result.ContentTypeId, Is.Null);
            Assert.That(result.ContentTypeOptions.Select(o => o.Id), Is.EqualTo(new[] { 10, 12 }));
        });
    }

    [Test]
    public async Task GetReport_KnownContentType_ReadsTheType_WithOptionsOfAllTypes()
    {
        var result = await service.GetReport(12, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(repository.Queries, Is.EqualTo(new int?[] { null, 12 }));
            Assert.That(result.ContentTypeId, Is.EqualTo(12));
            Assert.That(result.ReusableItems, Is.EqualTo(3));
            Assert.That(result.ContentTypeOptions.Select(o => o.Id), Is.EqualTo(new[] { 10, 12 }));
        });
    }

    [TestCase(99)]
    [TestCase(13)]
    [TestCase(0)]
    [TestCase(-5)]
    public async Task GetReport_UnknownContentTypeOrTypeWithoutItems_FallsBackToAll(int contentTypeId)
    {
        var result = await service.GetReport(contentTypeId, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(repository.Queries, Is.EqualTo(new int?[] { null }));
            Assert.That(result.ContentTypeId, Is.Null);
            Assert.That(cache.Settings.Select(s => s.CacheItemName).Distinct().Count(), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task GetReport_CachesPerContentType_WithExpiryOnly()
    {
        await service.GetReport(null, refresh: false, CancellationToken.None);
        await service.GetReport(10, refresh: false, CancellationToken.None);
        await service.GetReport(10, refresh: false, CancellationToken.None);
        await service.GetReport(12, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(repository.Queries, Is.EqualTo(new int?[] { null, 10, 12 }));
            Assert.That(cache.Settings.Select(s => s.GetCacheDependency), Is.All.Null);
            Assert.That(cache.Settings.Select(s => s.CacheMinutes), Is.All.EqualTo(StatsCache.CacheMinutes));
            Assert.That(cache.Settings[0].CacheItemName, Does.Contain("reusable-usage"));
        });
    }

    [Test]
    public async Task GetReport_Refresh_ReadsAgainAndSetsUpdatedAtEverywhere()
    {
        var first = await service.GetReport(12, refresh: false, CancellationToken.None);

        clock.Now = clock.Now.AddMinutes(1);
        var refreshed = await service.GetReport(12, refresh: true, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(repository.Queries, Is.EqualTo(new int?[] { null, 12, null, 12 }));
            Assert.That(first.UpdatedAt, Is.Not.EqualTo(refreshed.UpdatedAt));
            Assert.That(refreshed.UpdatedAt, Is.EqualTo(clock.Now));
            Assert.That(refreshed.Distribution.UpdatedAt, Is.EqualTo(clock.Now));
            Assert.That(refreshed.ByContentType.UpdatedAt, Is.EqualTo(clock.Now));
        });
    }

    [Test]
    public async Task GetReport_LinksItemsToTheContentHub_AndTypesToContentTypes()
    {
        var result = await service.GetReport(null, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.MostUsed.Single().AdminPath, Is.EqualTo("/ContentItemEdit/3/en/all/77"));
            Assert.That(result.ByContentType.Items.Select(i => i.AdminPath), Is.EqualTo(new[] { "/ContentTypeGeneral/10", "/ContentTypeGeneral/12", "/ContentTypeGeneral/13" }));
        });
    }

    private sealed class FakeRepository : IReusableUsageRepository
    {
        public List<int?> Queries { get; } = [];

        public Task<ReusableUsageData> GetData(int? contentTypeId, CancellationToken cancellationToken)
        {
            Queries.Add(contentTypeId);

            var all = new ReusableUsageData(
                new ReusableUsageTotals(8, 6, 2, 4, 2, 0, 0, 10),
                [new(77, "Logo", "Image", 3, 1, 1, 1, 0, null) { Link = new(ContentItemLocation.ContentHub, 3, 77, "en") }],
                [
                    new(10, "Acme.Image", "Image", 5, 4, 5),
                    new(12, "Acme.SocialLink", "Social link", 3, 2, 5),
                    new(13, "Acme.Empty", "Empty", 0, 0, 0),
                ]);

            return Task.FromResult(contentTypeId switch
            {
                null => all,
                12 => new ReusableUsageData(new ReusableUsageTotals(3, 2, 1, 1, 1, 0, 0, 5), [], [all.ContentTypes[1]]),
                _ => ReusableUsageData.Empty,
            });
        }
    }

    private sealed class FakeAdminLinks : IStatsAdminLinks
    {
        public string? GetPath<TPage>(PageParameterValues? parameters = null) =>
            $"/{typeof(TPage).Name}/{string.Join("/", parameters!.Select(v => v.Value))}";
    }
}
