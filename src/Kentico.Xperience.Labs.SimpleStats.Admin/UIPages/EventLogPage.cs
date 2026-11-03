using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.EventLog;
using Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

[assembly: UIPage(
    uiPageType: typeof(EventLogPage),
    parentType: typeof(StatsSystemSection),
    slug: "event-log",
    name: "Event log",
    templateName: EventLogPage.TEMPLATE_NAME,
    order: 600,
    Icon = Icons.RectangleParagraph)]

namespace Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

/// <summary>
/// Event log entries by type over time, top sources, event codes and users.
/// </summary>
[UIEvaluatePermission(StatsPermissions.EVENT_LOG)]
public sealed class EventLogPage(
    IEventLogReportService eventLogReportService,
    IPageLinkGenerator pageLinkGenerator,
    TimeProvider clock,
    IUIPermissionEvaluator permissionEvaluator,
    IStatsExportEventPublisher exportEventPublisher) : StatsReportPage<EventLogClientProperties>(permissionEvaluator, exportEventPublisher)
{
    public const string TEMPLATE_NAME = "@kentico/xperience-labs-simple-stats-admin/EventLog";

    private readonly IEventLogReportService eventLogReportService = eventLogReportService;
    private readonly IPageLinkGenerator pageLinkGenerator = pageLinkGenerator;
    private readonly TimeProvider clock = clock;

    protected override async Task<EventLogClientProperties> ConfigureReportProperties(EventLogClientProperties properties)
    {
        var query = new EventLogFilter().Normalize(GetToday());

        properties.Report = await eventLogReportService.GetReport(query, refresh: false, CancellationToken.None);
        properties.Today = GetToday();
        properties.PagePath = pageLinkGenerator.GetPath<EventLogPage>();

        return properties;
    }

    [PageCommand(CommandName = "LOAD", Permission = StatsPermissions.EVENT_LOG)]
    public async Task<ICommandResponse<EventLogResult>> Load(EventLogLoadRequest request, CancellationToken cancellationToken)
    {
        var query = (request?.Filter ?? new EventLogFilter()).Normalize(GetToday());
        var report = await eventLogReportService.GetReport(query, request?.Refresh ?? false, cancellationToken);

        return ResponseFrom(report);
    }

    // EventTime is compared as stored (server local time), so "today" uses the server time zone.
    private DateOnly GetToday() => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
}

public sealed class EventLogClientProperties : StatsReportClientProperties
{
    public EventLogResult? Report { get; set; }

    /// <summary>
    /// Server date used for date presets.
    /// </summary>
    public DateOnly Today { get; set; }

    /// <summary>
    /// Path of this page relative to the admin root. The client uses it to turn admin paths into links.
    /// </summary>
    public string? PagePath { get; set; }
}
