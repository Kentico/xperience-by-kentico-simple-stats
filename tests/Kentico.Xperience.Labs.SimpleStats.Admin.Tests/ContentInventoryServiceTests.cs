using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.Base.UIPages;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContentInventory;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class ContentInventoryServiceTests
{
    private static readonly StatsSnapshotQuery all = new(null, null);

    private static readonly ContentInventoryData data = new(
        [new(10, "Site.Article", "Article", "Website", 3)],
        [new(1, "en", "English", true, 3)],
        [new(2, null, null, null, null, 2, 0, 0), new(0, 7, "Review", 4, "Articles", 1, 0, 0)])
    {
        WorkflowItems = [new(5, "Coffee", "Article", "English", new(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified), "Review", 4, "Articles")],
    };

    private FakeRepository repository = null!;
    private FakeCache cache = null!;
    private FakeClock clock = null!;
    private FakeAdminLinks adminLinks = null!;
    private ContentInventoryService service = null!;

    [SetUp]
    public void SetUp()
    {
        repository = new FakeRepository();
        cache = new FakeCache();
        clock = new FakeClock(new DateTimeOffset(2026, 9, 30, 10, 0, 0, TimeSpan.Zero));
        adminLinks = new FakeAdminLinks();
        service = new ContentInventoryService(repository, cache, cache, adminLinks, clock);
    }

    [Test]
    public async Task GetReport_PassesQueryToRepository()
    {
        var query = new StatsSnapshotQuery("Website", 2);

        await service.GetReport(query, refresh: false, CancellationToken.None);

        Assert.That(repository.Queries.Single(), Is.EqualTo(query));
        Assert.That(repository.Nows.Single(), Is.EqualTo(clock.GetLocalNow().DateTime));
    }

    [Test]
    public async Task GetReport_LinksContentTypesAndWorkflows()
    {
        repository.Data = data;

        var result = await service.GetReport(all, refresh: false, CancellationToken.None);

        Assert.That(result.ByContentType.Items.Single().AdminPath, Is.EqualTo("/ContentTypeGeneral/10"));
        Assert.That(result.Workflow.Items.Single().AdminPath, Is.EqualTo("/WorkflowSteps/4"));
        Assert.That(adminLinks.Calls, Is.EquivalentTo(new (Type, Type, object)[]
        {
            (typeof(ContentTypeGeneral), typeof(ContentTypeEditSection), 10),
            (typeof(WorkflowSteps), typeof(WorkflowEditSection), 4),
        }));
    }

    [Test]
    public async Task GetReport_LinksReusableItemsToContentHub_ElseWorkflow()
    {
        var link = new ContentItemLink(ContentItemLocation.ContentHub, 3, 5, "es");
        var modified = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified);
        repository.Data = data with
        {
            Oldest = [new(8, "Coffee", "Coffee", "Spanish", modified) { Link = link }],
            WorkflowItems = [new(8, "Coffee", "Coffee", "Spanish", modified, "Review", 4, "Articles") { Link = link }],
            UnusedItems = [new(5, "Coffee", "Coffee", modified) { Link = link }, new(6, "No variant", "Coffee", null)],
        };

        var result = await service.GetReport(all, refresh: false, CancellationToken.None);

        const string expected = "/ContentItemEdit/3/es/all/5";
        Assert.That(result.Age.Oldest.Single().AdminPath, Is.EqualTo(expected));
        Assert.That(result.Workflow.Items.Single().AdminPath, Is.EqualTo(expected));
        Assert.That(result.UnusedReusable!.Items.Select(i => i.AdminPath), Is.EqualTo(new[] { expected, null }));
        Assert.That(
            adminLinks.Calls.Where(c => c.Page == typeof(ContentItemEdit)).Take(4).Select(c => c.ParameterPage),
            Is.EqualTo(new[] { typeof(ContentHubWorkspace), typeof(ContentHubContentLanguage), typeof(ContentHubFolder), typeof(ContentItemEditSection) }));
    }

    [Test]
    public async Task GetReport_LinksPagesEmailsAndHeadlessItemsInTheirChannels()
    {
        var modified = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified);
        repository.Data = data with
        {
            Oldest =
            [
                new(1, "Page", "Article", "English", modified) { Link = new(ContentItemLocation.WebPage, 1, 42, "en") },
                new(2, "Email", "Newsletter", "English", modified) { Link = new(ContentItemLocation.Email, 2, 7, "en") },
                new(3, "App item", "Screen", "English", modified) { Link = new(ContentItemLocation.Headless, 3, 9, "en") },
                new(4, "Folder", "Page", "English", modified),
            ],
            // A page in a workflow step links to the page, not the workflow.
            WorkflowItems = [new(1, "Page", "Article", "English", modified, "Review", 4, "Articles") { Link = new(ContentItemLocation.WebPage, 1, 42, "en") }],
            PendingDrafts = [new(1, "Page", "Article", "English", modified) { Link = new(ContentItemLocation.WebPage, 1, 42, "en") }],
            PendingDraftCount = 1,
        };

        var result = await service.GetReport(all, refresh: false, CancellationToken.None);

        Assert.That(result.Age.Oldest.Select(i => i.AdminPath), Is.EqualTo(new[]
        {
            "/webpages-1/en_42/content",
            "/emails-2/en/list/7",
            "/headless-3/en/list/9",
            null,
        }));
        Assert.That(result.Workflow.Items.Single().AdminPath, Is.EqualTo("/webpages-1/en_42/content"));
        Assert.That(result.ForgottenEdits.Items.Single().AdminPath, Is.EqualTo("/webpages-1/en_42/content"));
    }

    [Test]
    public async Task GetReport_WorksWithoutLinks()
    {
        repository.Data = data;
        adminLinks.ReturnNull = true;

        var result = await service.GetReport(all, refresh: false, CancellationToken.None);

        Assert.That(result.ByContentType.Items.Single().AdminPath, Is.Null);
        Assert.That(result.Workflow.Items.Single().AdminPath, Is.Null);
    }

    [Test]
    public async Task GetReport_UsesCachedData_WhenNotRefreshing()
    {
        var first = await service.GetReport(all, refresh: false, CancellationToken.None);

        repository.Data = data;
        clock.Now = clock.Now.AddMinutes(1);
        var second = await service.GetReport(all, refresh: false, CancellationToken.None);

        Assert.That(repository.Queries, Has.Count.EqualTo(1));
        Assert.That(second.TotalItems, Is.Zero);
        Assert.That(second.UpdatedAt, Is.EqualTo(first.UpdatedAt));
    }

    [Test]
    public async Task GetReport_Refresh_ReadsDatabaseAndSetsUpdatedAtEverywhere()
    {
        await service.GetReport(all, refresh: false, CancellationToken.None);

        repository.Data = data;
        clock.Now = clock.Now.AddMinutes(1);
        var refreshed = await service.GetReport(all, refresh: true, CancellationToken.None);

        Assert.That(repository.Queries, Has.Count.EqualTo(2));
        Assert.That(refreshed.TotalItems, Is.EqualTo(3));
        Assert.That(refreshed.UpdatedAt, Is.EqualTo(clock.Now));
        Assert.That(refreshed.ByKind.UpdatedAt, Is.EqualTo(clock.Now));
        Assert.That(refreshed.ByContentType.UpdatedAt, Is.EqualTo(clock.Now));
        Assert.That(refreshed.ByStatus.UpdatedAt, Is.EqualTo(clock.Now));
        Assert.That(refreshed.Age.Buckets.UpdatedAt, Is.EqualTo(clock.Now));
        Assert.That(refreshed.UnusedReusable!.ByContentType.UpdatedAt, Is.EqualTo(clock.Now));
    }

    [Test]
    public async Task GetReport_CachesPerKindAndChannel()
    {
        await service.GetReport(all, refresh: false, CancellationToken.None);
        await service.GetReport(all, refresh: false, CancellationToken.None);
        await service.GetReport(new("Website", null), refresh: false, CancellationToken.None);
        await service.GetReport(new("Website", 2), refresh: false, CancellationToken.None);
        await service.GetReport(new("Website", 2), refresh: false, CancellationToken.None);

        Assert.That(repository.Queries, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task GetReport_CachesWithExpiryOnly_UnderOwnKey()
    {
        await service.GetReport(all, refresh: false, CancellationToken.None);

        var settings = cache.Settings.Single();
        Assert.That(settings.GetCacheDependency, Is.Null);
        Assert.That(settings.CacheMinutes, Is.EqualTo(StatsCache.CacheMinutes));
        Assert.That(settings.CacheItemName, Does.Contain("content-inventory"));
    }

    private sealed class FakeRepository : IContentInventoryRepository
    {
        public ContentInventoryData Data { get; set; } = ContentInventoryData.Empty;

        public List<StatsSnapshotQuery> Queries { get; } = [];

        public List<DateTime> Nows { get; } = [];

        public Task<ContentInventoryData> GetData(StatsSnapshotQuery query, DateTime now, CancellationToken cancellationToken)
        {
            Queries.Add(query);
            Nows.Add(now);
            return Task.FromResult(Data);
        }
    }

    private sealed class FakeAdminLinks : IStatsAdminLinks
    {
        public bool ReturnNull { get; set; }

        public List<(Type Page, Type ParameterPage, object Value)> Calls { get; } = [];

        public string? GetPath<TPage>(PageParameterValues? parameters = null)
        {
            var values = parameters!.ToList();
            foreach (var parameter in values)
            {
                Calls.Add((typeof(TPage), parameter.Key, parameter.Value));
            }
            return ReturnNull ? null : $"/{typeof(TPage).Name}/{string.Join("/", values.Select(v => v.Value))}";
        }
    }
}
