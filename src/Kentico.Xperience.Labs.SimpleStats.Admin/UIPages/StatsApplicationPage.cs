using CMS.Membership;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.Base.UIPages;
using Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

[assembly: UIApplication(
    identifier: StatsApplicationPage.IDENTIFIER,
    type: typeof(StatsApplicationPage),
    slug: "simple-stats",
    name: "Simple Stats (Labs)",
    category: BaseApplicationCategories.DIGITAL_MARKETING,
    icon: Icons.Graph,
    templateName: TemplateNames.SECTION_LAYOUT)]

namespace Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

/// <summary>
/// "Simple Stats (Labs)" admin application. Reports are grouped into <see cref="StatsSectionPage"/> sections.
/// Assign the View permission to roles that may open the application,
/// plus one <see cref="StatsPermissions"/> permission per report the role may see, and optionally <see cref="StatsPermissions.EXPORT"/>.
/// </summary>
[UIPermission(SystemPermissions.VIEW)]
[UIPermission(StatsPermissions.ACTIVITY_COUNTS, StatsPermissions.ACTIVITY_COUNTS_DISPLAY_NAME)]
[UIPermission(StatsPermissions.TOP_PAGES, StatsPermissions.TOP_PAGES_DISPLAY_NAME)]
[UIPermission(StatsPermissions.NEW_CONTACTS, StatsPermissions.NEW_CONTACTS_DISPLAY_NAME)]
[UIPermission(StatsPermissions.FORM_SUBMISSIONS, StatsPermissions.FORM_SUBMISSIONS_DISPLAY_NAME)]
[UIPermission(StatsPermissions.CONTENT_INVENTORY, StatsPermissions.CONTENT_INVENTORY_DISPLAY_NAME)]
[UIPermission(StatsPermissions.PUBLISHING_CALENDAR, StatsPermissions.PUBLISHING_CALENDAR_DISPLAY_NAME)]
[UIPermission(StatsPermissions.CONTENT_LOCKS, StatsPermissions.CONTENT_LOCKS_DISPLAY_NAME)]
[UIPermission(StatsPermissions.PAGE_FRESHNESS, StatsPermissions.PAGE_FRESHNESS_DISPLAY_NAME)]
[UIPermission(StatsPermissions.REUSABLE_USAGE, StatsPermissions.REUSABLE_USAGE_DISPLAY_NAME)]
[UIPermission(StatsPermissions.EVENT_LOG, StatsPermissions.EVENT_LOG_DISPLAY_NAME)]
[UIPermission(StatsPermissions.ORDERS_REVENUE, StatsPermissions.ORDERS_REVENUE_DISPLAY_NAME)]
[UIPermission(StatsPermissions.CUSTOMERS, StatsPermissions.CUSTOMERS_DISPLAY_NAME)]
[UIPermission(StatsPermissions.MEMBERS, StatsPermissions.MEMBERS_DISPLAY_NAME)]
[UIPermission(StatsPermissions.CONSENTS, StatsPermissions.CONSENTS_DISPLAY_NAME)]
[UIPermission(StatsPermissions.EMAIL_SUMMARY, StatsPermissions.EMAIL_SUMMARY_DISPLAY_NAME)]
[UIPermission(StatsPermissions.RECIPIENT_LISTS, StatsPermissions.RECIPIENT_LISTS_DISPLAY_NAME)]
[UIPermission(StatsPermissions.WEB_PAGE_STATS, StatsPermissions.WEB_PAGE_STATS_DISPLAY_NAME)]
[UIPermission(StatsPermissions.EXPORT, StatsPermissions.EXPORT_DISPLAY_NAME)]
public sealed class StatsApplicationPage(IUIPermissionEvaluator permissionEvaluator) : ApplicationPage
{
    public const string IDENTIFIER = "Kentico.Xperience.Labs.SimpleStats.Application";

    private readonly IUIPermissionEvaluator permissionEvaluator = permissionEvaluator;

    private IReadOnlySet<string> deniedSlugs = new HashSet<string>();

    public override async Task ConfigurePage()
    {
        await base.ConfigurePage();

        // Runs before the product builds routes and navigation, which are synchronous.
        deniedSlugs = await StatsNavigation.GetDeniedChildSlugs(
            GetType(),
            async permission => (await permissionEvaluator.Evaluate(permission)).Succeeded);
    }

    public override Task<TemplateClientProperties> ConfigureTemplateProperties(TemplateClientProperties properties)
    {
        properties.Navigation.Items = StatsNavigation.FilterNavigation(properties.Navigation.Items, deniedSlugs);

        return base.ConfigureTemplateProperties(properties);
    }

    /// <summary>
    /// Opens the first section with a report the user may open. The section then opens that report.
    /// </summary>
    protected override Route GetDefaultRoute(IEnumerable<Route> routes) =>
        StatsNavigation.GetDefaultRoute(routes, deniedSlugs)!;
}
