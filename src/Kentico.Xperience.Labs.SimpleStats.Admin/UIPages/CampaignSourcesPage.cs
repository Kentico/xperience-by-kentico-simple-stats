using CMS.ContentEngine;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.CampaignSources;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;
using Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

[assembly: UIPage(
    uiPageType: typeof(CampaignSourcesPage),
    parentType: typeof(StatsContactsSection),
    slug: "campaign-sources",
    name: "Campaign sources",
    templateName: CampaignSourcesPage.TEMPLATE_NAME,
    order: 250,
    Icon = Icons.Campaign)]

namespace Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

/// <summary>
/// Landing page activities by the UTM source and content stored on them, and the pages campaigns bring visitors to.
/// UTM values exist only when the site captures them (see "UTM capture" in the usage guide).
/// </summary>
[UIEvaluatePermission(StatsPermissions.CAMPAIGN_SOURCES)]
public sealed class CampaignSourcesPage(
    ICampaignSourcesService campaignSourcesService,
    IStatsChannelOptionsProvider channelOptionsProvider,
    TimeProvider clock,
    IUIPermissionEvaluator permissionEvaluator,
    IStatsExportEventPublisher exportEventPublisher) : StatsReportPage<CampaignSourcesClientProperties>(permissionEvaluator, exportEventPublisher)
{
    public const string TEMPLATE_NAME = "@kentico/xperience-admin-labs-simple-stats/CampaignSources";

    /// <summary>
    /// Landing page activities are logged for website channels only.
    /// </summary>
    private static readonly ChannelType[] channelTypes = [ChannelType.Website];

    private readonly ICampaignSourcesService campaignSourcesService = campaignSourcesService;
    private readonly IStatsChannelOptionsProvider channelOptionsProvider = channelOptionsProvider;
    private readonly TimeProvider clock = clock;

    protected override async Task<CampaignSourcesClientProperties> ConfigureReportProperties(CampaignSourcesClientProperties properties)
    {
        var channels = await channelOptionsProvider.GetChannelOptions(channelTypes, CancellationToken.None);
        var query = new CampaignSourcesFilter().Normalize(GetToday(), channels);

        properties.Report = await campaignSourcesService.GetReport(query, refresh: false, CancellationToken.None);
        properties.Channels = channels;
        properties.Today = GetToday();

        return properties;
    }

    [PageCommand(CommandName = "LOAD", Permission = StatsPermissions.CAMPAIGN_SOURCES)]
    public async Task<ICommandResponse<CampaignSourcesResult>> Load(CampaignSourcesLoadRequest request, CancellationToken cancellationToken)
    {
        var channels = await channelOptionsProvider.GetChannelOptions(channelTypes, cancellationToken);
        var query = (request?.Filter ?? new CampaignSourcesFilter()).Normalize(GetToday(), channels);
        var report = await campaignSourcesService.GetReport(query, request?.Refresh ?? false, cancellationToken);

        return ResponseFrom(report);
    }

    // ActivityCreated is compared as stored (server local time), so "today" uses the server time zone.
    private DateOnly GetToday() => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
}

public sealed class CampaignSourcesClientProperties : StatsReportClientProperties
{
    public CampaignSourcesResult? Report { get; set; }

    /// <summary>
    /// Website channels.
    /// </summary>
    public IReadOnlyList<StatsChannelOption> Channels { get; set; } = [];

    /// <summary>
    /// Server date used for date presets.
    /// </summary>
    public DateOnly Today { get; set; }
}
