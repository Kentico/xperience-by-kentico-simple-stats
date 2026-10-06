using CMS.Helpers;

using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ActivityCounts;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.WebPageStats;

/// <summary>
/// Builds the contact activity report of one web page.
/// </summary>
public interface IWebPageStatsService
{
    /// <summary>
    /// Returns the report for the page and query. The channel filter is ignored (the page belongs to one channel).
    /// </summary>
    /// <param name="target">Web page and language variant.</param>
    /// <param name="query">Normalized filter.</param>
    /// <param name="refresh">When <c>true</c>, cached data is dropped and read again from the database (then cached again).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<WebPageStatsResult> GetReport(WebPageStatsTarget target, StatsQuery query, bool refresh, CancellationToken cancellationToken);
}

internal sealed class WebPageStatsService(
    IWebPageStatsRepository repository,
    IActivityCountsRepository activityCountsRepository,
    IProgressiveCache cache,
    IStatsCacheInvalidator cacheInvalidator,
    TimeProvider clock) : IWebPageStatsService
{
    private readonly IWebPageStatsRepository repository = repository;
    private readonly IActivityCountsRepository activityCountsRepository = activityCountsRepository;
    private readonly IProgressiveCache cache = cache;
    private readonly IStatsCacheInvalidator cacheInvalidator = cacheInvalidator;
    private readonly TimeProvider clock = clock;

    public async Task<WebPageStatsResult> GetReport(WebPageStatsTarget target, StatsQuery query, bool refresh, CancellationToken cancellationToken)
    {
        // Grouping is not part of the key: the daily aggregate is cached, so switching grouping does not hit the database.
        var dataSettings = StatsCache.CreateSettings(
            "web-page-stats",
            target.WebPageItemGuid,
            target.LanguageId,
            target.ChannelId,
            // The URL changes when the page is moved or renamed; the old cached item then just expires.
            target.FormUrlPath ?? "(none)",
            // Domains come from configuration and can change on restart.
            string.Join('|', target.FormUrlHosts),
            query.From.DayNumber,
            query.To.DayNumber);

        // Same item as the activity counts report.
        var displayNamesSettings = StatsCache.CreateSettings("activity-type-names");

        var snapshot = await cache.LoadAsync(
            cacheInvalidator,
            dataSettings,
            refresh,
            async token => new WebPageStatsSnapshot(
                await repository.GetData(target, query.From, query.To, token),
                clock.GetUtcNow()),
            cancellationToken);

        var displayNames = await cache.LoadAsync(
            cacheInvalidator,
            displayNamesSettings,
            refresh,
            activityCountsRepository.GetActivityTypeDisplayNames,
            cancellationToken);

        return WebPageStatsReportBuilder.Build(query, snapshot.Data, displayNames, target) with { UpdatedAt = snapshot.ReadAt };
    }
}
