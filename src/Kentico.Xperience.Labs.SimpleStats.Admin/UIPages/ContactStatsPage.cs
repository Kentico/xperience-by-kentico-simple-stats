using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.DigitalMarketing.UIPages;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContactStats;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;
using Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

[assembly: UIPage(
    uiPageType: typeof(ContactStatsPage),
    parentType: typeof(ContactEditSection),
    slug: ContactStatsPage.SLUG,
    name: ContactStatsPage.NAME,
    templateName: ContactStatsPage.TEMPLATE_NAME,
    order: ContactStatsPage.ORDER,
    Icon = Icons.Graph)]

[assembly: PageExtender(typeof(ContactStatsNavigationExtender))]

namespace Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

/// <summary>
/// "Stats (Labs)" tab of a contact in Contact management: activity trend, when active, pages, forms, emails, campaign sources,
/// content interests and insights of that one contact. Requires <see cref="StatsPermissions.CONTACT_STATS"/> in the
/// "Simple Stats (Labs)" application, on top of the Contact management permissions that already guard <see cref="ContactEditSection"/>.
/// </summary>
internal sealed class ContactStatsPage(
    IContactStatsService contactStatsService,
    IStatsApplicationPermissionEvaluator statsPermissionEvaluator,
    IStatsExportEventPublisher exportEventPublisher,
    TimeProvider clock) : Page<ContactStatsClientProperties>
{
    public const string SLUG = "simple-stats";

    /// <summary>
    /// Tab name. "(Labs)" tells users the tab comes from this Labs extension, not the product.
    /// </summary>
    public const string NAME = "Stats (Labs)";

    public const string TEMPLATE_NAME = "@kentico/xperience-admin-labs-simple-stats/ContactStats";

    /// <summary>
    /// After the product tabs on <see cref="ContactEditSection"/> (Overview 100, Activities 200, Automation 300).
    /// </summary>
    public const int ORDER = 1001;

    private readonly IContactStatsService contactStatsService = contactStatsService;
    private readonly IStatsApplicationPermissionEvaluator statsPermissionEvaluator = statsPermissionEvaluator;
    private readonly IStatsExportEventPublisher exportEventPublisher = exportEventPublisher;
    private readonly TimeProvider clock = clock;

    /// <summary>
    /// Contact ID from the parent's URL segment, as the product's contact tabs (<c>ContactActivityList.ObjectId</c>) read it.
    /// </summary>
    [PageParameter(typeof(IntPageModelBinder), typeof(ContactEditSection))]
    public int ContactId { get; set; }

    public override async Task<PageValidationResult> ValidatePage()
    {
        // Before the contact lookup, so users without the permission can't probe contact IDs or fill the cache.
        await statsPermissionEvaluator.EnsureGranted(StatsPermissions.CONTACT_STATS);

        return new()
        {
            IsValid = await contactStatsService.ContactExists(ContactId, CancellationToken.None),
            ErrorMessageKey = "base.forms.error.objectnotinitialized",
        };
    }

    public override async Task<ContactStatsClientProperties> ConfigureTemplateProperties(ContactStatsClientProperties properties)
    {
        await statsPermissionEvaluator.EnsureGranted(StatsPermissions.CONTACT_STATS);

        // UI guard only: the client builds the CSV from data already on the page.
        properties.CanExport = await statsPermissionEvaluator.IsGranted(StatsPermissions.EXPORT);
        properties.Report = await contactStatsService.GetReport(ContactId, null, GetToday(), refresh: false, CancellationToken.None);
        properties.Today = GetToday();

        return properties;
    }

    [PageCommand(CommandName = "LOAD")]
    public async Task<ICommandResponse<ContactStatsResult>> Load(ContactStatsLoadRequest request, CancellationToken cancellationToken)
    {
        await statsPermissionEvaluator.EnsureGranted(StatsPermissions.CONTACT_STATS);

        var report = await contactStatsService.GetReport(ContactId, request?.Filter, GetToday(), request?.Refresh ?? false, cancellationToken);

        return ResponseFrom(report);
    }

    /// <summary>
    /// "When active" heatmap. Separate from <see cref="Load"/>: the client runs it only when the user asks for it.
    /// </summary>
    [PageCommand(CommandName = "LOAD_HEATMAP")]
    public async Task<ICommandResponse<ContactHeatmapResult>> LoadHeatmap(ContactStatsLoadRequest request, CancellationToken cancellationToken)
    {
        await statsPermissionEvaluator.EnsureGranted(StatsPermissions.CONTACT_STATS);

        var heatmap = await contactStatsService.GetHeatmap(ContactId, request?.Filter, GetToday(), request?.Refresh ?? false, cancellationToken);

        return ResponseFrom(heatmap);
    }

    /// <summary>
    /// Raises the <see cref="AfterExportStatsEvent"/> after the client downloaded a CSV, like <see cref="StatsReportPage{TClientProperties}.LogExport"/>.
    /// </summary>
    [PageCommand(CommandName = "LOG_EXPORT")]
    public async Task<ICommandResponse> LogExport(StatsExportLogRequest request, CancellationToken cancellationToken)
    {
        await statsPermissionEvaluator.EnsureGranted(StatsPermissions.CONTACT_STATS);
        await statsPermissionEvaluator.EnsureGranted(StatsPermissions.EXPORT);

        await exportEventPublisher.Publish(GetType(), request, cancellationToken);

        return Response();
    }

    // ActivityCreated is compared as stored (server local time), so "today" uses the server time zone.
    private DateOnly GetToday() => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
}

internal sealed class ContactStatsClientProperties : StatsReportClientProperties
{
    public ContactStatsResult? Report { get; set; }

    /// <summary>
    /// Server date used for date presets.
    /// </summary>
    public DateOnly Today { get; set; }
}

/// <summary>
/// Hides the contact "Stats (Labs)" tab for users without <see cref="StatsPermissions.CONTACT_STATS"/>.
/// Only the navigation entry is removed; the page itself checks the permission too.
/// </summary>
internal sealed class ContactStatsNavigationExtender(IStatsApplicationPermissionEvaluator statsPermissionEvaluator) : PageExtender<ContactEditSection>
{
    private readonly IStatsApplicationPermissionEvaluator statsPermissionEvaluator = statsPermissionEvaluator;

    public override async Task<TemplateClientProperties> ConfigureTemplateProperties(TemplateClientProperties properties)
    {
        properties = await base.ConfigureTemplateProperties(properties);

        if (!await statsPermissionEvaluator.IsGranted(StatsPermissions.CONTACT_STATS))
        {
            properties.Navigation.Items = [.. properties.Navigation.Items
                .Where(item => !string.Equals(item.Path, ContactStatsPage.SLUG, StringComparison.OrdinalIgnoreCase))];
        }

        return properties;
    }
}
