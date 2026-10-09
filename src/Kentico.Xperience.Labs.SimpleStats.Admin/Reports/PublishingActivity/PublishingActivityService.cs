using CMS.Core;
using CMS.Helpers;

using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PublishingActivity;

/// <summary>
/// Builds the publishing activity report.
/// </summary>
public interface IPublishingActivityService
{
    /// <summary>
    /// Returns the report for the query.
    /// </summary>
    /// <param name="query">Normalized filter (see <see cref="PublishingActivityFilter.Normalize"/>).</param>
    /// <param name="refresh">When <c>true</c>, cached data is dropped and read again from the database (then cached again).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<PublishingActivityResult> GetReport(PublishingActivityQuery query, bool refresh, CancellationToken cancellationToken);
}

internal sealed class PublishingActivityService(
    IPublishingActivityRepository repository,
    ISettingsService settingsService,
    IProgressiveCache cache,
    IStatsCacheInvalidator cacheInvalidator,
    IStatsAdminLinks adminLinks,
    TimeProvider clock) : IPublishingActivityService
{
    /// <inheritdoc cref="StatsContentVersionHistory.EnabledSettingsKey"/>
    public const string VersionHistoryEnabledSettingsKey = StatsContentVersionHistory.EnabledSettingsKey;

    /// <inheritdoc cref="StatsContentVersionHistory.LengthSettingsKey"/>
    public const string VersionHistoryLengthSettingsKey = StatsContentVersionHistory.LengthSettingsKey;

    private readonly IPublishingActivityRepository repository = repository;
    private readonly ISettingsService settingsService = settingsService;
    private readonly IProgressiveCache cache = cache;
    private readonly IStatsCacheInvalidator cacheInvalidator = cacheInvalidator;
    private readonly IStatsAdminLinks adminLinks = adminLinks;
    private readonly TimeProvider clock = clock;

    public async Task<PublishingActivityResult> GetReport(PublishingActivityQuery query, bool refresh, CancellationToken cancellationToken)
    {
        var range = query.Range;

        // Cache the daily aggregate (not the bucketed result) so switching grouping does not hit the database.
        var settings = StatsCache.CreateSettings(
            "publishing-activity",
            range.From.DayNumber,
            range.To.DayNumber,
            query.Kind ?? "all",
            range.ChannelId ?? 0,
            PublishingActivityReportBuilder.ListLimit);

        var snapshot = await cache.LoadAsync(
            cacheInvalidator,
            settings,
            refresh,
            async token => new PublishingActivitySnapshot(await repository.GetData(query, token), clock.GetUtcNow()),
            cancellationToken);

        // The settings are read on every request (the settings service caches them), so the hint follows the settings right away.
        var result = PublishingActivityReportBuilder.Build(
            query,
            snapshot.Data,
            StatsContentVersionHistory.IsEnabled(settingsService),
            StatsContentVersionHistory.GetLength(settingsService),
            link => StatsContentItemPaths.GetPath(adminLinks, link),
            classId => StatsContentItemPaths.GetContentTypePath(adminLinks, classId));

        return result with
        {
            Series = result.Series with { UpdatedAt = snapshot.ReadAt },
            UpdatedAt = snapshot.ReadAt,
        };
    }
}
