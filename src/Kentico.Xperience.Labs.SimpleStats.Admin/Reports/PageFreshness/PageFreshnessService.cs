using CMS.Helpers;

using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PageFreshness;

/// <summary>
/// Builds the page freshness report.
/// </summary>
public interface IPageFreshnessService
{
    /// <summary>
    /// Returns the report for the query.
    /// </summary>
    /// <param name="query">Normalized filter (see <see cref="PageFreshnessReportBuilder.Normalize"/>). Grouping is ignored.</param>
    /// <param name="refresh">When <c>true</c>, cached data is dropped and read again from the database (then cached again).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<PageFreshnessResult> GetReport(StatsQuery query, bool refresh, CancellationToken cancellationToken);
}

internal sealed class PageFreshnessService(
    IPageFreshnessRepository repository,
    IProgressiveCache cache,
    IStatsCacheInvalidator cacheInvalidator,
    TimeProvider clock) : IPageFreshnessService
{
    private readonly IPageFreshnessRepository repository = repository;
    private readonly IProgressiveCache cache = cache;
    private readonly IStatsCacheInvalidator cacheInvalidator = cacheInvalidator;
    private readonly TimeProvider clock = clock;

    public async Task<PageFreshnessResult> GetReport(StatsQuery query, bool refresh, CancellationToken cancellationToken)
    {
        // Range and channel only (grouping is not used). Page ages are counted to the read time, so they are as old as the cached data.
        var settings = StatsCache.CreateSettings(
            "page-freshness",
            query.From.DayNumber,
            query.To.DayNumber,
            query.ChannelId ?? 0,
            PageFreshnessReportBuilder.ListLimit);

        var snapshot = await cache.LoadAsync(
            cacheInvalidator,
            settings,
            refresh,
            async token => new PageFreshnessSnapshot(
                await repository.GetData(query, clock.GetLocalNow().DateTime, token),
                clock.GetUtcNow()),
            cancellationToken);

        var result = PageFreshnessReportBuilder.Build(query, snapshot.Data);

        return result with
        {
            AgeBuckets = result.AgeBuckets with { UpdatedAt = snapshot.ReadAt },
            UpdatedAt = snapshot.ReadAt,
        };
    }
}
