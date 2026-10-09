using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContentInventory;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;
using Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

[assembly: UIPage(
    uiPageType: typeof(ContentInventoryPage),
    parentType: typeof(StatsContentSection),
    slug: "content-inventory",
    name: "Content inventory",
    templateName: ContentInventoryPage.TEMPLATE_NAME,
    order: 500,
    Icon = Icons.Boxes)]

namespace Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

/// <summary>
/// Current state of content items by content type, status and language.
/// </summary>
[UIEvaluatePermission(StatsPermissions.CONTENT_INVENTORY)]
public sealed class ContentInventoryPage(
    IContentInventoryService contentInventoryService,
    IStatsChannelOptionsProvider channelOptionsProvider,
    IPageLinkGenerator pageLinkGenerator,
    IUIPermissionEvaluator permissionEvaluator,
    IStatsExportEventPublisher exportEventPublisher) : StatsReportPage<ContentInventoryClientProperties>(permissionEvaluator, exportEventPublisher)
{
    public const string TEMPLATE_NAME = "@kentico/xperience-admin-labs-simple-stats/ContentInventory";

    private readonly IContentInventoryService contentInventoryService = contentInventoryService;
    private readonly IStatsChannelOptionsProvider channelOptionsProvider = channelOptionsProvider;
    private readonly IPageLinkGenerator pageLinkGenerator = pageLinkGenerator;

    protected override async Task<ContentInventoryClientProperties> ConfigureReportProperties(ContentInventoryClientProperties properties)
    {
        var query = new StatsSnapshotFilter().Normalize(StatsContentKinds.Kinds);

        properties.Report = await contentInventoryService.GetReport(query, refresh: false, CancellationToken.None);
        properties.Channels = await channelOptionsProvider.GetChannelOptions(StatsContentKinds.ChannelTypes, CancellationToken.None);
        properties.PagePath = pageLinkGenerator.GetPath<ContentInventoryPage>();

        return properties;
    }

    [PageCommand(CommandName = "LOAD", Permission = StatsPermissions.CONTENT_INVENTORY)]
    public async Task<ICommandResponse<ContentInventoryResult>> Load(StatsSnapshotLoadRequest request, CancellationToken cancellationToken)
    {
        // A channel that does not fit the kind (for example an email channel with pages) is dropped.
        var channels = await channelOptionsProvider.GetChannelOptions(StatsContentKinds.ChannelTypes, cancellationToken);
        var query = StatsContentKinds.Normalize(request?.Filter, channels);
        var report = await contentInventoryService.GetReport(query, request?.Refresh ?? false, cancellationToken);

        return ResponseFrom(report);
    }
}

public sealed class ContentInventoryClientProperties : StatsReportClientProperties
{
    public ContentInventoryResult? Report { get; set; }

    /// <summary>
    /// Website, email and headless channels. The client shows the ones that match the selected kind.
    /// </summary>
    public IReadOnlyList<StatsChannelOption> Channels { get; set; } = [];

    /// <summary>
    /// Path of this page relative to the admin root. The client uses it to turn admin paths into links.
    /// </summary>
    public string? PagePath { get; set; }
}
