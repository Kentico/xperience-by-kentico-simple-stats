using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContentLocks;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;
using Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

[assembly: UIPage(
    uiPageType: typeof(ContentLocksPage),
    parentType: typeof(StatsContentSection),
    slug: "content-locks",
    name: "Content locks",
    templateName: ContentLocksPage.TEMPLATE_NAME,
    order: 520,
    Icon = Icons.Lock)]

namespace Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

/// <summary>
/// Language variants locked for editing (content locking): who holds which items, and for how long.
/// </summary>
[UIEvaluatePermission(StatsPermissions.CONTENT_LOCKS)]
public sealed class ContentLocksPage(
    IContentLocksService contentLocksService,
    IStatsChannelOptionsProvider channelOptionsProvider,
    IPageLinkGenerator pageLinkGenerator,
    IUIPermissionEvaluator permissionEvaluator,
    IStatsExportEventPublisher exportEventPublisher) : StatsReportPage<ContentLocksClientProperties>(permissionEvaluator, exportEventPublisher)
{
    public const string TEMPLATE_NAME = "@kentico/xperience-admin-labs-simple-stats/ContentLocks";

    private readonly IContentLocksService contentLocksService = contentLocksService;
    private readonly IStatsChannelOptionsProvider channelOptionsProvider = channelOptionsProvider;
    private readonly IPageLinkGenerator pageLinkGenerator = pageLinkGenerator;

    protected override async Task<ContentLocksClientProperties> ConfigureReportProperties(ContentLocksClientProperties properties)
    {
        var channels = await channelOptionsProvider.GetChannelOptions(StatsContentKinds.ChannelTypes, CancellationToken.None);
        var query = StatsContentKinds.Normalize(null, channels);

        properties.Report = await contentLocksService.GetReport(query, refresh: false, CancellationToken.None);
        properties.Channels = channels;
        properties.PagePath = pageLinkGenerator.GetPath<ContentLocksPage>();

        return properties;
    }

    [PageCommand(CommandName = "LOAD", Permission = StatsPermissions.CONTENT_LOCKS)]
    public async Task<ICommandResponse<ContentLocksResult>> Load(StatsSnapshotLoadRequest request, CancellationToken cancellationToken)
    {
        // Kind and channel as in the content inventory: a channel that does not fit the kind is dropped.
        var channels = await channelOptionsProvider.GetChannelOptions(StatsContentKinds.ChannelTypes, cancellationToken);
        var query = StatsContentKinds.Normalize(request?.Filter, channels);
        var report = await contentLocksService.GetReport(query, request?.Refresh ?? false, cancellationToken);

        return ResponseFrom(report);
    }
}

public sealed class ContentLocksClientProperties : StatsReportClientProperties
{
    public ContentLocksResult? Report { get; set; }

    /// <summary>
    /// Website, email and headless channels. The client shows the ones that match the selected kind.
    /// </summary>
    public IReadOnlyList<StatsChannelOption> Channels { get; set; } = [];

    /// <summary>
    /// Path of this page relative to the admin root. The client uses it to turn admin paths into links.
    /// </summary>
    public string? PagePath { get; set; }
}
