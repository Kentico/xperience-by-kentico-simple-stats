using CMS.Helpers;

using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ActivityCounts;

/// <summary>
/// Builds the activity counts report.
/// </summary>
public interface IActivityCountsService
{
    /// <summary>
    /// Returns the report for the query.
    /// </summary>
    /// <param name="query">Normalized filter.</param>
    /// <param name="refresh">When <c>true</c>, cached data is dropped and read again from the database (then cached again).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<ActivityCountsResult> GetReport(StatsQuery query, bool refresh, CancellationToken cancellationToken);
}

internal sealed class ActivityCountsService(
    IActivityCountsRepository repository,
    IProgressiveCache cache,
    IStatsCacheInvalidator cacheInvalidator,
    TimeProvider clock) : IActivityCountsService
{
    /// <summary>
    /// Cache expiry. Only this expiry or an explicit refresh drops cached data; data changes do not.
    /// </summary>
    internal const double CacheMinutes = StatsCache.CacheMinutes;

    private readonly IActivityCountsRepository repository = repository;
    private readonly IProgressiveCache cache = cache;
    private readonly IStatsCacheInvalidator cacheInvalidator = cacheInvalidator;
    private readonly TimeProvider clock = clock;

    public async Task<ActivityCountsResult> GetReport(StatsQuery query, bool refresh, CancellationToken cancellationToken)
    {
        // One query reads the previous period and the range; the builder splits the rows by date.
        var (previousFrom, _) = StatsComparison.GetPreviousRange(query);

        // Cache the daily aggregate (not the bucketed result) so switching grouping does not hit the database.
        // The previous period is derived from the range, so the range alone is the key.
        var dailyCountsSettings = StatsCache.CreateSettings(
            "activity-counts",
            query.From.DayNumber,
            query.To.DayNumber,
            query.ChannelId ?? 0);

        var displayNamesSettings = StatsCache.CreateSettings("activity-type-names");

        var dailyCounts = await cache.LoadAsync(
            cacheInvalidator,
            dailyCountsSettings,
            refresh,
            async token => new ActivityDailyCountsSnapshot(
                await repository.GetDailyCounts(previousFrom, query.To, query.ChannelId, token),
                clock.GetUtcNow()),
            cancellationToken);

        var displayNames = await cache.LoadAsync(
            cacheInvalidator,
            displayNamesSettings,
            refresh,
            repository.GetActivityTypeDisplayNames,
            cancellationToken);

        return ActivityCountsReportBuilder.Build(query, dailyCounts.Rows, displayNames) with { UpdatedAt = dailyCounts.ReadAt };
    }
}
