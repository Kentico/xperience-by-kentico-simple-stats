using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.Consents;
using Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

[assembly: UIPage(
    uiPageType: typeof(ConsentsPage),
    parentType: typeof(StatsContactsSection),
    slug: "consents",
    name: "Consents",
    templateName: ConsentsPage.TEMPLATE_NAME,
    order: 600,
    Icon = Icons.DocUser)]

namespace Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

/// <summary>
/// Consent agreements and revocations over time, agreed contacts, consents compared and consent text versions.
/// </summary>
[UIEvaluatePermission(StatsPermissions.CONSENTS)]
public sealed class ConsentsPage(
    IConsentsService consentsService,
    IPageLinkGenerator pageLinkGenerator,
    TimeProvider clock,
    IUIPermissionEvaluator permissionEvaluator,
    IStatsExportEventPublisher exportEventPublisher) : StatsReportPage<ConsentsClientProperties>(permissionEvaluator, exportEventPublisher)
{
    public const string TEMPLATE_NAME = "@kentico/xperience-labs-simple-stats-admin/Consents";

    private readonly IConsentsService consentsService = consentsService;
    private readonly IPageLinkGenerator pageLinkGenerator = pageLinkGenerator;
    private readonly TimeProvider clock = clock;

    protected override async Task<ConsentsClientProperties> ConfigureReportProperties(ConsentsClientProperties properties)
    {
        var query = new ConsentsFilter().Normalize(GetToday());

        properties.Report = await consentsService.GetReport(query, refresh: false, CancellationToken.None);
        properties.Today = GetToday();
        properties.PagePath = pageLinkGenerator.GetPath<ConsentsPage>();

        return properties;
    }

    [PageCommand(CommandName = "LOAD", Permission = StatsPermissions.CONSENTS)]
    public async Task<ICommandResponse<ConsentsResult>> Load(ConsentsLoadRequest request, CancellationToken cancellationToken)
    {
        var query = (request?.Filter ?? new ConsentsFilter()).Normalize(GetToday());
        var report = await consentsService.GetReport(query, request?.Refresh ?? false, cancellationToken);

        return ResponseFrom(report);
    }

    // ConsentAgreementTime is compared as stored (server local time), so "today" uses the server time zone.
    private DateOnly GetToday() => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
}

public sealed class ConsentsClientProperties : StatsReportClientProperties
{
    public ConsentsResult? Report { get; set; }

    /// <summary>
    /// Server date used for date presets.
    /// </summary>
    public DateOnly Today { get; set; }

    /// <summary>
    /// Path of this page relative to the admin root. The client uses it to turn admin paths into links.
    /// </summary>
    public string? PagePath { get; set; }
}
