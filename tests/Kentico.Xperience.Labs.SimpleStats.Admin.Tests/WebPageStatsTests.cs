using System.Text.RegularExpressions;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.Websites.UIPages;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ActivityCounts;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.WebPageStats;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;
using Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class WebPageStatsTests
{
    private static readonly StatsQuery query = new(new(2026, 9, 1), new(2026, 9, 7), StatsGrouping.Day, null);
    private static readonly WebPageStatsTarget target = new(Guid.Parse("8d1f6f43-4a0e-4f0b-9e57-55a1f3a3c001"), 1, 2, "/articles");

    private FakeRepository repository = null!;
    private FakeCache cache = null!;
    private FakeClock clock = null!;
    private WebPageStatsService service = null!;

    [SetUp]
    public void SetUp()
    {
        repository = new FakeRepository();
        cache = new FakeCache();
        clock = new FakeClock(new DateTimeOffset(2026, 9, 7, 10, 0, 0, TimeSpan.Zero));
        service = new WebPageStatsService(repository, repository, cache, cache, clock);
    }

    [Test]
    public void Build_CountsPageVisitsAndKeepsContactCounts()
    {
        var data = new WebPageStatsData(
            [
                new("pagevisit", new(2026, 9, 1), 3),
                new("pagevisit", new(2026, 9, 2), 2),
                new("landingpage", new(2026, 9, 2), 4),
                new("bizformsubmit", new(2026, 9, 3), 2),
            ],
            UniqueContacts: 5,
            UniqueVisitors: 3,
            UniqueSubmitters: 1);

        var result = WebPageStatsReportBuilder.Build(query, data, new Dictionary<string, string> { ["pagevisit"] = "Page visit" }, target);

        Assert.Multiple(() =>
        {
            Assert.That(result.Total, Is.EqualTo(11));
            Assert.That(result.PageVisits, Is.EqualTo(5));
            Assert.That(result.FormSubmissions, Is.EqualTo(2));
            Assert.That(result.UniqueContacts, Is.EqualTo(5));
            Assert.That(result.UniqueVisitors, Is.EqualTo(3));
            Assert.That(result.UniqueSubmitters, Is.EqualTo(1));
            Assert.That(result.FormUrlPath, Is.EqualTo("/articles"));
            Assert.That(result.Periods, Has.Count.EqualTo(7));
            Assert.That(result.Series.Select(s => s.DisplayName), Is.EqualTo(new[] { "Page visit", "landingpage", "bizformsubmit" }));
        });
    }

    [Test]
    public void Build_NoData_ReturnsZeroFilledEmptyReport()
    {
        var result = WebPageStatsReportBuilder.Build(query, WebPageStatsData.Empty, new Dictionary<string, string>(), target with { FormUrlPath = null });

        Assert.Multiple(() =>
        {
            Assert.That(result.Total, Is.Zero);
            Assert.That(result.PageVisits, Is.Zero);
            Assert.That(result.FormSubmissions, Is.Zero);
            Assert.That(result.FormUrlPath, Is.Null);
            Assert.That(result.Series, Is.Empty);
            Assert.That(result.Periods, Has.Count.EqualTo(7));
        });
    }

    [Test]
    public async Task GetReport_PassesTargetAndRangeToRepository()
    {
        await service.GetReport(target, query, refresh: false, CancellationToken.None);

        Assert.That(repository.LastCall, Is.EqualTo((target, query.From, query.To)));
    }

    [Test]
    public async Task GetReport_KeysCacheByPageLanguageUrlAndRange_NotGroupingOrFilterChannel()
    {
        await service.GetReport(target, query, refresh: false, CancellationToken.None);
        await service.GetReport(target, query with { Grouping = StatsGrouping.Week, ChannelId = 3 }, refresh: false, CancellationToken.None);
        Assert.That(repository.Calls, Is.EqualTo(1));

        await service.GetReport(target with { LanguageId = 2 }, query, refresh: false, CancellationToken.None);
        await service.GetReport(target with { WebPageItemGuid = Guid.NewGuid() }, query, refresh: false, CancellationToken.None);
        await service.GetReport(target with { FormUrlPath = "/news" }, query, refresh: false, CancellationToken.None);
        await service.GetReport(target with { FormUrlPath = null }, query, refresh: false, CancellationToken.None);
        await service.GetReport(target with { FormUrlHosts = ["fr.example.com"], UsesLanguageDomains = true }, query, refresh: false, CancellationToken.None);
        await service.GetReport(target, query with { From = new(2026, 8, 1) }, refresh: false, CancellationToken.None);
        Assert.That(repository.Calls, Is.EqualTo(7));
    }

    [Test]
    public async Task GetReport_Refresh_ReadsDatabaseAgain()
    {
        await service.GetReport(target, query, refresh: false, CancellationToken.None);

        repository.Data = new([new("pagevisit", new(2026, 9, 3), 2)], 1, 1, 0);
        clock.Now = clock.Now.AddMinutes(1);
        var refreshed = await service.GetReport(target, query, refresh: true, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(repository.Calls, Is.EqualTo(2));
            Assert.That(refreshed.PageVisits, Is.EqualTo(2));
            Assert.That(refreshed.UpdatedAt, Is.EqualTo(clock.Now));
        });
    }

    [Test]
    public void Query_ReturnsOnlyAggregatesOfContacts()
    {
        // The CTE reads contact IDs; the final SELECT (what leaves the database) may use them only inside COUNT(DISTINCT ...).
        string finalSelect = WebPageStatsRepository.Query[WebPageStatsRepository.Query.LastIndexOf("SELECT", StringComparison.Ordinal)..];
        int references = Regex.Matches(finalSelect, @"\[ActivityContactID\]").Count;
        int aggregated = Regex.Matches(finalSelect, @"COUNT\(DISTINCT (CASE WHEN [^)]*THEN )?A\.\[ActivityContactID\]").Count;

        Assert.Multiple(() =>
        {
            Assert.That(references, Is.EqualTo(3));
            Assert.That(aggregated, Is.EqualTo(references));
        });
    }

    [TestCase("~/", "")]
    [TestCase("~/es", "/es")]
    [TestCase("~/Articles/Coffee", "/Articles/Coffee")]
    [TestCase("/articles/coffee/", "/articles/coffee")]
    [TestCase("http://localhost/", "")]
    [TestCase("http://localhost", "")]
    [TestCase("https://example.com:8080/coffee-samples?utm_source=x#form", "/coffee-samples")]
    [TestCase("https://example.com/coffee-samples/#form", "/coffee-samples")]
    [TestCase("articles", "/articles")]
    public void UrlPath_Normalize(string url, string expected) =>
        Assert.That(WebPageStatsUrlPath.Normalize(url), Is.EqualTo(expected));

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    public void UrlPath_Normalize_EmptyIsNull(string? url) =>
        Assert.That(WebPageStatsUrlPath.Normalize(url), Is.Null);

    [TestCase("fr.DancingGoat.com", "fr.dancinggoat.com")]
    [TestCase("mysite.com:8080", "mysite.com:8080")]
    [TestCase("mysite.com/path", "mysite.com")]
    [TestCase("https://mysite.com/", "mysite.com")]
    [TestCase(" localhost ", "localhost")]
    public void UrlPath_NormalizeHost(string domain, string expected) =>
        Assert.That(WebPageStatsUrlPath.NormalizeHost(domain), Is.EqualTo(expected));

    [TestCase(null)]
    [TestCase("")]
    [TestCase("https://")]
    public void UrlPath_NormalizeHost_EmptyIsNull(string? domain) =>
        Assert.That(WebPageStatsUrlPath.NormalizeHost(domain), Is.Null);

    [Test]
    public void FormatHosts_WrapsAndDelimits_SkipsInvalid() => Assert.Multiple(() =>
                                                                    {
                                                                        Assert.That(WebPageStatsRepository.FormatHosts([]), Is.Empty);
                                                                        Assert.That(WebPageStatsRepository.FormatHosts(["a.com", "b.com:8080"]), Is.EqualTo("|a.com|b.com:8080|"));
                                                                        Assert.That(WebPageStatsRepository.FormatHosts(["", "x|y"]), Is.Empty);
                                                                    });

    [Test]
    public void Build_PassesHostMatchingToResult()
    {
        var languageDomainTarget = target with { FormUrlHosts = ["fr.example.com"], UsesLanguageDomains = true };

        var result = WebPageStatsReportBuilder.Build(query, WebPageStatsData.Empty, new Dictionary<string, string>(), languageDomainTarget);

        Assert.Multiple(() =>
        {
            Assert.That(result.FormUrlHosts, Is.EqualTo(new[] { "fr.example.com" }));
            Assert.That(result.UsesLanguageDomains, Is.True);
        });
    }

    [Test]
    public void Application_DeclaresWebPageStatsPermission()
    {
        var permissions = typeof(StatsApplicationPage)
            .GetCustomAttributes(typeof(UIPermissionAttribute), false)
            .Cast<UIPermissionAttribute>();

        Assert.That(
            permissions.Select(p => (p.Name, p.DisplayName)),
            Does.Contain(("SimpleStats.WebPageStats", "Web page stats")));
    }

    [Test]
    public void Page_IsRegisteredUnderWebPageLayout_AfterRootPropertiesTab()
    {
        var registration = typeof(WebPageStatsPage).Assembly
            .GetCustomAttributes(typeof(UIPageAttribute), false)
            .Cast<UIPageAttribute>()
            .Single(a => a.Type == typeof(WebPageStatsPage));

        Assert.Multiple(() =>
        {
            Assert.That(registration.ParentType, Is.EqualTo(typeof(WebPageLayout)));
            Assert.That(registration.Order, Is.GreaterThan(10100));
        });
    }

    private sealed class FakeRepository : IWebPageStatsRepository, IActivityCountsRepository
    {
        public WebPageStatsData Data { get; set; } = WebPageStatsData.Empty;

        public int Calls { get; private set; }

        public (WebPageStatsTarget Target, DateOnly From, DateOnly To)? LastCall { get; private set; }

        public Task<WebPageStatsData> GetData(WebPageStatsTarget target, DateOnly from, DateOnly to, CancellationToken cancellationToken)
        {
            Calls++;
            LastCall = (target, from, to);
            return Task.FromResult(Data);
        }

        public Task<IReadOnlyList<ActivityDailyCount>> GetDailyCounts(DateOnly from, DateOnly to, int? channelId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<string, string>> GetActivityTypeDisplayNames(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
    }
}
