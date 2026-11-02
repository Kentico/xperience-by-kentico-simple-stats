using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ActivityCounts;

/// <summary>
/// Activity counts per type per period.
/// </summary>
/// <param name="From">Applied range start (inclusive).</param>
/// <param name="To">Applied range end (inclusive).</param>
/// <param name="Grouping">Applied grouping.</param>
/// <param name="ChannelId">Applied channel filter.</param>
/// <param name="Periods">Continuous period axis.</param>
/// <param name="Series">One series per activity type, sorted by total (descending). Values align with <paramref name="Periods"/>.</param>
/// <param name="Total">Sum of all activities in the range.</param>
/// <param name="TotalComparison"><paramref name="Total"/> compared with the previous period of the same length (same channel filter).</param>
public sealed record ActivityCountsResult(
    DateOnly From,
    DateOnly To,
    StatsGrouping Grouping,
    int? ChannelId,
    IReadOnlyList<StatsPeriod> Periods,
    IReadOnlyList<ActivityCountsSeries> Series,
    int Total,
    StatsComparison TotalComparison)
{
    /// <summary>
    /// When the counts were read from the database. Can be older than the request when served from cache.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// Counts for one activity type.
/// </summary>
/// <param name="ActivityType">Activity type code name.</param>
/// <param name="DisplayName">Activity type display name.</param>
/// <param name="Values">Count per period (zero-filled).</param>
/// <param name="Total">Sum of <paramref name="Values"/>.</param>
public sealed record ActivityCountsSeries(string ActivityType, string DisplayName, IReadOnlyList<int> Values, int Total);

/// <summary>
/// Count of activities of one type on one day, as returned by the SQL aggregate.
/// </summary>
public sealed record ActivityDailyCount(string ActivityType, DateOnly Date, int Count);

/// <summary>
/// Daily counts for one range and channel, with the time they were read. This is the cached value.
/// </summary>
internal sealed record ActivityDailyCountsSnapshot(IReadOnlyList<ActivityDailyCount> Rows, DateTimeOffset ReadAt);
