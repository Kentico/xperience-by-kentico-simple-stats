using CMS.Core;
using CMS.Helpers;

using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContentLocks;

/// <summary>
/// Builds the content locks report.
/// </summary>
public interface IContentLocksService
{
    /// <summary>
    /// Returns the report for the query.
    /// </summary>
    /// <param name="query">Normalized kind and channel filter (see <see cref="StatsContentKinds.Normalize"/>).</param>
    /// <param name="refresh">When <c>true</c>, cached data is dropped and read again from the database (then cached again).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<ContentLocksResult> GetReport(StatsSnapshotQuery query, bool refresh, CancellationToken cancellationToken);
}

internal sealed class ContentLocksService(
    IContentLocksRepository repository,
    ISettingsService settingsService,
    IProgressiveCache cache,
    IStatsCacheInvalidator cacheInvalidator,
    IStatsAdminLinks adminLinks,
    TimeProvider clock) : IContentLocksService
{
    /// <summary>
    /// Settings key of the "Content locking" setting (Settings → Content).
    /// </summary>
    public const string LockingSettingsKey = "CMSEnableContentLocking";

    private readonly IContentLocksRepository repository = repository;
    private readonly ISettingsService settingsService = settingsService;
    private readonly IProgressiveCache cache = cache;
    private readonly IStatsCacheInvalidator cacheInvalidator = cacheInvalidator;
    private readonly IStatsAdminLinks adminLinks = adminLinks;
    private readonly TimeProvider clock = clock;

    public async Task<ContentLocksResult> GetReport(StatsSnapshotQuery query, bool refresh, CancellationToken cancellationToken)
    {
        // Current state: the key is the normalized kind and channel. Lock ages are counted from the read time,
        // so they are as old as the cached data.
        var settings = StatsCache.CreateSettings("content-locks", query.Kind ?? "all", query.ChannelId ?? 0);

        var snapshot = await cache.LoadAsync(
            cacheInvalidator,
            settings,
            refresh,
            async token => new ContentLocksSnapshot(
                await repository.GetData(query, clock.GetLocalNow().DateTime, token),
                clock.GetUtcNow()),
            cancellationToken);

        // The setting is read on every request (the settings service caches it), so the hint follows the setting right away.
        var result = ContentLocksReportBuilder.Build(
            query,
            snapshot.Data,
            IsLockingEnabled(),
            link => StatsContentItemPaths.GetPath(adminLinks, link),
            userId => StatsUserPaths.GetPath(adminLinks, userId));

        return result with
        {
            ByUser = result.ByUser with { UpdatedAt = snapshot.ReadAt },
            UpdatedAt = snapshot.ReadAt,
        };
    }

    /// <summary>
    /// <c>true</c> when the setting is "True" (any casing); <c>false</c> when it is not set or has another value.
    /// </summary>
    private bool IsLockingEnabled() => bool.TryParse(settingsService[LockingSettingsKey], out bool enabled) && enabled;
}
