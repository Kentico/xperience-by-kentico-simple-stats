using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.NewContacts;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;
using Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

[assembly: UIPage(
    uiPageType: typeof(NewContactsPage),
    parentType: typeof(StatsContactsSection),
    slug: "new-contacts",
    name: "New contacts",
    templateName: NewContactsPage.TEMPLATE_NAME,
    order: 300,
    Icon = Icons.UserFrame)]

namespace Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

/// <summary>
/// New contacts over time, identified vs anonymous.
/// </summary>
[UIEvaluatePermission(StatsPermissions.NEW_CONTACTS)]
public sealed class NewContactsPage(
    INewContactsService newContactsService,
    TimeProvider clock,
    IUIPermissionEvaluator permissionEvaluator,
    IStatsExportEventPublisher exportEventPublisher) : StatsReportPage<NewContactsClientProperties>(permissionEvaluator, exportEventPublisher)
{
    public const string TEMPLATE_NAME = "@kentico/xperience-labs-simple-stats-admin/NewContacts";

    private readonly INewContactsService newContactsService = newContactsService;
    private readonly TimeProvider clock = clock;

    protected override async Task<NewContactsClientProperties> ConfigureReportProperties(NewContactsClientProperties properties)
    {
        var query = new StatsFilter().Normalize(GetToday());

        properties.Report = await newContactsService.GetReport(query, refresh: false, CancellationToken.None);
        properties.Today = GetToday();

        return properties;
    }

    [PageCommand(CommandName = "LOAD", Permission = StatsPermissions.NEW_CONTACTS)]
    public async Task<ICommandResponse<NewContactsResult>> Load(StatsLoadRequest request, CancellationToken cancellationToken)
    {
        var query = (request?.Filter ?? new StatsFilter()).Normalize(GetToday());
        var report = await newContactsService.GetReport(query, request?.Refresh ?? false, cancellationToken);

        return ResponseFrom(report);
    }

    // ContactCreated is compared as stored (no time zone conversion), so "today" uses the server time zone.
    private DateOnly GetToday() => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
}

public sealed class NewContactsClientProperties : StatsReportClientProperties
{
    public NewContactsResult? Report { get; set; }

    /// <summary>
    /// Server date used for date presets.
    /// </summary>
    public DateOnly Today { get; set; }
}
