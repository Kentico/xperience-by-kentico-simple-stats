using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PublishingCalendar;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;
using Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

[assembly: UIPage(
    uiPageType: typeof(PublishingCalendarPage),
    parentType: typeof(StatsContentSection),
    slug: "publishing-calendar",
    name: "Publishing calendar",
    templateName: PublishingCalendarPage.TEMPLATE_NAME,
    order: 510,
    Icon = Icons.Calendar)]

namespace Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

/// <summary>
/// Scheduled publishes, unpublishes and email sends (upcoming) and recently published content.
/// </summary>
[UIEvaluatePermission(StatsPermissions.PUBLISHING_CALENDAR)]
public sealed class PublishingCalendarPage(
    IPublishingCalendarService publishingCalendarService,
    IStatsChannelOptionsProvider channelOptionsProvider,
    IPageLinkGenerator pageLinkGenerator,
    IUIPermissionEvaluator permissionEvaluator,
    IStatsExportEventPublisher exportEventPublisher) : StatsReportPage<PublishingCalendarClientProperties>(permissionEvaluator, exportEventPublisher)
{
    public const string TEMPLATE_NAME = "@kentico/xperience-admin-labs-simple-stats/PublishingCalendar";

    private readonly IPublishingCalendarService publishingCalendarService = publishingCalendarService;
    private readonly IStatsChannelOptionsProvider channelOptionsProvider = channelOptionsProvider;
    private readonly IPageLinkGenerator pageLinkGenerator = pageLinkGenerator;

    protected override async Task<PublishingCalendarClientProperties> ConfigureReportProperties(PublishingCalendarClientProperties properties)
    {
        var channels = await channelOptionsProvider.GetChannelOptions(PublishingCalendarReportBuilder.ChannelTypes, CancellationToken.None);
        var query = PublishingCalendarReportBuilder.Normalize(null, channels);

        properties.Report = await publishingCalendarService.GetReport(query, refresh: false, CancellationToken.None);
        properties.Channels = channels;
        properties.Windows = PublishingCalendarReportBuilder.Windows;
        properties.PagePath = pageLinkGenerator.GetPath<PublishingCalendarPage>();

        return properties;
    }

    [PageCommand(CommandName = "LOAD", Permission = StatsPermissions.PUBLISHING_CALENDAR)]
    public async Task<ICommandResponse<PublishingCalendarResult>> Load(StatsSnapshotLoadRequest request, CancellationToken cancellationToken)
    {
        // A channel that does not fit the kind (for example an email channel with pages) is dropped, a headless kind is all kinds;
        // an unknown window falls back to the default.
        var channels = await channelOptionsProvider.GetChannelOptions(PublishingCalendarReportBuilder.ChannelTypes, cancellationToken);
        var query = PublishingCalendarReportBuilder.Normalize(request?.Filter, channels);
        var report = await publishingCalendarService.GetReport(query, request?.Refresh ?? false, cancellationToken);

        return ResponseFrom(report);
    }
}

public sealed class PublishingCalendarClientProperties : StatsReportClientProperties
{
    public PublishingCalendarResult? Report { get; set; }

    /// <summary>
    /// Website and email channels (headless items cannot be scheduled). The client shows the ones that match the selected kind.
    /// </summary>
    public IReadOnlyList<StatsChannelOption> Channels { get; set; } = [];

    /// <summary>
    /// Windows (days ahead) the client can pick.
    /// </summary>
    public IReadOnlyList<int> Windows { get; set; } = [];

    /// <summary>
    /// Path of this page relative to the admin root. The client uses it to turn admin paths into links.
    /// </summary>
    public string? PagePath { get; set; }
}
