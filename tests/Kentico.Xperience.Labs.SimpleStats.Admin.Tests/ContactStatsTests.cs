using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.DigitalMarketing.UIPages;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ActivityCounts;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContactStats;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;
using Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class ContactStatsTests
{
    private const int ContactId = 9;
    private static readonly DateOnly today = new(2026, 10, 8);

    private static readonly ContactStatsInfo info = new(
        true,
        At(2026, 7, 15, 18, 0),
        [
            new("pagevisit", 7, At(2026, 8, 30, 18, 0)),
            new("bizformsubmit", 4, At(2026, 9, 7, 19, 0)),
            new("emailclick", 2, At(2026, 9, 23, 18, 0)),
            new("landingpage", 1, At(2026, 7, 1, 10, 0)),
        ]);

    private FakeRepository repository = null!;
    private FakeCache cache = null!;
    private FakeClock clock = null!;
    private ContactStatsService service = null!;

    [SetUp]
    public void SetUp()
    {
        repository = new FakeRepository { Info = info };
        cache = new FakeCache();
        clock = new FakeClock(new DateTimeOffset(2026, 10, 8, 10, 0, 0, TimeSpan.Zero));
        service = new ContactStatsService(repository, repository, repository, new FakeAdminLinks(), cache, cache, clock);
    }

    [Test]
    public void Normalize_AllTime_StartsAtCreatedOrFirstActivity_WhicheverIsEarlier()
    {
        var query = new ContactStatsFilter().Normalize(today, info);

        Assert.Multiple(() =>
        {
            Assert.That(query.AllTime, Is.True);
            // The landing on Jul 1 is earlier than the contact's creation (Jul 15).
            Assert.That(query.Range.From, Is.EqualTo(new DateOnly(2026, 7, 1)));
            Assert.That(query.Range.To, Is.EqualTo(today));
            Assert.That(query.Range.Grouping, Is.EqualTo(StatsGrouping.Week));
            Assert.That(query.Range.ChannelId, Is.Null);
            Assert.That(query.ActivityTypes, Is.Empty);
        });
    }

    [Test]
    public void Normalize_Range_UsesRangeAndIgnoresChannel()
    {
        var query = new ContactStatsFilter
        {
            AllTime = false,
            Range = new StatsFilter { From = new(2026, 9, 1), To = new(2026, 9, 30), Grouping = StatsGrouping.Day, ChannelId = 2 },
        }.Normalize(today, info);

        Assert.That(query.Range, Is.EqualTo(new StatsQuery(new(2026, 9, 1), new(2026, 9, 30), StatsGrouping.Day, null)));
    }

    [Test]
    public void Normalize_Types_IgnoresUnknownAndSorts()
    {
        var query = new ContactStatsFilter { ActivityTypes = ["pagevisit", "nope", "BizFormSubmit", "pagevisit", " "] }.Normalize(today, info);

        Assert.That(query.ActivityTypes, Is.EqualTo(new[] { "bizformsubmit", "pagevisit" }));
    }

    [Test]
    public void Normalize_OnlyUnknownTypes_AreKeptNotAll()
    {
        var query = new ContactStatsFilter { ActivityTypes = ["Nope", "nope", " other ", "a|pagevisit"] }.Normalize(today, info);

        Assert.That(query.ActivityTypes, Is.EqualTo(new[] { "a_pagevisit", "Nope", "other" }));
    }

    [Test]
    public void Normalize_KnownType_KeepsStoredCasing() =>
        Assert.That(new ContactStatsFilter { ActivityTypes = ["PAGEVISIT"] }.Normalize(today, info).ActivityTypes, Is.EqualTo(new[] { "pagevisit" }));

    [Test]
    public void Normalize_UnknownTypes_AreBounded()
    {
        var many = Enumerable.Range(0, 50).Select(i => $"x{i:00}").Append(new string('y', 500)).ToList();

        var query = new ContactStatsFilter { ActivityTypes = many }.Normalize(today, info);

        Assert.Multiple(() =>
        {
            Assert.That(query.ActivityTypes, Has.Count.EqualTo(ContactStatsFilter.MaxTypes));
            Assert.That(query.ActivityTypes.Max(t => t.Length), Is.LessThanOrEqualTo(ContactStatsFilter.MaxTypeLength));
            Assert.That(
                new ContactStatsFilter { ActivityTypes = [new string('y', 500)] }.Normalize(today, info).ActivityTypes.Single(),
                Has.Length.EqualTo(ContactStatsFilter.MaxTypeLength));
        });
    }

    [Test]
    public async Task GetReport_TypeOrder_SharesCacheEntry()
    {
        await service.GetReport(ContactId, new ContactStatsFilter { ActivityTypes = ["pagevisit", "emailclick"] }, today, refresh: false, CancellationToken.None);
        await service.GetReport(ContactId, new ContactStatsFilter { ActivityTypes = ["emailclick", "pagevisit"] }, today, refresh: false, CancellationToken.None);
        Assert.That(repository.DataCalls, Is.EqualTo(1));

        await service.GetReport(ContactId, new ContactStatsFilter { ActivityTypes = ["pagevisit"] }, today, refresh: false, CancellationToken.None);
        await service.GetReport(ContactId, null, today, refresh: false, CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(repository.DataCalls, Is.EqualTo(3));
            Assert.That(repository.LastTypes, Is.Empty);
        });
    }

    [Test]
    public async Task GetReport_PassesTypesAndPreviousPeriodToRepository()
    {
        var filter = new ContactStatsFilter
        {
            AllTime = false,
            Range = new StatsFilter { From = new(2026, 9, 1), To = new(2026, 9, 30) },
            ActivityTypes = ["pagevisit", "bizformsubmit"],
        };

        var result = await service.GetReport(ContactId, filter, today, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(repository.LastCall, Is.EqualTo((ContactId, new DateOnly(2026, 8, 2), new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30))));
            Assert.That(repository.LastTypes, Is.EqualTo(new[] { "bizformsubmit", "pagevisit" }));
            Assert.That(result.ActivityTypes, Is.EqualTo(new[] { "bizformsubmit", "pagevisit" }));
            Assert.That(result.Comparison, Is.Not.Null);
            Assert.That(result.ActivitiesPath, Is.EqualTo("ContactActivityList:9"));
            Assert.That(result.PagePath, Is.EqualTo("ContactStatsPage:9"));
        });
    }

    [Test]
    public async Task GetReport_AllTime_NoPreviousPeriodAndNoComparison()
    {
        var result = await service.GetReport(ContactId, null, today, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(repository.LastCall!.Value.PreviousFrom, Is.EqualTo(repository.LastCall.Value.From));
            Assert.That(result.AllTime, Is.True);
            Assert.That(result.Comparison, Is.Null);
            Assert.That(result.Insights.Select(i => i.Kind), Does.Not.Contain(ContactStatsInsights.Trend));
        });
    }

    [Test]
    public async Task GetReport_ContactWithoutActivities_EmptyReport()
    {
        repository.Info = new(true, At(2026, 10, 1), []);

        var result = await service.GetReport(ContactId, null, today, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.From, Is.EqualTo(new DateOnly(2026, 10, 1)));
            Assert.That(result.Totals.Activities, Is.Zero);
            Assert.That(result.Totals.LastSeen, Is.Null);
            Assert.That(result.TypeOptions, Is.Empty);
            Assert.That(result.Series.Series, Is.Empty);
            Assert.That(result.Insights, Is.Empty);
        });
    }

    [Test]
    public async Task GetReport_MissingContact_DoesNotQueryData()
    {
        repository.Info = ContactStatsInfo.Missing;

        Assert.That(await service.ContactExists(ContactId, CancellationToken.None), Is.False);
        await service.GetReport(ContactId, null, today, refresh: false, CancellationToken.None);
        Assert.That(repository.DataCalls, Is.Zero);
    }

    [Test]
    public async Task GetHeatmap_SeparateQuery_SameFilter_NotRunByLoad()
    {
        var filter = new ContactStatsFilter { ActivityTypes = ["pagevisit"] };

        await service.GetReport(ContactId, filter, today, refresh: false, CancellationToken.None);
        Assert.That(repository.HeatmapCalls, Is.Zero);

        repository.Heatmap = [new(0, 9, 2), new(6, 23, 5)];
        var heatmap = await service.GetHeatmap(ContactId, filter, today, refresh: false, CancellationToken.None);
        await service.GetHeatmap(ContactId, filter, today, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(repository.HeatmapCalls, Is.EqualTo(1));
            Assert.That(repository.LastTypes, Is.EqualTo(new[] { "pagevisit" }));
            Assert.That(heatmap.From, Is.EqualTo(new DateOnly(2026, 7, 1)));
            Assert.That(heatmap.Cells, Has.Count.EqualTo(7 * 24));
            Assert.That(heatmap.Max, Is.EqualTo(5));
            Assert.That(heatmap.Total, Is.EqualTo(7));
            Assert.That(heatmap.Cells.Single(c => c.Weekday == 0 && c.Hour == 9).Count, Is.EqualTo(2));
        });
    }

    [Test]
    public void Build_DeletedFormAndEmail_GetFallbackLabelsWithoutLinks()
    {
        var data = ContactStatsData.Empty with
        {
            Totals = ContactStatsTotalsRow.Empty with { Activities = 3, FormSubmissions = 2, EmailClicks = 1 },
            Forms = [new(1, 1, At(2026, 9, 1), "Coffee sample list"), new(99, 1, At(2026, 9, 2), null)],
            FormCount = 2,
            Emails = [new ContactStatsItemRow(7, 1, At(2026, 9, 3), null)],
            EmailCount = 1,
        };

        var result = Build(data);

        Assert.Multiple(() =>
        {
            Assert.That(result.Forms.Items.Select(i => i.Label), Is.EquivalentTo(new[] { "Coffee sample list", ContactStatsReportBuilder.DeletedFormLabel }));
            Assert.That(result.Forms.Items.Single(i => i.Key == "1").AdminPath, Is.EqualTo("form:1"));
            Assert.That(result.Forms.Items.Single(i => i.Key == "99").AdminPath, Is.Null);
            Assert.That(result.Emails.Items.Single().Label, Is.EqualTo(ContactStatsReportBuilder.DeletedEmailLabel));
            Assert.That(result.Emails.Items.Single().SecondaryLabel, Is.EqualTo("2026-09-03 00:00"));
            // The latest form submission is the deleted form.
            Assert.That(result.Insights.Single(i => i.Kind == ContactStatsInsights.LatestForm).Text, Is.EqualTo("Submitted (deleted form) 1 time"));
        });
    }

    [Test]
    public void Build_PagesInOtherChannels_AreListedWithTheirChannel()
    {
        var data = ContactStatsData.Empty with
        {
            Totals = ContactStatsTotalsRow.Empty with { Activities = 3, PageVisits = 3 },
            Pages =
            [
                new(Guid.NewGuid(), 1, 2, At(2026, 9, 1), "https://a.com/x", "X", "English", "Site A"),
                new(Guid.NewGuid(), 1, 1, At(2026, 9, 1), "https://b.com/y", "Y", "English", "Site B"),
            ],
            PageCount = 2,
        };

        var result = Build(data);

        Assert.Multiple(() =>
        {
            Assert.That(result.TopPages.Items.Select(i => i.SecondaryLabel), Is.EqualTo(new[] { "Site A · English", "Site B · English" }));
            Assert.That(result.TopPages.Items[0].Url, Is.EqualTo("https://a.com/x"));
            Assert.That(result.Insights.Single(i => i.Kind == ContactStatsInsights.MostVisited).Text, Is.EqualTo("Most visited: X"));
        });
    }

    [Test]
    public void Insights_ActiveVsNotSeen()
    {
        var active = Totals(activeDays: 4, daysSinceLastSeen: 3);
        var inactive = Totals(activeDays: 0, daysSinceLastSeen: 45);

        Assert.Multiple(() =>
        {
            Assert.That(Insights(active).Select(i => i.Text), Is.EqualTo(new[] { "Active on 4 of the last 30 days" }));
            Assert.That(Insights(inactive).Select(i => i.Text), Is.EqualTo(new[] { "Not seen for 45 days" }));
            Assert.That(Insights(Totals(activeDays: 0, daysSinceLastSeen: null)), Is.Empty);
            // Exactly the threshold is still "active".
            Assert.That(Insights(Totals(activeDays: 1, daysSinceLastSeen: ContactStatsInsights.InactiveDays)).Single().Kind, Is.EqualTo(ContactStatsInsights.Active));
        });
    }

    [Test]
    public void Insights_Trend_OnlyWithChange()
    {
        var range = new StatsQuery(new(2026, 9, 1), new(2026, 9, 30), StatsGrouping.Day, null);
        var totals = Totals(activeDays: 0, daysSinceLastSeen: null);

        Assert.Multiple(() =>
        {
            Assert.That(Insights(totals, StatsComparison.Create(range, 15, 10)).Single().Text, Is.EqualTo("Activity up 50% vs previous 30 days"));
            Assert.That(Insights(totals, StatsComparison.Create(range, 5, 10)).Single().Text, Is.EqualTo("Activity down 50% vs previous 30 days"));
            Assert.That(Insights(totals, StatsComparison.Create(range, 5, 5)), Is.Empty);
            Assert.That(Insights(totals, StatsComparison.Create(range, 5, 0)), Is.Empty);
        });
    }

    [Test]
    public void Insights_InterestAndCampaignSource()
    {
        var data = ContactStatsData.Empty with
        {
            Totals = ContactStatsTotalsRow.Empty with { Activities = 6, Sessions = 4, CampaignSessions = 3, PageVisits = 2 },
            Sources = [new("newsletter", null, 2), new("google", null, 1)],
            SourceCount = 2,
            SourceContents = [new("newsletter", "hero", 2), new("google", null, 1)],
            SourceContentCount = 2,
            Interests = [new(5, "Article page", 2, 1)],
            InterestCount = 1,
            InterestVisits = 2,
        };

        var withUtm = Build(data);
        var kinds = withUtm.Insights.Select(i => i.Kind).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(withUtm.Insights.Single(i => i.Kind == ContactStatsInsights.TopInterest).Text, Is.EqualTo("Top interest: Article page"));
            Assert.That(withUtm.Insights.Single(i => i.Kind == ContactStatsInsights.CampaignSource).Text, Is.EqualTo("Came from newsletter in 2 of 4 sessions"));
            Assert.That(withUtm.Campaigns.Items.Select(i => (i.Label, i.SecondaryLabel)), Is.EqualTo(new[] { ("newsletter", "hero"), ("google", "(none)") }));
            Assert.That(withUtm.HasAnyUtmData, Is.True);
            Assert.That(kinds, Does.Not.Contain(ContactStatsInsights.LatestForm));
        });

        var noSources = Build(data with { Sources = [], SourceContents = [], Totals = data.Totals with { CampaignSessions = 0 } });
        Assert.That(noSources.Insights.Select(i => i.Kind), Does.Not.Contain(ContactStatsInsights.CampaignSource));
    }

    [Test]
    public async Task GetReport_Taxonomy_PassedToRepositoryAndPartOfCacheKey()
    {
        await service.GetReport(ContactId, new ContactStatsFilter { TaxonomyId = 3 }, today, refresh: false, CancellationToken.None);
        Assert.That(repository.LastTaxonomyId, Is.EqualTo(3));

        await service.GetReport(ContactId, new ContactStatsFilter { TaxonomyId = 3 }, today, refresh: false, CancellationToken.None);
        await service.GetReport(ContactId, new ContactStatsFilter { TaxonomyId = 0 }, today, refresh: false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(repository.DataCalls, Is.EqualTo(2));
            // 0 or less means all taxonomies.
            Assert.That(repository.LastTaxonomyId, Is.Null);
        });
    }

    [Test]
    public void Build_Tags_RankedWithTaxonomy_AndTopInterestUsesTag()
    {
        var data = ContactStatsData.Empty with
        {
            Totals = ContactStatsTotalsRow.Empty with { Activities = 5, PageVisits = 5 },
            Interests = [new(5, "Article page", 5, 2)],
            InterestCount = 1,
            InterestVisits = 5,
            Tags = [new(30, "Winey", "Coffee tastes", 2, 1), new(32, "Arabica", "Coffee tastes", 4, 2)],
            TagCount = 2,
            TagVisits = 4,
            TaxonomyOptions = [new(3, "Coffee tastes", 2)],
        };

        var result = Build(data);

        Assert.Multiple(() =>
        {
            Assert.That(result.InterestTags.Items.Select(i => (i.Label, i.SecondaryLabel, i.Value, i.SecondaryValue)), Is.EqualTo(new[]
            {
                ("Arabica", "Coffee tastes", 4m, 2m),
                ("Winey", "Coffee tastes", 2m, (decimal?)1m),
            }));
            Assert.That(result.InterestTags.ItemCount, Is.EqualTo(2));
            Assert.That(result.TaxonomyOptions.Single().DisplayName, Is.EqualTo("Coffee tastes"));
            Assert.That(result.Insights.Single(i => i.Kind == ContactStatsInsights.TopInterest).Text, Is.EqualTo("Top interest: Arabica (Coffee tastes)"));
            Assert.That(result.Interests.Items.Single().Label, Is.EqualTo("Article page"));
        });
    }

    [Test]
    public void Build_NoTags_EmptyTagList_TopInterestFallsBackToContentType()
    {
        var data = ContactStatsData.Empty with
        {
            Totals = ContactStatsTotalsRow.Empty with { Activities = 1, PageVisits = 1 },
            Interests = [new(5, "Article page", 1, 1)],
            InterestCount = 1,
            InterestVisits = 1,
        };

        var result = Build(data);

        Assert.Multiple(() =>
        {
            Assert.That(result.InterestTags.Items, Is.Empty);
            Assert.That(result.TaxonomyOptions, Is.Empty);
            Assert.That(result.Insights.Single(i => i.Kind == ContactStatsInsights.TopInterest).Text, Is.EqualTo("Top interest: Article page"));
        });
    }

    [Test]
    public void TagsSql_DirectAndLinkedTags_PublishedReferencesOnly_LanguageFallback_OncePerVisit() => Assert.Multiple(() =>
    {
        string sql = ContactStatsSql.TagsQuery;

        // Direct page tags and tags of linked items (one level), merged.
        Assert.That(sql, Does.Contain("PI.[ContentItemID] AS [ItemID]"));
        Assert.That(sql, Does.Contain("R.[ContentItemReferenceTargetItemID]"));
        Assert.That(sql, Does.Contain("UNION"));
        // Only references of the page's published version in the visit's language: draft-only links are ignored.
        Assert.That(sql, Does.Contain("D.[ContentItemCommonDataVersionStatus] = @PublishedStatus"));
        Assert.That(sql, Does.Contain("D.[ContentItemCommonDataContentLanguageID] = PI.[LanguageID]"));
        // Tags of the visit's language variant first, else the variant with the lowest metadata ID.
        Assert.That(sql, Does.Contain("ORDER BY CASE WHEN M.[ContentItemLanguageMetadataContentLanguageID] = RC.[LanguageID] THEN 0 ELSE 1 END, M.[ContentItemLanguageMetadataID]"));
        // A visit counts once per tag, whatever the path.
        Assert.That(sql, Does.Contain("SELECT DISTINCT RC.[PageGUID], RC.[LanguageID], G.[TagID]"));
        Assert.That(sql, Does.Contain("PRIMARY KEY ([ActivityID], [TagID])"));
        // Taxonomy filter (0 = all) on the tags only; options keep all taxonomies plus the selected one.
        Assert.That(sql, Does.Contain("@TaxonomyID = 0 OR VT.[TaxonomyID] = @TaxonomyID"));
        Assert.That(sql, Does.Contain("OR X.[TaxonomyID] = @TaxonomyID"));
        // Only page visits in the range (and so the activity type filter) contribute.
        Assert.That(sql, Does.Contain("X.[IsCurrent] = 1 AND X.[Type] = @PageVisitType"));
        string batch = ContactStatsSql.Batch;
        Assert.That(batch, Does.Contain(sql));
    });

    [Test]
    public void Build_TypeOptions_UseDisplayNames()
    {
        var result = ContactStatsReportBuilder.Build(
            ContactId,
            new ContactStatsFilter().Normalize(today, info),
            info,
            ContactStatsData.Empty,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["pagevisit"] = "Page visit" },
            false,
            today,
            _ => null,
            (_, _, _) => null);

        Assert.That(result.TypeOptions.Select(o => (o.ActivityType, o.DisplayName, o.Count)), Is.EqualTo(new[]
        {
            ("pagevisit", "Page visit", 7),
            ("bizformsubmit", "bizformsubmit", 4),
            ("emailclick", "emailclick", 2),
            ("landingpage", "landingpage", 1),
        }));
    }

    [Test]
    public void TypesKey_IsOrderIndependentForSortedTypes_AndLengthPrefixed() => Assert.Multiple(() =>
    {
        Assert.That(ContactStatsService.TypesKey([]), Is.EqualTo("all"));
        Assert.That(ContactStatsService.TypesKey(["a", "bc"]), Is.EqualTo("1:a,2:bc"));
    });

    [Test]
    public void Sql_FiltersContactAndTypes_NoChannelFilter() => Assert.Multiple(() =>
    {
        foreach (string sql in new[] { ContactStatsSql.Batch, ContactStatsSql.HeatmapQuery })
        {
            Assert.That(sql, Does.Contain("A.[ActivityContactID] = @ContactID"));
            Assert.That(sql, Does.Contain(ContactStatsSql.TypeCondition));
            Assert.That(sql, Does.Contain("A.[ActivityCreated] < @ToExclusive"));
            Assert.That(sql, Does.Not.Contain("ActivityChannelID"));
        }

        Assert.That(ContactStatsSql.Batch, Does.Contain(StatsUtm.SourceColumn));
        Assert.That(ContactStatsSql.Batch, Does.Not.Contain("Weekday"));
    });

    [Test]
    public void Parameters_TypesAsOneDelimitedList()
    {
        var parameters = ContactStatsRepository.CreateParameters(ContactId, today, today, ["bizformsubmit", "pagevisit"]);
        var all = ContactStatsRepository.CreateParameters(ContactId, today, today, []);

        Assert.Multiple(() =>
        {
            Assert.That(parameters["@Types"].Value, Is.EqualTo("|bizformsubmit|pagevisit|"));
            Assert.That(parameters["@AllTypes"].Value, Is.False);
            Assert.That(all["@Types"].Value, Is.EqualTo(string.Empty));
            Assert.That(all["@AllTypes"].Value, Is.True);
        });
    }

    [Test]
    public void Application_DeclaresContactStatsPermission()
    {
        var permissions = typeof(StatsApplicationPage)
            .GetCustomAttributes(typeof(UIPermissionAttribute), false)
            .Cast<UIPermissionAttribute>();

        Assert.That(permissions.Select(p => (p.Name, p.DisplayName)), Does.Contain(("SimpleStats.ContactStats", "Contact stats")));
    }

    [Test]
    public void Page_IsRegisteredUnderContactEditSection()
    {
        var registration = typeof(ContactStatsPage).Assembly
            .GetCustomAttributes(typeof(UIPageAttribute), false)
            .Cast<UIPageAttribute>()
            .Single(a => a.Type == typeof(ContactStatsPage));

        Assert.Multiple(() =>
        {
            Assert.That(registration.ParentType, Is.EqualTo(typeof(ContactEditSection)));
            Assert.That(registration.Order, Is.EqualTo(1001));
            Assert.That(registration.Name, Is.EqualTo("Stats (Labs)"));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Page_Commands_RequirePermission(bool granted)
    {
        var evaluator = new FakePermissionEvaluator(granted ? [StatsPermissions.CONTACT_STATS] : []);
        var page = new ContactStatsPage(service, evaluator, new FakePublisher(), clock) { ContactId = ContactId };

        if (granted)
        {
            var properties = await page.ConfigureTemplateProperties(new ContactStatsClientProperties());
            Assert.Multiple(() =>
            {
                Assert.That(properties.Report, Is.Not.Null);
                Assert.That(properties.CanExport, Is.False);
            });
        }
        else
        {
            Assert.ThrowsAsync<ForbiddenAccessException>(() => page.Load(new ContactStatsLoadRequest(), CancellationToken.None));
            Assert.ThrowsAsync<ForbiddenAccessException>(() => page.LoadHeatmap(new ContactStatsLoadRequest(), CancellationToken.None));
            Assert.ThrowsAsync<ForbiddenAccessException>(() => page.ConfigureTemplateProperties(new ContactStatsClientProperties()));
        }
    }

    // Activity times are server local time, as stored.
    private static DateTime At(int year, int month, int day, int hour = 0, int minute = 0) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);

    private static ContactStatsResult Build(ContactStatsData data) =>
        ContactStatsReportBuilder.Build(
            ContactId,
            new ContactStatsFilter().Normalize(today, info),
            info,
            data,
            new Dictionary<string, string>(),
            hasAnyUtmData: false,
            today,
            formId => $"form:{formId}",
            (emailId, _, _) => $"email:{emailId}");

    private static ContactStatsTotals Totals(int activeDays, int? daysSinceLastSeen) =>
        new(0, 0, 0, 0, 0, activeDays, 30, null, null, daysSinceLastSeen);

    private static List<ContactStatsInsight> Insights(ContactStatsTotals totals, StatsComparison? comparison = null)
    {
        var empty = StatsRankedBuilder.Build(new StatsQuery(today, today, StatsGrouping.Day, null), [], 0, 0, 1);
        return ContactStatsReportBuilder.BuildInsights(totals, comparison, empty, empty, [], []);
    }

    private sealed class FakeRepository : IContactStatsRepository, IActivityCountsRepository, IStatsUtmDataRepository
    {
        public ContactStatsInfo Info { get; set; } = ContactStatsInfo.Missing;

        public ContactStatsData Data { get; set; } = ContactStatsData.Empty;

        public IReadOnlyList<ContactHeatmapCell> Heatmap { get; set; } = [];

        public int DataCalls { get; private set; }

        public int HeatmapCalls { get; private set; }

        public (int ContactId, DateOnly PreviousFrom, DateOnly From, DateOnly To)? LastCall { get; private set; }

        public IReadOnlyList<string>? LastTypes { get; private set; }

        public Task<ContactStatsInfo> GetInfo(int contactId, CancellationToken cancellationToken) => Task.FromResult(Info);

        public int? LastTaxonomyId { get; private set; }

        public Task<ContactStatsData> GetData(int contactId, DateOnly previousFrom, DateOnly from, DateOnly to, IReadOnlyList<string> activityTypes, int? taxonomyId, CancellationToken cancellationToken)
        {
            DataCalls++;
            LastTaxonomyId = taxonomyId;
            LastCall = (contactId, previousFrom, from, to);
            LastTypes = activityTypes;
            return Task.FromResult(Data);
        }

        public Task<IReadOnlyList<ContactHeatmapCell>> GetHeatmap(int contactId, DateOnly from, DateOnly to, IReadOnlyList<string> activityTypes, CancellationToken cancellationToken)
        {
            HeatmapCalls++;
            LastTypes = activityTypes;
            return Task.FromResult(Heatmap);
        }

        public Task<IReadOnlyList<ActivityDailyCount>> GetDailyCounts(DateOnly from, DateOnly to, int? channelId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<string, string>> GetActivityTypeDisplayNames(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());

        public Task<bool> HasAnyUtmData(CancellationToken cancellationToken) => Task.FromResult(false);
    }

    private sealed class FakeAdminLinks : IStatsAdminLinks
    {
        public string? GetPath<TPage>(PageParameterValues? parameters = null) =>
            parameters?.TryGetValue(typeof(ContactEditSection), out object? id) == true ? $"{typeof(TPage).Name}:{id}" : typeof(TPage).Name;
    }

    private sealed class FakePermissionEvaluator(IEnumerable<string> granted) : IStatsApplicationPermissionEvaluator
    {
        private readonly HashSet<string> granted = [.. granted];

        public Task<bool> IsGranted(string permission) => Task.FromResult(granted.Contains(permission));
    }

    private sealed class FakePublisher : IStatsExportEventPublisher
    {
        public Task<bool> Publish(Type reportPageType, StatsExportLogRequest? request, CancellationToken cancellationToken) => Task.FromResult(true);
    }
}
