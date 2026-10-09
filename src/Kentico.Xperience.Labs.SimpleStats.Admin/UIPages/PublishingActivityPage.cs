using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PublishingActivity;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;
using Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

[assembly: UIPage(
    uiPageType: typeof(PublishingActivityPage),
    parentType: typeof(StatsContentSection),
    slug: "publishing-activity",
    name: "Publishing activity",
    templateName: PublishingActivityPage.TEMPLATE_NAME,
    order: 550,
    Icon = Icons.ArrowSend)]

namespace Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

/// <summary>
/// Content created, first published and updated over time, and how long content takes from created to first published.
/// </summary>
[UIEvaluatePermission(StatsPermissions.PUBLISHING_ACTIVITY)]
public sealed class PublishingActivityPage(
    IPublishingActivityService publishingActivityService,
    IStatsChannelOptionsProvider channelOptionsProvider,
    IPageLinkGenerator pageLinkGenerator,
    TimeProvider clock,
    IUIPermissionEvaluator permissionEvaluator,
    IStatsExportEventPublisher exportEventPublisher) : StatsReportPage<PublishingActivityClientProperties>(permissionEvaluator, exportEventPublisher)
{
    public const string TEMPLATE_NAME = "@kentico/xperience-admin-labs-simple-stats/PublishingActivity";

    private readonly IPublishingActivityService publishingActivityService = publishingActivityService;
    private readonly IStatsChannelOptionsProvider channelOptionsProvider = channelOptionsProvider;
    private readonly IPageLinkGenerator pageLinkGenerator = pageLinkGenerator;
    private readonly TimeProvider clock = clock;

    protected override async Task<PublishingActivityClientProperties> ConfigureReportProperties(PublishingActivityClientProperties properties)
    {
        var channels = await channelOptionsProvider.GetChannelOptions(StatsContentKinds.ChannelTypes, CancellationToken.None);
        var query = new PublishingActivityFilter().Normalize(GetToday(), channels);

        properties.Report = await publishingActivityService.GetReport(query, refresh: false, CancellationToken.None);
        properties.Channels = channels;
        properties.Today = GetToday();
        properties.PagePath = pageLinkGenerator.GetPath<PublishingActivityPage>();

        return properties;
    }

    [PageCommand(CommandName = "LOAD", Permission = StatsPermissions.PUBLISHING_ACTIVITY)]
    public async Task<ICommandResponse<PublishingActivityResult>> Load(PublishingActivityLoadRequest request, CancellationToken cancellationToken)
    {
        // A channel that does not fit the kind (for example an email channel with pages) is dropped.
        var channels = await channelOptionsProvider.GetChannelOptions(StatsContentKinds.ChannelTypes, cancellationToken);
        var query = (request?.Filter ?? new PublishingActivityFilter()).Normalize(GetToday(), channels);
        var report = await publishingActivityService.GetReport(query, request?.Refresh ?? false, cancellationToken);

        return ResponseFrom(report);
    }

    // Created and publish times are compared as stored (server local time), so "today" uses the server time zone.
    private DateOnly GetToday() => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
}

public sealed class PublishingActivityClientProperties : StatsReportClientProperties
{
    public PublishingActivityResult? Report { get; set; }

    /// <summary>
    /// Website, email and headless channels. The client shows the ones that match the selected kind.
    /// </summary>
    public IReadOnlyList<StatsChannelOption> Channels { get; set; } = [];

    /// <summary>
    /// Server date used for date presets.
    /// </summary>
    public DateOnly Today { get; set; }

    /// <summary>
    /// Path of this page relative to the admin root. The client uses it to turn admin paths into links.
    /// </summary>
    public string? PagePath { get; set; }
}
