using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class StatsNavigationTests
{
    private static readonly string[] allPermissions =
    [
        StatsPermissions.ACTIVITY_COUNTS,
        StatsPermissions.TOP_PAGES,
        StatsPermissions.NEW_CONTACTS,
        StatsPermissions.FORM_SUBMISSIONS,
        StatsPermissions.CONTENT_INVENTORY,
        StatsPermissions.PUBLISHING_CALENDAR,
        StatsPermissions.CONTENT_LOCKS,
        StatsPermissions.PAGE_FRESHNESS,
        StatsPermissions.REUSABLE_USAGE,
        StatsPermissions.PUBLISHING_ACTIVITY,
        StatsPermissions.TRANSLATION_STATUS,
        StatsPermissions.EDITOR_CONTRIBUTIONS,
        StatsPermissions.TAG_USAGE,
        StatsPermissions.EVENT_LOG,
        StatsPermissions.ORDERS_REVENUE,
        StatsPermissions.CUSTOMERS,
        StatsPermissions.MEMBERS,
        StatsPermissions.CONSENTS,
        StatsPermissions.EMAIL_SUMMARY,
        StatsPermissions.RECIPIENT_LISTS,
    ];

    [Test]
    public void Application_HasSectionsInOrderThenNoReportsPage()
    {
        var children = StatsNavigation.GetChildPages(typeof(StatsApplicationPage));
        var sections = children.Where(c => typeof(StatsSectionPage).IsAssignableFrom(c.Type)).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(children.Select(s => s.Slug), Is.EqualTo(new[] { "contacts", "emails", "content", "commerce", "system", "no-reports" }));
            Assert.That(children[^1].Type, Is.EqualTo(typeof(StatsNoReportsPage)));
            Assert.That(sections.Select(s => s.TemplateName), Is.All.EqualTo(TemplateNames.SECTION_LAYOUT));
        });
    }

    [TestCase(typeof(StatsContactsSection), new[] { "activity-counts", "top-pages", "new-contacts", "form-submissions", "members", "consents" })]
    [TestCase(typeof(StatsEmailsSection), new[] { "email-summary", "recipient-lists" })]
    [TestCase(typeof(StatsContentSection), new[] { "content-inventory", "publishing-calendar", "content-locks", "page-freshness", "reusable-usage", "publishing-activity", "translation-status", "editor-contributions", "tag-usage" })]
    [TestCase(typeof(StatsCommerceSection), new[] { "orders-revenue", "customers" })]
    [TestCase(typeof(StatsSystemSection), new[] { "event-log" })]
    public void Section_HasReportsInOrder(Type sectionType, string[] slugs) =>
        Assert.That(StatsNavigation.GetChildPages(sectionType).Select(p => p.Slug), Is.EqualTo(slugs));

    [Test]
    public void EveryReportPermission_BelongsToOneReport()
    {
        var reportPermissions = StatsNavigation.GetChildPages(typeof(StatsApplicationPage))
            .Where(section => typeof(StatsSectionPage).IsAssignableFrom(section.Type))
            .SelectMany(section => StatsNavigation.GetChildPages(section.Type))
            .Select(report => report.Type.GetCustomAttributes(typeof(UIEvaluatePermissionAttribute), true)
                .Cast<UIEvaluatePermissionAttribute>()
                .Single()
                .Permission);

        Assert.That(reportPermissions, Is.EquivalentTo(allPermissions));
    }

    [Test]
    public async Task AllGranted_NothingDenied()
    {
        var denied = await StatsNavigation.GetDeniedChildSlugs(typeof(StatsApplicationPage), Granted(allPermissions));

        Assert.That(denied, Is.Empty);
    }

    [Test]
    public async Task OnlyCustomers_HidesOtherSectionsAndOrdersRevenue()
    {
        var isGranted = Granted(StatsPermissions.CUSTOMERS);

        var deniedSections = await StatsNavigation.GetDeniedChildSlugs(typeof(StatsApplicationPage), isGranted);
        var deniedReports = await StatsNavigation.GetDeniedChildSlugs(typeof(StatsCommerceSection), isGranted);

        Assert.Multiple(() =>
        {
            Assert.That(deniedSections, Is.EquivalentTo(new[] { "contacts", "emails", "content", "system" }));
            Assert.That(deniedReports, Is.EquivalentTo(new[] { "orders-revenue" }));
            Assert.That(StatsNavigation.GetDefaultRoute(ChildRoutes(typeof(StatsApplicationPage)), deniedSections)?.Path, Is.EqualTo("commerce"));
            Assert.That(StatsNavigation.GetDefaultRoute(ChildRoutes(typeof(StatsCommerceSection)), deniedReports)?.Path, Is.EqualTo("customers"));
        });
    }

    [Test]
    public async Task OnlyMembers_ShowsContactsSection_AndOpensMembers()
    {
        var isGranted = Granted(StatsPermissions.MEMBERS);

        var deniedSections = await StatsNavigation.GetDeniedChildSlugs(typeof(StatsApplicationPage), isGranted);
        var deniedReports = await StatsNavigation.GetDeniedChildSlugs(typeof(StatsContactsSection), isGranted);

        Assert.Multiple(() =>
        {
            Assert.That(deniedSections, Is.EquivalentTo(new[] { "emails", "content", "commerce", "system" }));
            Assert.That(deniedReports, Is.EquivalentTo(new[] { "activity-counts", "top-pages", "new-contacts", "form-submissions", "consents" }));
            Assert.That(StatsNavigation.GetDefaultRoute(ChildRoutes(typeof(StatsApplicationPage)), deniedSections)?.Path, Is.EqualTo("contacts"));
            Assert.That(StatsNavigation.GetDefaultRoute(ChildRoutes(typeof(StatsContactsSection)), deniedReports)?.Path, Is.EqualTo("members"));
        });
    }

    [Test]
    public async Task OnlyConsents_ShowsContactsSection_AndOpensConsents()
    {
        var isGranted = Granted(StatsPermissions.CONSENTS);

        var deniedSections = await StatsNavigation.GetDeniedChildSlugs(typeof(StatsApplicationPage), isGranted);
        var deniedReports = await StatsNavigation.GetDeniedChildSlugs(typeof(StatsContactsSection), isGranted);

        Assert.Multiple(() =>
        {
            Assert.That(deniedSections, Is.EquivalentTo(new[] { "emails", "content", "commerce", "system" }));
            Assert.That(deniedReports, Is.EquivalentTo(new[] { "activity-counts", "top-pages", "new-contacts", "form-submissions", "members" }));
            Assert.That(StatsNavigation.GetDefaultRoute(ChildRoutes(typeof(StatsContactsSection)), deniedReports)?.Path, Is.EqualTo("consents"));
        });
    }

    [Test]
    public async Task OnlyRecipientLists_ShowsOnlyEmailsSection_AndOpensRecipientLists()
    {
        var isGranted = Granted(StatsPermissions.RECIPIENT_LISTS);

        var deniedSections = await StatsNavigation.GetDeniedChildSlugs(typeof(StatsApplicationPage), isGranted);
        var deniedReports = await StatsNavigation.GetDeniedChildSlugs(typeof(StatsEmailsSection), isGranted);

        Assert.Multiple(() =>
        {
            Assert.That(deniedSections, Is.EquivalentTo(new[] { "contacts", "content", "commerce", "system" }));
            Assert.That(deniedReports, Is.EquivalentTo(new[] { "email-summary" }));
            Assert.That(StatsNavigation.GetDefaultRoute(ChildRoutes(typeof(StatsApplicationPage)), deniedSections)?.Path, Is.EqualTo("emails"));
            Assert.That(StatsNavigation.GetDefaultRoute(ChildRoutes(typeof(StatsEmailsSection)), deniedReports)?.Path, Is.EqualTo("recipient-lists"));
        });
    }

    [Test]
    public async Task OnlyEmailSummary_ShowsOnlyEmailsSection_AndOpensEmailSummary()
    {
        var isGranted = Granted(StatsPermissions.EMAIL_SUMMARY);

        var deniedSections = await StatsNavigation.GetDeniedChildSlugs(typeof(StatsApplicationPage), isGranted);
        var deniedReports = await StatsNavigation.GetDeniedChildSlugs(typeof(StatsEmailsSection), isGranted);

        Assert.Multiple(() =>
        {
            Assert.That(deniedSections, Is.EquivalentTo(new[] { "contacts", "content", "commerce", "system" }));
            Assert.That(deniedReports, Is.EquivalentTo(new[] { "recipient-lists" }));
            Assert.That(StatsNavigation.GetDefaultRoute(ChildRoutes(typeof(StatsApplicationPage)), deniedSections)?.Path, Is.EqualTo("emails"));
            Assert.That(StatsNavigation.GetDefaultRoute(ChildRoutes(typeof(StatsEmailsSection)), deniedReports)?.Path, Is.EqualTo("email-summary"));
        });
    }

    [Test]
    public async Task BothEmailReports_OpensEmailSummaryFirst()
    {
        var isGranted = Granted(StatsPermissions.EMAIL_SUMMARY, StatsPermissions.RECIPIENT_LISTS);

        var deniedReports = await StatsNavigation.GetDeniedChildSlugs(typeof(StatsEmailsSection), isGranted);

        Assert.Multiple(() =>
        {
            Assert.That(deniedReports, Is.Empty);
            Assert.That(StatsNavigation.GetDefaultRoute(ChildRoutes(typeof(StatsEmailsSection)), deniedReports)?.Path, Is.EqualTo("email-summary"));
        });
    }

    [Test]
    public async Task WithoutEmailReports_HidesEmailsSection()
    {
        var denied = await StatsNavigation.GetDeniedChildSlugs(
            typeof(StatsApplicationPage),
            Granted([.. allPermissions.Where(p => p is not StatsPermissions.RECIPIENT_LISTS and not StatsPermissions.EMAIL_SUMMARY)]));

        Assert.That(denied, Is.EquivalentTo(new[] { "emails" }));
    }

    [Test]
    public async Task OnlyPublishingCalendar_ShowsOnlyContentSection_AndOpensPublishingCalendar()
    {
        var isGranted = Granted(StatsPermissions.PUBLISHING_CALENDAR);

        var deniedSections = await StatsNavigation.GetDeniedChildSlugs(typeof(StatsApplicationPage), isGranted);
        var deniedReports = await StatsNavigation.GetDeniedChildSlugs(typeof(StatsContentSection), isGranted);

        Assert.Multiple(() =>
        {
            Assert.That(deniedSections, Is.EquivalentTo(new[] { "contacts", "emails", "commerce", "system" }));
            Assert.That(deniedReports, Is.EquivalentTo(new[] { "content-inventory", "content-locks", "page-freshness", "reusable-usage", "publishing-activity", "translation-status", "editor-contributions", "tag-usage" }));
            Assert.That(StatsNavigation.GetDefaultRoute(ChildRoutes(typeof(StatsApplicationPage)), deniedSections)?.Path, Is.EqualTo("content"));
            Assert.That(StatsNavigation.GetDefaultRoute(ChildRoutes(typeof(StatsContentSection)), deniedReports)?.Path, Is.EqualTo("publishing-calendar"));
        });
    }

    [Test]
    public async Task OnlyContentLocks_ShowsOnlyContentSection_AndOpensContentLocks()
    {
        var isGranted = Granted(StatsPermissions.CONTENT_LOCKS);

        var deniedSections = await StatsNavigation.GetDeniedChildSlugs(typeof(StatsApplicationPage), isGranted);
        var deniedReports = await StatsNavigation.GetDeniedChildSlugs(typeof(StatsContentSection), isGranted);

        Assert.Multiple(() =>
        {
            Assert.That(deniedSections, Is.EquivalentTo(new[] { "contacts", "emails", "commerce", "system" }));
            Assert.That(deniedReports, Is.EquivalentTo(new[] { "content-inventory", "publishing-calendar", "page-freshness", "reusable-usage", "publishing-activity", "translation-status", "editor-contributions", "tag-usage" }));
            Assert.That(StatsNavigation.GetDefaultRoute(ChildRoutes(typeof(StatsApplicationPage)), deniedSections)?.Path, Is.EqualTo("content"));
            Assert.That(StatsNavigation.GetDefaultRoute(ChildRoutes(typeof(StatsContentSection)), deniedReports)?.Path, Is.EqualTo("content-locks"));
        });
    }

    [Test]
    public async Task OnlyPageFreshness_ShowsOnlyContentSection_AndOpensPageFreshness()
    {
        var isGranted = Granted(StatsPermissions.PAGE_FRESHNESS);

        var deniedSections = await StatsNavigation.GetDeniedChildSlugs(typeof(StatsApplicationPage), isGranted);
        var deniedReports = await StatsNavigation.GetDeniedChildSlugs(typeof(StatsContentSection), isGranted);

        Assert.Multiple(() =>
        {
            Assert.That(deniedSections, Is.EquivalentTo(new[] { "contacts", "emails", "commerce", "system" }));
            Assert.That(deniedReports, Is.EquivalentTo(new[] { "content-inventory", "publishing-calendar", "content-locks", "reusable-usage", "publishing-activity", "translation-status", "editor-contributions", "tag-usage" }));
            Assert.That(StatsNavigation.GetDefaultRoute(ChildRoutes(typeof(StatsApplicationPage)), deniedSections)?.Path, Is.EqualTo("content"));
            Assert.That(StatsNavigation.GetDefaultRoute(ChildRoutes(typeof(StatsContentSection)), deniedReports)?.Path, Is.EqualTo("page-freshness"));
        });
    }

    [Test]
    public async Task OnlyReusableUsage_ShowsOnlyContentSection_AndOpensReusableUsage()
    {
        var isGranted = Granted(StatsPermissions.REUSABLE_USAGE);

        var deniedSections = await StatsNavigation.GetDeniedChildSlugs(typeof(StatsApplicationPage), isGranted);
        var deniedReports = await StatsNavigation.GetDeniedChildSlugs(typeof(StatsContentSection), isGranted);

        Assert.Multiple(() =>
        {
            Assert.That(deniedSections, Is.EquivalentTo(new[] { "contacts", "emails", "commerce", "system" }));
            Assert.That(deniedReports, Is.EquivalentTo(new[] { "content-inventory", "publishing-calendar", "content-locks", "page-freshness", "publishing-activity", "translation-status", "editor-contributions", "tag-usage" }));
            Assert.That(StatsNavigation.GetDefaultRoute(ChildRoutes(typeof(StatsApplicationPage)), deniedSections)?.Path, Is.EqualTo("content"));
            Assert.That(StatsNavigation.GetDefaultRoute(ChildRoutes(typeof(StatsContentSection)), deniedReports)?.Path, Is.EqualTo("reusable-usage"));
        });
    }

    [Test]
    public async Task OnlyPublishingActivity_ShowsOnlyContentSection_AndOpensPublishingActivity()
    {
        var isGranted = Granted(StatsPermissions.PUBLISHING_ACTIVITY);

        var deniedSections = await StatsNavigation.GetDeniedChildSlugs(typeof(StatsApplicationPage), isGranted);
        var deniedReports = await StatsNavigation.GetDeniedChildSlugs(typeof(StatsContentSection), isGranted);

        Assert.Multiple(() =>
        {
            Assert.That(deniedSections, Is.EquivalentTo(new[] { "contacts", "emails", "commerce", "system" }));
            Assert.That(deniedReports, Is.EquivalentTo(new[] { "content-inventory", "publishing-calendar", "content-locks", "page-freshness", "reusable-usage", "translation-status", "editor-contributions", "tag-usage" }));
            Assert.That(StatsNavigation.GetDefaultRoute(ChildRoutes(typeof(StatsApplicationPage)), deniedSections)?.Path, Is.EqualTo("content"));
            Assert.That(StatsNavigation.GetDefaultRoute(ChildRoutes(typeof(StatsContentSection)), deniedReports)?.Path, Is.EqualTo("publishing-activity"));
        });
    }

    [Test]
    public async Task OnlyTranslationStatus_ShowsOnlyContentSection_AndOpensTranslationStatus()
    {
        var isGranted = Granted(StatsPermissions.TRANSLATION_STATUS);

        var deniedSections = await StatsNavigation.GetDeniedChildSlugs(typeof(StatsApplicationPage), isGranted);
        var deniedReports = await StatsNavigation.GetDeniedChildSlugs(typeof(StatsContentSection), isGranted);

        Assert.Multiple(() =>
        {
            Assert.That(deniedSections, Is.EquivalentTo(new[] { "contacts", "emails", "commerce", "system" }));
            Assert.That(deniedReports, Is.EquivalentTo(new[] { "content-inventory", "publishing-calendar", "content-locks", "page-freshness", "reusable-usage", "publishing-activity", "editor-contributions", "tag-usage" }));
            Assert.That(StatsNavigation.GetDefaultRoute(ChildRoutes(typeof(StatsApplicationPage)), deniedSections)?.Path, Is.EqualTo("content"));
            Assert.That(StatsNavigation.GetDefaultRoute(ChildRoutes(typeof(StatsContentSection)), deniedReports)?.Path, Is.EqualTo("translation-status"));
        });
    }

    [Test]
    public async Task OnlyEditorContributions_ShowsOnlyContentSection_AndOpensEditorContributions()
    {
        var isGranted = Granted(StatsPermissions.EDITOR_CONTRIBUTIONS);

        var deniedSections = await StatsNavigation.GetDeniedChildSlugs(typeof(StatsApplicationPage), isGranted);
        var deniedReports = await StatsNavigation.GetDeniedChildSlugs(typeof(StatsContentSection), isGranted);

        Assert.Multiple(() =>
        {
            Assert.That(deniedSections, Is.EquivalentTo(new[] { "contacts", "emails", "commerce", "system" }));
            Assert.That(deniedReports, Is.EquivalentTo(new[] { "content-inventory", "publishing-calendar", "content-locks", "page-freshness", "reusable-usage", "publishing-activity", "translation-status", "tag-usage" }));
            Assert.That(StatsNavigation.GetDefaultRoute(ChildRoutes(typeof(StatsApplicationPage)), deniedSections)?.Path, Is.EqualTo("content"));
            Assert.That(StatsNavigation.GetDefaultRoute(ChildRoutes(typeof(StatsContentSection)), deniedReports)?.Path, Is.EqualTo("editor-contributions"));
        });
    }

    [Test]
    public async Task OnlyTagUsage_ShowsOnlyContentSection_AndOpensTagUsage()
    {
        var isGranted = Granted(StatsPermissions.TAG_USAGE);

        var deniedSections = await StatsNavigation.GetDeniedChildSlugs(typeof(StatsApplicationPage), isGranted);
        var deniedReports = await StatsNavigation.GetDeniedChildSlugs(typeof(StatsContentSection), isGranted);

        Assert.Multiple(() =>
        {
            Assert.That(deniedSections, Is.EquivalentTo(new[] { "contacts", "emails", "commerce", "system" }));
            Assert.That(deniedReports, Is.EquivalentTo(new[] { "content-inventory", "publishing-calendar", "content-locks", "page-freshness", "reusable-usage", "publishing-activity", "translation-status", "editor-contributions" }));
            Assert.That(StatsNavigation.GetDefaultRoute(ChildRoutes(typeof(StatsApplicationPage)), deniedSections)?.Path, Is.EqualTo("content"));
            Assert.That(StatsNavigation.GetDefaultRoute(ChildRoutes(typeof(StatsContentSection)), deniedReports)?.Path, Is.EqualTo("tag-usage"));
        });
    }

    [Test]
    public async Task AllContentReports_OpensContentInventoryFirst()
    {
        var isGranted = Granted(StatsPermissions.CONTENT_INVENTORY, StatsPermissions.PUBLISHING_CALENDAR, StatsPermissions.CONTENT_LOCKS, StatsPermissions.PAGE_FRESHNESS, StatsPermissions.REUSABLE_USAGE, StatsPermissions.PUBLISHING_ACTIVITY, StatsPermissions.TRANSLATION_STATUS, StatsPermissions.EDITOR_CONTRIBUTIONS, StatsPermissions.TAG_USAGE);

        var deniedReports = await StatsNavigation.GetDeniedChildSlugs(typeof(StatsContentSection), isGranted);

        Assert.Multiple(() =>
        {
            Assert.That(deniedReports, Is.Empty);
            Assert.That(StatsNavigation.GetDefaultRoute(ChildRoutes(typeof(StatsContentSection)), deniedReports)?.Path, Is.EqualTo("content-inventory"));
        });
    }

    [Test]
    public async Task WithoutContentReports_HidesContentSection()
    {
        var denied = await StatsNavigation.GetDeniedChildSlugs(
            typeof(StatsApplicationPage),
            Granted([.. allPermissions.Where(p => p is not StatsPermissions.CONTENT_INVENTORY and not StatsPermissions.PUBLISHING_CALENDAR and not StatsPermissions.CONTENT_LOCKS and not StatsPermissions.PAGE_FRESHNESS and not StatsPermissions.REUSABLE_USAGE and not StatsPermissions.PUBLISHING_ACTIVITY and not StatsPermissions.TRANSLATION_STATUS and not StatsPermissions.EDITOR_CONTRIBUTIONS and not StatsPermissions.TAG_USAGE)]));

        Assert.That(denied, Is.EquivalentTo(new[] { "content" }));
    }

    [Test]
    public async Task NoReportPermission_DeniesAllSectionsAndOpensNoReportsPage()
    {
        var denied = await StatsNavigation.GetDeniedChildSlugs(typeof(StatsApplicationPage), Granted());

        Assert.Multiple(() =>
        {
            Assert.That(denied, Is.EquivalentTo(new[] { "contacts", "emails", "content", "commerce", "system" }));
            Assert.That(StatsNavigation.GetDefaultRoute(ChildRoutes(typeof(StatsApplicationPage)), denied)?.Path, Is.EqualTo("no-reports"));
        });
    }

    [Test]
    public void FilterNavigation_RemovesDeniedItems()
    {
        var items = new[]
        {
            new NavigationItem { Path = "orders-revenue" },
            new NavigationItem { Path = "customers" },
            new NavigationItem { Path = "custom-page" },
        };

        var filtered = StatsNavigation.FilterNavigation(items, new HashSet<string> { "orders-revenue" });

        Assert.That(filtered.Select(i => i.Path), Is.EqualTo(new[] { "customers", "custom-page" }));
    }

    private static Func<string, Task<bool>> Granted(params string[] permissions) =>
        permission => Task.FromResult(permissions.Contains(permission));

    [Test]
    public async Task SectionWithoutAllowedReport_HasNoDefaultRoute()
    {
        var denied = await StatsNavigation.GetDeniedChildSlugs(typeof(StatsCommerceSection), Granted(StatsPermissions.EVENT_LOG));

        Assert.That(StatsNavigation.GetDefaultRoute(ChildRoutes(typeof(StatsCommerceSection)), denied), Is.Null);
    }

    // Same as the product: one route per child page, by order, with the slug as path.
    private static IEnumerable<Route> ChildRoutes(Type parentType) =>
        StatsNavigation.GetChildPages(parentType).Select(page => new Route { Path = page.Slug }).ToList();
}
