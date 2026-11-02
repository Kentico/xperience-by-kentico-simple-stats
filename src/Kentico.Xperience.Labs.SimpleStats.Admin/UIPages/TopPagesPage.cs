using CMS.ContentEngine;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.TopPages;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;
using Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

[assembly: UIPage(
    uiPageType: typeof(TopPagesPage),
    parentType: typeof(StatsContactsSection),
    slug: "top-pages",
    name: "Top pages",
    templateName: TopPagesPage.TEMPLATE_NAME,
    order: 200,
    Icon = Icons.ListNumbers)]

namespace Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

/// <summary>
/// Most visited pages in a date range.
/// </summary>
[UIEvaluatePermission(StatsPermissions.TOP_PAGES)]
public sealed class TopPagesPage(
    ITopPagesService topPagesService,
    IStatsChannelOptionsProvider channelOptionsProvider,
    TimeProvider clock,
    IUIPermissionEvaluator permissionEvaluator,
    IStatsExportEventPublisher exportEventPublisher) : StatsReportPage<TopPagesClientProperties>(permissionEvaluator, exportEventPublisher)
{
    public const string TEMPLATE_NAME = "@kentico/xperience-labs-simple-stats-admin/TopPages";

    private readonly ITopPagesService topPagesService = topPagesService;
    private readonly IStatsChannelOptionsProvider channelOptionsProvider = channelOptionsProvider;
    private readonly TimeProvider clock = clock;

    protected override async Task<TopPagesClientProperties> ConfigureReportProperties(TopPagesClientProperties properties)
    {
        var query = new StatsFilter().Normalize(GetToday());
        var channels = await channelOptionsProvider.GetChannelOptions(CancellationToken.None);

        properties.Report = await topPagesService.GetReport(query, refresh: false, CancellationToken.None);
        // Page visits are logged for website channels only.
        properties.Channels = [.. channels.Where(c => c.Type == nameof(ChannelType.Website))];
        properties.Today = GetToday();

        return properties;
    }

    [PageCommand(CommandName = "LOAD", Permission = StatsPermissions.TOP_PAGES)]
    public async Task<ICommandResponse<StatsRankedResult>> Load(StatsLoadRequest request, CancellationToken cancellationToken)
    {
        var query = (request?.Filter ?? new StatsFilter()).Normalize(GetToday());
        var report = await topPagesService.GetReport(query, request?.Refresh ?? false, cancellationToken);

        return ResponseFrom(report);
    }

    // Same rule as the activity counts report: ActivityCreated is compared as stored, so "today" uses the server time zone.
    private DateOnly GetToday() => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
}

public sealed class TopPagesClientProperties : StatsReportClientProperties
{
    public StatsRankedResult? Report { get; set; }

    public IReadOnlyList<StatsChannelOption> Channels { get; set; } = [];

    /// <summary>
    /// Server date used for date presets.
    /// </summary>
    public DateOnly Today { get; set; }
}
