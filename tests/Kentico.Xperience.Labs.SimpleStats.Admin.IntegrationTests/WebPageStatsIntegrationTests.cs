using CMS.Activities;

using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.WebPageStats;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.IntegrationTests;

public class WebPageStatsIntegrationTests : IntegrationFixture
{
    private const string PageVisit = PredefinedActivityType.PAGE_VISIT;
    private const string Landing = PredefinedActivityType.LANDING_PAGE;
    private const string FormSubmit = PredefinedActivityType.BIZFORM_SUBMIT;

    private static readonly DateOnly from = new(2026, 3, 1);
    private static readonly DateOnly to = new(2026, 3, 7);

    private static readonly Guid home = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid contact = new("00000000-0000-0000-0000-000000000002");

    // Language-specific domains: only submissions logged on site.test count.
    private static readonly WebPageStatsTarget contactOnDomain = new(contact, English, 1, "/contact", ["site.test"], UsesLanguageDomains: true);
    private static readonly WebPageStatsTarget homeOnDomain = new(home, English, 1, string.Empty, ["site.test"], UsesLanguageDomains: true);

    private readonly WebPageStatsRepository repository = new();

    // Activities 1–7 link the Contact page; forms (20+) are matched by URL in channel 1.
    private protected override SeedBuilder CreateSeed() => new SeedBuilder()
        .Language(English, "en", "English")
        .Language(French, "fr", "French")
        .WebsiteChannel(1, "Site One", English)
        .WebsiteChannel(2, "Site Two", English)
        .Activity(1, 1, At(1, 10), PageVisit, 1, contact, English, "https://site.test/contact")
        .Activity(2, 2, At(2, 10), PageVisit, 1, contact, English, "https://site.test/contact")
        .Activity(3, 2, At(2, 11), PageVisit, 1, contact, English, "https://site.test/contact")
        .Activity(4, 3, At(2, 10), PageVisit, 1, contact, French, "https://fr.site.test/contact")
        .Activity(5, 1, At(1, 9).AddMinutes(59), Landing, 1, contact, English, "https://site.test/contact?utm_source=news", "news", "a")
        .Activity(6, 4, At(3, 10), Landing, 1, contact, English, "https://site.test/contact")
        .Activity(7, 1, At(8, 0), PageVisit, 1, contact, English, "https://site.test/contact")
        .Activity(20, 1, At(2, 12), FormSubmit, 1, url: "https://site.test/contact")
        .Activity(21, 3, At(3, 12), FormSubmit, 1, url: "https://SITE.test/contact/")
        .Activity(22, 5, At(3, 13), FormSubmit, 1, url: "https://site.test/contact?utm=1#x")
        .Activity(23, 6, At(4, 12), FormSubmit, 1, url: "https://other.test/contact")
        .Activity(24, 7, At(4, 12), FormSubmit, 2, url: "https://site.test/contact")
        .Activity(25, 8, At(4, 12), FormSubmit, 1, url: "https://site.test/contactus")
        .Activity(26, 9, At(4, 12), FormSubmit, 1, url: "https://site.test/contact/more")
        .Activity(27, 10, At(5, 12), FormSubmit, 1, url: "https://site.test:8080/contact")
        // A form submission is matched by its URL only, never by a page link.
        .Activity(28, 1, At(5, 12), FormSubmit, 1, contact, English, "https://site.test/other")
        .Activity(30, 11, At(2, 12), FormSubmit, 1, url: "http://site.test?x=1")
        .Activity(31, 12, At(2, 12), FormSubmit, 1, url: "http://site.test#f")
        .Activity(32, 13, At(2, 12), FormSubmit, 1, url: "http://site.test")
        .Activity(33, 14, At(2, 12), FormSubmit, 1, url: "http://site.test/")
        .Activity(34, 15, At(2, 12), FormSubmit, 1, url: "http://other.test?x=1")
        .Activity(35, 16, At(2, 12), FormSubmit, 1, url: "http://site.test/?x=1");

    private static DateTime At(int day, int hour) => new(2026, 3, day, hour, 0, 0, DateTimeKind.Unspecified);
    [Test]
    public async Task GetData_LanguageDomains_CountsPageActivitiesAndFormsOnTheDomain()
    {
        var data = await repository.GetData(contactOnDomain, from, to, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(data.DailyCounts, Is.EquivalentTo(new[]
            {
                Day(PageVisit, 1, 1),
                Day(PageVisit, 2, 2),
                Day(Landing, 1, 1),
                Day(Landing, 3, 1),
                Day(FormSubmit, 2, 1),
                Day(FormSubmit, 3, 2),
            }));
            Assert.That(data.UniqueContacts, Is.EqualTo(5));
            Assert.That(data.UniqueVisitors, Is.EqualTo(2));
            Assert.That(data.UniqueSubmitters, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task GetData_LanguagePrefixes_CountsFormsOnAnyHost()
    {
        var target = new WebPageStatsTarget(contact, English, 1, "/contact");

        var data = await repository.GetData(target, from, to, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(data.DailyCounts.Where(d => d.SeriesKey == FormSubmit).Sum(d => d.Count), Is.EqualTo(5));
            Assert.That(data.UniqueSubmitters, Is.EqualTo(5));
        });
    }

    [Test]
    public async Task GetData_HomePage_MatchesHostEndingAtQueryStringOrFragment()
    {
        var data = await repository.GetData(homeOnDomain, from, to, CancellationToken.None);

        Assert.Multiple(() =>
        {
            // http://site.test?x=1, http://site.test#f, http://site.test, http://site.test/, http://site.test/?x=1; not http://other.test?x=1.
            Assert.That(data.DailyCounts, Is.EqualTo(new[] { Day(FormSubmit, 2, 5) }));
            Assert.That(data.UniqueSubmitters, Is.EqualTo(5));
        });
    }

    [Test]
    public async Task GetData_NoLiveUrl_CountsNoForms()
    {
        var target = new WebPageStatsTarget(home, English, 1, null);

        var data = await repository.GetData(target, from, to, CancellationToken.None);

        Assert.That(data.DailyCounts, Is.Empty);
    }

    [Test]
    public async Task GetData_Campaigns_CountsLandingsOfTheVariant()
    {
        var data = await repository.GetData(contactOnDomain, from, to, CancellationToken.None);

        Assert.That(data.Campaigns, Is.EqualTo(new WebPageCampaignData(
            Landings: 2,
            CampaignLandings: 1,
            CampaignVisitors: 1,
            SourceCount: 1,
            Sources: data.Campaigns.Sources,
            SourceContentCount: 1,
            SourceContents: data.Campaigns.SourceContents)));
        Assert.Multiple(() =>
        {
            Assert.That(data.Campaigns.Sources, Is.EqualTo(new[] { new WebPageCampaignSourceRow("news", 1, 1) }));
            Assert.That(data.Campaigns.SourceContents, Is.EqualTo(new[] { new WebPageCampaignContentRow("news", "a", 1) }));
        });
    }

    [Test]
    public async Task Build_ReturnsTotalsOfTheRange()
    {
        var query = new StatsQuery(from, to, StatsGrouping.Day, null);
        var data = await repository.GetData(contactOnDomain, from, to, CancellationToken.None);

        var result = WebPageStatsReportBuilder.Build(query, data, new Dictionary<string, string>(), contactOnDomain, hasAnyUtmData: true);

        Assert.That(result.Total, Is.EqualTo(8));
    }

    private static StatsDailyCount Day(string type, int day, int count) => new(type, new DateOnly(2026, 3, day), count);
}
