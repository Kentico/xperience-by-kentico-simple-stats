using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.TagUsage;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class TagUsageServiceTests
{
    private static readonly Guid taxonomyGuid = Guid.NewGuid();
    private static readonly Guid fieldGuid = Guid.NewGuid();

    private FakeRepository repository = null!;
    private FakeFieldProvider fieldProvider = null!;
    private FakeCache cache = null!;
    private FakeClock clock = null!;
    private TagUsageService service = null!;

    [SetUp]
    public void SetUp()
    {
        repository = new FakeRepository();
        fieldProvider = new FakeFieldProvider();
        cache = new FakeCache();
        clock = new FakeClock(new DateTimeOffset(2026, 10, 8, 10, 0, 0, TimeSpan.Zero));
        service = new TagUsageService(repository, fieldProvider, cache, cache, new FakeAdminLinks(), clock);
    }

    [Test]
    public async Task GetReport_All_ReadsOnce_AndReturnsOptions()
    {
        var result = await service.GetReport(null, null, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(repository.Queries, Is.EqualTo(new[] { ((string?)null, (int?)null) }));
            Assert.That(result.TaxonomyId, Is.Null);
            Assert.That(result.TaxonomyOptions.Select(o => o.Id), Is.EqualTo(new[] { 4, 7 }));
            Assert.That(result.Fields.Single().Total, Is.EqualTo(10));
        });
    }

    [Test]
    public async Task GetReport_KnownTaxonomy_ReadsIt_WithOptionsOfAll()
    {
        var result = await service.GetReport("Reusable", 7, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(repository.Queries, Is.EqualTo(new[] { ("Reusable", null), ("Reusable", (int?)7) }));
            Assert.That(result.TaxonomyId, Is.EqualTo(7));
            Assert.That(result.Kind, Is.EqualTo("Reusable"));
            Assert.That(result.TaxonomyOptions, Has.Count.EqualTo(2));
        });
    }

    [TestCase(99)]
    [TestCase(0)]
    [TestCase(-3)]
    public async Task GetReport_UnknownTaxonomy_FallsBackToAll(int taxonomyId)
    {
        var result = await service.GetReport(null, taxonomyId, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(repository.Queries, Is.EqualTo(new[] { ((string?)null, (int?)null) }));
            Assert.That(result.TaxonomyId, Is.Null);
        });
    }

    [Test]
    public async Task GetReport_CachesPerKindAndTaxonomy_FieldsOnce_WithExpiryOnly()
    {
        await service.GetReport(null, null, refresh: false, CancellationToken.None);
        await service.GetReport(null, 7, refresh: false, CancellationToken.None);
        await service.GetReport(null, 7, refresh: false, CancellationToken.None);
        await service.GetReport("Website", null, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(repository.Queries, Is.EqualTo(new[] { ((string?)null, (int?)null), (null, 7), ("Website", null) }));
            Assert.That(fieldProvider.Calls, Is.EqualTo(1));
            Assert.That(cache.Settings.Select(s => s.GetCacheDependency), Is.All.Null);
            Assert.That(cache.Settings.Select(s => s.CacheMinutes), Is.All.EqualTo(StatsCache.CacheMinutes));
            Assert.That(cache.Settings[0].CacheItemName, Does.Contain("tag-usage"));
        });
    }

    [Test]
    public async Task GetReport_Refresh_ReadsDataAndFieldsAgain()
    {
        var first = await service.GetReport(null, null, refresh: false, CancellationToken.None);

        clock.Now = clock.Now.AddMinutes(1);
        var refreshed = await service.GetReport(null, null, refresh: true, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(repository.Queries, Has.Count.EqualTo(2));
            Assert.That(fieldProvider.Calls, Is.EqualTo(2));
            Assert.That(first.UpdatedAt, Is.Not.EqualTo(refreshed.UpdatedAt));
            Assert.That(refreshed.UpdatedAt, Is.EqualTo(clock.Now));
            Assert.That(refreshed.TopTags.UpdatedAt, Is.EqualTo(clock.Now));
        });
    }

    [Test]
    public async Task GetReport_LinksTagsToTagEdit()
    {
        var result = await service.GetReport(null, null, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.TopTags.Items.Single().AdminPath, Is.EqualTo("/TagEdit/4/22"));
            Assert.That(result.UnusedTags.Single().AdminPath, Is.EqualTo("/TagEdit/7/27"));
        });
    }

    private sealed class FakeRepository : ITagUsageRepository
    {
        public List<(string? Kind, int? TaxonomyId)> Queries { get; } = [];

        public Task<TagUsageData> GetData(string? kind, int? taxonomyId, CancellationToken cancellationToken)
        {
            Queries.Add((kind, taxonomyId));

            return Task.FromResult(new TagUsageData(
                [
                    new(7, Guid.NewGuid(), "ProductTags", "Product tags", 3, 1, 1),
                    new(4, taxonomyGuid, "ImageTags", "Image tags", 7, 0, 7),
                ],
                [new(22, 4, "Product shot", "Image tags", null, 32)],
                [new(27, 7, "Sweet", "Product tags", null, 0)],
                [new(fieldGuid, 10, 6)],
                [new(10, "Acme.Image", "Image", 10)]));
        }
    }

    private sealed class FakeFieldProvider : ITagUsageFieldProvider
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<TagUsageFieldDefinition>> GetFields(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<TagUsageFieldDefinition>>([new(fieldGuid, "ImageTags", "Tags", [taxonomyGuid], null, [10])]);
        }
    }

    private sealed class FakeAdminLinks : IStatsAdminLinks
    {
        public string? GetPath<TPage>(PageParameterValues? parameters = null) =>
            $"/{typeof(TPage).Name}/{string.Join("/", parameters!.Select(v => v.Value))}";
    }
}
