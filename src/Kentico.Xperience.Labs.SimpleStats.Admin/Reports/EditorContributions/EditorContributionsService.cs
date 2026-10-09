using CMS.Core;
using CMS.Helpers;

using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PublishingActivity;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.EditorContributions;

/// <summary>
/// Builds the editor contributions report.
/// </summary>
public interface IEditorContributionsService
{
    /// <summary>
    /// Returns the report for the query.
    /// </summary>
    /// <param name="query">Normalized filter (see <see cref="PublishingActivityFilter.Normalize"/>).</param>
    /// <param name="refresh">When <c>true</c>, cached data is dropped and read again from the database (then cached again).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<EditorContributionsResult> GetReport(PublishingActivityQuery query, bool refresh, CancellationToken cancellationToken);
}

internal sealed class EditorContributionsService(
    IEditorContributionsRepository repository,
    ISettingsService settingsService,
    IProgressiveCache cache,
    IStatsCacheInvalidator cacheInvalidator,
    IStatsAdminLinks adminLinks,
    TimeProvider clock) : IEditorContributionsService
{
    private readonly IEditorContributionsRepository repository = repository;
    private readonly ISettingsService settingsService = settingsService;
    private readonly IProgressiveCache cache = cache;
    private readonly IStatsCacheInvalidator cacheInvalidator = cacheInvalidator;
    private readonly IStatsAdminLinks adminLinks = adminLinks;
    private readonly TimeProvider clock = clock;

    public async Task<EditorContributionsResult> GetReport(PublishingActivityQuery query, bool refresh, CancellationToken cancellationToken)
    {
        var range = query.Range;

        // The settings are read on every request (the settings service caches them). Publishes are read only with version history,
        // so the setting is part of the cache key and the report follows it right away.
        bool historyEnabled = StatsContentVersionHistory.IsEnabled(settingsService);

        // Cache the daily aggregate (not the bucketed result) so switching grouping does not hit the database.
        var settings = StatsCache.CreateSettings(
            "editor-contributions",
            range.From.DayNumber,
            range.To.DayNumber,
            query.Kind ?? "all",
            range.ChannelId ?? 0,
            historyEnabled,
            EditorContributionsReportBuilder.UserLimit);

        var snapshot = await cache.LoadAsync(
            cacheInvalidator,
            settings,
            refresh,
            async token => new EditorContributionsSnapshot(await repository.GetData(query, historyEnabled, token), clock.GetUtcNow()),
            cancellationToken);

        var result = EditorContributionsReportBuilder.Build(
            query,
            snapshot.Data,
            historyEnabled,
            StatsContentVersionHistory.GetLength(settingsService),
            userId => StatsUserPaths.GetPath(adminLinks, userId));

        return result with
        {
            Series = result.Series with { UpdatedAt = snapshot.ReadAt },
            UpdatedAt = snapshot.ReadAt,
        };
    }
}
