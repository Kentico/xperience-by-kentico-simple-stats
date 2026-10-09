using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PageFreshness;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;
using Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

[assembly: UIPage(
    uiPageType: typeof(PageFreshnessPage),
    parentType: typeof(StatsContentSection),
    slug: "page-freshness",
    name: "Page freshness",
    templateName: PageFreshnessPage.TEMPLATE_NAME,
    order: 530,
    Icon = Icons.Clock)]

namespace Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

/// <summary>
/// Published pages by time since their last change and their page visits in a date range:
/// stale pages people still visit, and published pages nobody visits.
/// </summary>
[UIEvaluatePermission(StatsPermissions.PAGE_FRESHNESS)]
public sealed class PageFreshnessPage(
    IPageFreshnessService pageFreshnessService,
    IStatsChannelOptionsProvider channelOptionsProvider,
    IPageLinkGenerator pageLinkGenerator,
    TimeProvider clock,
    IUIPermissionEvaluator permissionEvaluator,
    IStatsExportEventPublisher exportEventPublisher) : StatsReportPage<PageFreshnessClientProperties>(permissionEvaluator, exportEventPublisher)
{
    public const string TEMPLATE_NAME = "@kentico/xperience-admin-labs-simple-stats/PageFreshness";

    private readonly IPageFreshnessService pageFreshnessService = pageFreshnessService;
    private readonly IStatsChannelOptionsProvider channelOptionsProvider = channelOptionsProvider;
    private readonly IPageLinkGenerator pageLinkGenerator = pageLinkGenerator;
    private readonly TimeProvider clock = clock;

    protected override async Task<PageFreshnessClientProperties> ConfigureReportProperties(PageFreshnessClientProperties properties)
    {
        var channels = await channelOptionsProvider.GetChannelOptions(PageFreshnessReportBuilder.ChannelTypes, CancellationToken.None);
        var query = PageFreshnessReportBuilder.Normalize(null, GetToday(), channels);

        properties.Report = await pageFreshnessService.GetReport(query, refresh: false, CancellationToken.None);
        properties.Channels = channels;
        properties.Today = GetToday();
        properties.PagePath = pageLinkGenerator.GetPath<PageFreshnessPage>();

        return properties;
    }

    [PageCommand(CommandName = "LOAD", Permission = StatsPermissions.PAGE_FRESHNESS)]
    public async Task<ICommandResponse<PageFreshnessResult>> Load(StatsLoadRequest request, CancellationToken cancellationToken)
    {
        // A channel that is not a website channel is dropped; grouping is not used.
        var channels = await channelOptionsProvider.GetChannelOptions(PageFreshnessReportBuilder.ChannelTypes, cancellationToken);
        var query = PageFreshnessReportBuilder.Normalize(request?.Filter, GetToday(), channels);
        var report = await pageFreshnessService.GetReport(query, request?.Refresh ?? false, cancellationToken);

        return ResponseFrom(report);
    }

    // Same rule as the other range reports: ActivityCreated is compared as stored, so "today" uses the server time zone.
    private DateOnly GetToday() => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
}

public sealed class PageFreshnessClientProperties : StatsReportClientProperties
{
    public PageFreshnessResult? Report { get; set; }

    /// <summary>
    /// Website channels (pages are in website channels only).
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
