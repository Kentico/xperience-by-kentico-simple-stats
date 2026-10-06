using CMS.Activities;

using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.WebPageStats;

/// <summary>
/// Turns daily activity counts of one web page into a period-bucketed, zero-filled report.
/// </summary>
internal static class WebPageStatsReportBuilder
{
    public static WebPageStatsResult Build(
        StatsQuery query,
        WebPageStatsData data,
        IReadOnlyDictionary<string, string> displayNames,
        WebPageStatsTarget target)
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
            target.UsesLanguageDomains);
    }

    private static int GetTotal(StatsTimeSeriesResult timeSeries, string activityType) =>
        timeSeries.Series
            .Where(s => string.Equals(s.Key, activityType, StringComparison.OrdinalIgnoreCase))
            .Sum(s => s.Total);

    private static string GetDisplayName(string activityType, IReadOnlyDictionary<string, string> displayNames)
    {
        if (displayNames.TryGetValue(activityType, out string? name) && !string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        return string.IsNullOrWhiteSpace(activityType) ? "(no type)" : activityType;
    }
}
