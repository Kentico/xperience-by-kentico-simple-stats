using CMS.Activities;
using CMS.ContentEngine;
using CMS.DataEngine;

using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContactStats;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.TagUsage;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.IntegrationTests;

public class ContactStatsIntegrationTests : IntegrationFixture
{
    private const int ContactId = 10;

    private const string PageVisit = PredefinedActivityType.PAGE_VISIT;
    private const string Landing = PredefinedActivityType.LANDING_PAGE;
    private const string FormSubmit = PredefinedActivityType.BIZFORM_SUBMIT;
    private const string EmailClick = PredefinedActivityType.EMAIL_CLICK;

    // Range Mar 1–7 (Mar 2 is a Monday), previous period Feb 22–28.
    private static readonly DateOnly today = new(2026, 3, 31);
    private static readonly DateOnly previousPeriodFrom = new(2026, 2, 22);
    private static readonly DateOnly from = new(2026, 3, 1);
    private static readonly DateOnly to = new(2026, 3, 7);

    private static readonly Guid coffee = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid promo = new("00000000-0000-0000-0000-000000000002");

    private static readonly Guid coffeeTag = new("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid europeTag = new("00000000-0000-0000-0000-0000000000a2");
    private static readonly Guid dealsTag = new("00000000-0000-0000-0000-0000000000a3");
    private static readonly Guid draftTag = new("00000000-0000-0000-0000-0000000000a4");

    private const int Article = SeedBuilder.FirstSharedId + 1;
    private const int LandingPage = SeedBuilder.FirstSharedId + 2;
    private const int Snippet = SeedBuilder.FirstSharedId + 3;
    private const int FormClass = SeedBuilder.FirstSharedId + 4;

    private readonly ContactStatsRepository repository = new();

    // Page Coffee (item 101) is tagged "coffee" and its published version links item 201 ("europe", "coffee"); only its draft links
    // item 202 ("draft-only"). Page Promo (item 102) is tagged "deals". Taxonomy "Unused" has no tags.
    private protected override SeedBuilder CreateSeed() => new SeedBuilder()
        .Contact(ContactId, new DateTime(2026, 1, 10, 8, 0, 0, DateTimeKind.Unspecified))
        .Contact(11, new DateTime(2026, 1, 10, 8, 0, 0, DateTimeKind.Unspecified))
        .Language(English, "en", "English")
        .Workspace(MainWorkspace, "Main")
        .WebsiteChannel(1, "Site One", English)
        .ContentType(Article, "Article", ClassContentTypeType.WEBSITE)
        .ContentType(LandingPage, "Landing", ClassContentTypeType.WEBSITE)
        .ContentType(Snippet, "Snippet", ClassContentTypeType.REUSABLE)
        .Class(FormClass, "Contact us form", ClassType.FORM, null)
        .Item(101, Article, channelId: 1)
        .Item(102, LandingPage, channelId: 1)
        .Item(201, Snippet, workspaceId: MainWorkspace)
        .Item(202, Snippet, workspaceId: MainWorkspace)
        .WebPage(1, coffee, 1, 101)
        .WebPage(2, promo, 1, 102)
        .Variant(1101, 101, English, "Coffee", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified))
        .Variant(1102, 102, English, "Promo", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified))
        .Variant(1201, 201, English, "Snippet A", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified))
        .Variant(1202, 202, English, "Snippet B", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified))
        .CommonData(5001, 101, English, (int)VersionStatus.Published, isLatest: false, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified))
        .CommonData(5002, 101, English, (int)VersionStatus.Draft, isLatest: true, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified))
        .CommonData(5003, 102, English, (int)VersionStatus.Published, isLatest: true, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified))
        .Reference(1, 5001, 201)
        .Reference(2, 5002, 202)
        .Taxonomy(1, "Topics")
        .Taxonomy(2, "Regions")
        .Taxonomy(3, "Unused")
        .Tag(1, 1, coffeeTag, "coffee")
        .Tag(2, 2, europeTag, "europe")
        .Tag(3, 1, dealsTag, "deals")
        .Tag(4, 1, draftTag, "draft-only")
        .ItemTag(1, 1101, coffeeTag)
        .ItemTag(2, 1201, europeTag)
        .ItemTag(3, 1201, coffeeTag)
        .ItemTag(4, 1102, dealsTag)
        .ItemTag(5, 1202, draftTag)
        .Form(5, "Contact us", FormClass)
        .Activity(1, ContactId, At(3, 2, 9, 0), PageVisit, 1, coffee, English, "https://one.test/coffee?x=1")
        .Activity(2, ContactId, At(3, 2, 10, 0), PageVisit, 1, coffee, English, "https://one.test/coffee")
        .Activity(3, ContactId, At(3, 3, 9, 0), PageVisit, 1, promo, English, "https://one.test/promo")
        .Activity(4, ContactId, At(3, 2, 8, 59), Landing, 1, coffee, English, "https://one.test/coffee", "news", "a")
        .Activity(5, ContactId, At(3, 4, 12, 0), FormSubmit, 1, url: "https://one.test/contact", itemId: 5)
        .Activity(6, ContactId, At(3, 5, 12, 0), EmailClick, itemId: 7)
        .Activity(7, ContactId, At(3, 6, 12, 0), "customevent")
        // Previous period, before it, and another contact.
        .Activity(8, ContactId, At(2, 25, 12, 0), PageVisit, 1, coffee, English, "https://one.test/coffee")
        .Activity(9, ContactId, At(1, 15, 9, 0), PageVisit, 1, coffee, English, "https://one.test/coffee")
        .Activity(10, 11, At(3, 2, 9, 0), PageVisit, 1, coffee, English, "https://one.test/coffee");

    private static DateTime At(int month, int day, int hour, int minute) => new(2026, month, day, hour, minute, 0, DateTimeKind.Unspecified);

    [Test]
    public async Task GetInfo_ReturnsCreationAndTypesOfAnyDate()
    {
        var info = await repository.GetInfo(ContactId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(info.Exists, Is.True);
            Assert.That(info.Created, Is.EqualTo(new DateTime(2026, 1, 10, 8, 0, 0, DateTimeKind.Unspecified)));
            Assert.That(info.Types[0], Is.EqualTo(new ContactActivityTypeCount(PageVisit, 5, new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Unspecified))));
            Assert.That(info.Types.Select(t => t.ActivityType), Is.EquivalentTo(new[] { PageVisit, Landing, FormSubmit, EmailClick, "customevent" }));
        });
    }

    [Test]
    public async Task GetData_AllTypes_ReturnsTotalsAndListsOfTheContact()
    {
        var data = await GetData([]);

        Assert.Multiple(() =>
        {
            Assert.That(data.Totals, Is.EqualTo(new ContactStatsTotalsRow(
                Activities: 7, PreviousActivities: 1, Sessions: 1, PageVisits: 3, FormSubmissions: 1, EmailClicks: 1, ActiveDays: 5,
                CampaignSessions: 1, FirstSeen: new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Unspecified), LastSeen: new DateTime(2026, 3, 6, 12, 0, 0, DateTimeKind.Unspecified))));
            Assert.That(data.Daily.Sum(d => d.Count), Is.EqualTo(7));
            Assert.That(data.Pages, Is.EqualTo(new[]
            {
                new ContactStatsPageRow(coffee, English, 2, new DateTime(2026, 3, 2, 10, 0, 0, DateTimeKind.Unspecified), "https://one.test/coffee", "Coffee", "English", "Site One"),
                new ContactStatsPageRow(promo, English, 1, new DateTime(2026, 3, 3, 9, 0, 0, DateTimeKind.Unspecified), "https://one.test/promo", "Promo", "English", "Site One"),
            }));
            Assert.That(data.Forms, Is.EqualTo(new[] { new ContactStatsItemRow(5, 1, new DateTime(2026, 3, 4, 12, 0, 0, DateTimeKind.Unspecified), "Contact us") }));
            // No email tables in the test schema: the click is counted without a name.
            Assert.That(data.Emails, Is.EqualTo(new[] { new ContactStatsItemRow(7, 1, new DateTime(2026, 3, 5, 12, 0, 0, DateTimeKind.Unspecified), null) }));
            Assert.That(data.Sources, Is.EqualTo(new[] { new ContactStatsSourceRow("news", null, 1) }));
            Assert.That(data.SourceContents, Is.EqualTo(new[] { new ContactStatsSourceRow("news", "a", 1) }));
            Assert.That(data.Interests, Is.EqualTo(new[]
            {
                new ContactStatsInterestRow(Article, "Article", 2, 1),
                new ContactStatsInterestRow(LandingPage, "Landing", 1, 1),
            }));
            Assert.That(data.InterestVisits, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task GetData_OneType_CountsOnlyThatType()
    {
        var data = await GetData([PageVisit]);

        Assert.Multiple(() =>
        {
            Assert.That(data.Totals.Activities, Is.EqualTo(3));
            Assert.That(data.Totals.PreviousActivities, Is.EqualTo(1));
            Assert.That(data.Totals.Sessions, Is.Zero);
            Assert.That(data.Totals.LastSeen, Is.EqualTo(new DateTime(2026, 3, 3, 9, 0, 0, DateTimeKind.Unspecified)));
            Assert.That(data.Forms, Is.Empty);
            Assert.That(data.Sources, Is.Empty);
            Assert.That(data.Daily.Select(d => d.SeriesKey).Distinct(), Is.EqualTo(new[] { PageVisit }));
        });
    }

    [Test]
    public async Task Normalize_UnknownType_ReturnsNoActivities()
    {
        var (query, data) = await Load(["nosuchtype"]);

        Assert.Multiple(() =>
        {
            Assert.That(query.ActivityTypes, Is.EqualTo(new[] { "nosuchtype" }));
            Assert.That(data.Totals, Is.EqualTo(ContactStatsTotalsRow.Empty));
            Assert.That(data.Daily, Is.Empty);
            Assert.That(data.Pages, Is.Empty);
        });
    }

    [Test]
    public async Task Normalize_TypeInOtherCaseAndUnknownType_KeepsTheKnownType()
    {
        var (query, data) = await Load(["PAGEVISIT", "nosuchtype"]);

        Assert.Multiple(() =>
        {
            Assert.That(query.ActivityTypes, Is.EqualTo(new[] { PageVisit }));
            Assert.That(data.Totals.Activities, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task GetTags_AllTaxonomies_CountsEachVisitOncePerTagThroughPublishedLinksOnly()
    {
        var tags = await repository.GetTags(ContactId, from, to, [], null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(tags.Tags, Is.EqualTo(new[]
            {
                new ContactStatsTagRow(1, "coffee", "Topics", 2, 1),
                new ContactStatsTagRow(2, "europe", "Regions", 2, 1),
                new ContactStatsTagRow(3, "deals", "Topics", 1, 1),
            }));
            Assert.That(tags.TagCount, Is.EqualTo(3));
            Assert.That(tags.TagVisits, Is.EqualTo(3));
            Assert.That(tags.TaxonomyOptions, Is.EqualTo(new[]
            {
                new TagUsageTaxonomyOption(2, "Regions", 1),
                new TagUsageTaxonomyOption(1, "Topics", 2),
            }));
        });
    }

    [Test]
    public async Task GetTags_Taxonomy_KeepsOnlyItsTags()
    {
        var tags = await repository.GetTags(ContactId, from, to, [], 2, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(tags.Tags.Select(t => t.Title), Is.EqualTo(new[] { "europe" }));
            Assert.That(tags.TagVisits, Is.EqualTo(2));
            Assert.That(tags.TaxonomyOptions.Select(o => o.Id), Is.EquivalentTo(new[] { 1, 2 }));
        });
    }

    [Test]
    public async Task GetTags_TaxonomyWithoutTags_IsStillAnOption()
    {
        var tags = await repository.GetTags(ContactId, from, to, [], 3, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(tags.Tags, Is.Empty);
            Assert.That(tags.TagVisits, Is.Zero);
            Assert.That(tags.TaxonomyOptions, Does.Contain(new TagUsageTaxonomyOption(3, "Unused", 0)));
        });
    }

    [Test]
    public async Task GetTags_TypeWithoutPageVisits_ReturnsNoTags()
    {
        var tags = await repository.GetTags(ContactId, from, to, [FormSubmit], null, CancellationToken.None);

        Assert.That(tags.Tags, Is.Empty);
    }

    [Test]
    public async Task GetHeatmap_GroupsByWeekdayFromMondayAndHour()
    {
        var cells = await repository.GetHeatmap(ContactId, from, to, [], CancellationToken.None);

        Assert.That(cells, Is.EquivalentTo(new[]
        {
            new ContactHeatmapCell(0, 8, 1),
            new ContactHeatmapCell(0, 9, 1),
            new ContactHeatmapCell(0, 10, 1),
            new ContactHeatmapCell(1, 9, 1),
            new ContactHeatmapCell(2, 12, 1),
            new ContactHeatmapCell(3, 12, 1),
            new ContactHeatmapCell(4, 12, 1),
        }));
    }

    [Test]
    public async Task GetTaxonomyIds_ReturnsAll()
    {
        var ids = await repository.GetTaxonomyIds(CancellationToken.None);

        Assert.That(ids, Is.EquivalentTo(new[] { 1, 2, 3 }));
    }

    private Task<ContactStatsData> GetData(IReadOnlyList<string> types) =>
        repository.GetData(ContactId, previousPeriodFrom, from, to, types, CancellationToken.None);

    // Service path without the cache: the filter is normalized against the contact's types, then the batch runs with the result.
    private async Task<(ContactStatsQuery Query, ContactStatsData Data)> Load(IReadOnlyList<string> types)
    {
        var info = await repository.GetInfo(ContactId, CancellationToken.None);
        var filter = new ContactStatsFilter
        {
            AllTime = false,
            Range = new StatsFilter { From = from, To = to, Grouping = StatsGrouping.Day },
            ActivityTypes = types,
        };
        var query = filter.Normalize(today, info);
        var previousFrom = StatsComparison.GetPreviousRange(query.Range).From;

        return (query, await repository.GetData(ContactId, previousFrom, query.Range.From, query.Range.To, query.ActivityTypes, CancellationToken.None));
    }
}
