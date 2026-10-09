using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ActivityCounts;

/// <summary>
/// Turns daily SQL aggregates into a period-bucketed, zero-filled report.
/// </summary>
internal static class ActivityCountsReportBuilder
{
    public static ActivityCountsResult Build(
        StatsQuery query,
        IEnumerable<ActivityDailyCount> dailyCounts,
        IReadOnlyDictionary<string, string> displayNames)
    {
        var timeSeries = StatsTimeSeriesBuilder.BuildByTotal(
            query,
            dailyCounts.Select(row => new StatsDailyCount(row.ActivityType ?? string.Empty, row.Date, row.Count)),
            activityType => GetDisplayName(activityType, displayNames));

        // Same JSON shape as before the shared builder existed (series use "activityType").
        return new(
            timeSeries.From,
            timeSeries.To,
            timeSeries.Grouping,
            timeSeries.ChannelId,
            timeSeries.Periods,
            [.. timeSeries.Series.Select(s => new ActivityCountsSeries(s.Key, s.DisplayName, s.Values, s.Total))],
            timeSeries.Total);
    }

    /// <summary>
    /// Display name of an activity type from <c>OM_ActivityType</c>, else the code name, else "(no type)". Shared by activity reports.
    /// </summary>
    internal static string GetDisplayName(string activityType, IReadOnlyDictionary<string, string> displayNames)
    {
        if (displayNames.TryGetValue(activityType, out string? name) && !string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        return string.IsNullOrWhiteSpace(activityType) ? "(no type)" : activityType;
    }
}
