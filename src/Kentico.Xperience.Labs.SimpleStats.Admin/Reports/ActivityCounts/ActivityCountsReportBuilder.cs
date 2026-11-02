using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ActivityCounts;

/// <summary>
/// Turns daily SQL aggregates into a period-bucketed, zero-filled report.
/// </summary>
internal static class ActivityCountsReportBuilder
{
    /// <summary>
    /// Builds the report.
    /// </summary>
    /// <param name="query">Normalized filter.</param>
    /// <param name="dailyCounts">
    /// Daily counts from the start of the previous period (see <see cref="StatsComparison.GetPreviousRange"/>) to the end of the range.
    /// The series use only the range; rows before it are the previous period of <see cref="ActivityCountsResult.TotalComparison"/>.
    /// </param>
    /// <param name="displayNames">Activity type display names by code name.</param>
    public static ActivityCountsResult Build(
        StatsQuery query,
        IEnumerable<ActivityDailyCount> dailyCounts,
        IReadOnlyDictionary<string, string> displayNames)
    {
        var rows = dailyCounts.ToList();
        var (previousFrom, _) = StatsComparison.GetPreviousRange(query);
        int previousTotal = rows
            .Where(row => row.Date >= previousFrom && row.Date < query.From && row.Count > 0)
            .Sum(row => row.Count);

        var timeSeries = StatsTimeSeriesBuilder.BuildByTotal(
            query,
            rows.Select(row => new StatsDailyCount(row.ActivityType ?? string.Empty, row.Date, row.Count)),
            activityType => GetDisplayName(activityType, displayNames));

        // Same JSON shape as before the shared builder existed (series use "activityType").
        return new(
            timeSeries.From,
            timeSeries.To,
            timeSeries.Grouping,
            timeSeries.ChannelId,
            timeSeries.Periods,
            [.. timeSeries.Series.Select(s => new ActivityCountsSeries(s.Key, s.DisplayName, s.Values, s.Total))],
            timeSeries.Total,
            StatsComparison.Create(query, timeSeries.Total, previousTotal));
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
