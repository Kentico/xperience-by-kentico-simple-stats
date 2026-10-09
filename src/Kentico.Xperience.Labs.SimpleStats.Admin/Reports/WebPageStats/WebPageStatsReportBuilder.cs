using CMS.Activities;

using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ActivityCounts;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.WebPageStats;

/// <summary>
/// Turns daily activity counts of one web page into a period-bucketed, zero-filled report.
/// </summary>
internal static class WebPageStatsReportBuilder
{
    /// <summary>
    /// Maximum number of UTM sources listed.
    /// </summary>
    public const int SourceLimit = 10;

    /// <summary>
    /// Maximum number of UTM source and content pairs listed.
    /// </summary>
    public const int SourceContentLimit = 25;

    public static WebPageStatsResult Build(
        StatsQuery query,
        WebPageStatsData data,
        IReadOnlyDictionary<string, string> displayNames,
        WebPageStatsTarget target,
        bool hasAnyUtmData)
    {
        var timeSeries = StatsTimeSeriesBuilder.BuildByTotal(
            query,
            data.DailyCounts,
            activityType => GetDisplayName(activityType, displayNames));

        int pageVisits = GetTotal(timeSeries, PredefinedActivityType.PAGE_VISIT);
        int formSubmissions = GetTotal(timeSeries, PredefinedActivityType.BIZFORM_SUBMIT);

        return new(
            timeSeries.From,
            timeSeries.To,
            timeSeries.Grouping,
            timeSeries.Periods,
            timeSeries.Series,
            timeSeries.Total,
            pageVisits,
            data.UniqueContacts,
            data.UniqueVisitors,
            formSubmissions,
            data.UniqueSubmitters,
            target.FormUrlPath,
            target.FormUrlHosts,
            target.UsesLanguageDomains,
            BuildCampaigns(query, data.Campaigns, hasAnyUtmData));
    }

    private static WebPageCampaignsResult BuildCampaigns(StatsQuery query, WebPageCampaignData data, bool hasAnyUtmData)
    {
        // The page belongs to one channel, so the ranked lists carry no channel filter.
        var pageQuery = query with { ChannelId = null };

        var bySource = StatsRankedBuilder.Build(
            pageQuery,
            data.Sources.Select(s => new StatsRankedEntry(s.Source, s.Source, null, s.Landings, s.Visitors, null)),
            data.CampaignLandings,
            data.SourceCount,
            SourceLimit);

        var bySourceContent = StatsRankedBuilder.Build(
            pageQuery,
            data.SourceContents.Select(c => new StatsRankedEntry(
                StatsUtm.GetPairKey(c.Source, c.Content),
                c.Source,
                c.Content ?? StatsUtm.NoValueLabel,
                c.Landings,
                null,
                null)),
            data.CampaignLandings,
            data.SourceContentCount,
            SourceContentLimit);

        return new(
            data.Landings,
            data.CampaignLandings,
            data.Landings > 0 ? (double)data.CampaignLandings / data.Landings : null,
            data.CampaignVisitors,
            bySource,
            bySourceContent,
            // Campaign landings on this page prove the site has UTM data.
            hasAnyUtmData || data.CampaignLandings > 0);
    }

    private static int GetTotal(StatsTimeSeriesResult timeSeries, string activityType) =>
        timeSeries.Series
            .Where(s => string.Equals(s.Key, activityType, StringComparison.OrdinalIgnoreCase))
            .Sum(s => s.Total);

    private static string GetDisplayName(string activityType, IReadOnlyDictionary<string, string> displayNames) =>
        ActivityCountsReportBuilder.GetDisplayName(activityType, displayNames);
}
