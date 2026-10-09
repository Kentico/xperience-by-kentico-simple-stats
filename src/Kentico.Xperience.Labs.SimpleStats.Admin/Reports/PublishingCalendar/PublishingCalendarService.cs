using CMS.Helpers;

using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PublishingCalendar;

/// <summary>
/// Builds the publishing calendar report.
/// </summary>
public interface IPublishingCalendarService
{
    /// <summary>
    /// Returns the report for the query.
    /// </summary>
    /// <param name="query">Normalized filter (see <see cref="PublishingCalendarReportBuilder.Normalize"/>).</param>
    /// <param name="refresh">When <c>true</c>, cached data is dropped and read again from the database (then cached again).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<PublishingCalendarResult> GetReport(PublishingCalendarQuery query, bool refresh, CancellationToken cancellationToken);
}

internal sealed class PublishingCalendarService(
    IPublishingCalendarRepository repository,
    IProgressiveCache cache,
    IStatsCacheInvalidator cacheInvalidator,
    IStatsAdminLinks adminLinks,
    TimeProvider clock) : IPublishingCalendarService
{
    private readonly IPublishingCalendarRepository repository = repository;
    private readonly IProgressiveCache cache = cache;
    private readonly IStatsCacheInvalidator cacheInvalidator = cacheInvalidator;
    private readonly IStatsAdminLinks adminLinks = adminLinks;
    private readonly TimeProvider clock = clock;

    public async Task<PublishingCalendarResult> GetReport(PublishingCalendarQuery query, bool refresh, CancellationToken cancellationToken)
    {
        // Current state: the key is the normalized kind, channel and window. The window and the recent limit
        // are counted from the read time, so they are as old as the cached data.
        var settings = StatsCache.CreateSettings(
            "publishing-calendar",
            query.Filter.Kind ?? "all",
            query.Filter.ChannelId ?? 0,
            query.Window);

        var snapshot = await cache.LoadAsync(
            cacheInvalidator,
            settings,
            refresh,
            async token => new PublishingCalendarSnapshot(
                await repository.GetData(query, clock.GetLocalNow().DateTime, token),
                clock.GetUtcNow()),
            cancellationToken);

        var result = PublishingCalendarReportBuilder.Build(query, snapshot.Data, link => StatsContentItemPaths.GetPath(adminLinks, link));

        return result with
        {
            Days = result.Days with { UpdatedAt = snapshot.ReadAt },
            UpdatedAt = snapshot.ReadAt,
        };
    }
}
