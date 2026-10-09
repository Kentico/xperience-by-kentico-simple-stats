using CMS.Helpers;

using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.TranslationStatus;

/// <summary>
/// Builds the translation status report.
/// </summary>
public interface ITranslationStatusService
{
    /// <summary>
    /// Returns the report for the query and language.
    /// </summary>
    /// <param name="query">Normalized kind and channel filter (see <see cref="StatsContentKinds.Normalize"/>).</param>
    /// <param name="languageId">Language sent by the client, or <c>null</c> for all. An ID that is not a non-default language means all.</param>
    /// <param name="refresh">When <c>true</c>, cached data is dropped and read again from the database (then cached again).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<TranslationStatusResult> GetReport(StatsSnapshotQuery query, int? languageId, bool refresh, CancellationToken cancellationToken);
}

internal sealed class TranslationStatusService(
    ITranslationStatusRepository repository,
    IProgressiveCache cache,
    IStatsCacheInvalidator cacheInvalidator,
    IStatsAdminLinks adminLinks,
    TimeProvider clock) : ITranslationStatusService
{
    private readonly ITranslationStatusRepository repository = repository;
    private readonly IProgressiveCache cache = cache;
    private readonly IStatsCacheInvalidator cacheInvalidator = cacheInvalidator;
    private readonly IStatsAdminLinks adminLinks = adminLinks;
    private readonly TimeProvider clock = clock;

    public async Task<TranslationStatusResult> GetReport(StatsSnapshotQuery query, int? languageId, bool refresh, CancellationToken cancellationToken)
    {
        // Current state: the key is the normalized kind and channel. The data has all languages, so the language filter
        // is applied by the builder and does not split the cache.
        var snapshot = await cache.LoadAsync(
            cacheInvalidator,
            StatsCache.CreateSettings("translation-status", query.Kind ?? "all", query.ChannelId ?? 0),
            refresh,
            async token => new TranslationStatusSnapshot(await repository.GetData(query, token), clock.GetUtcNow()),
            cancellationToken);

        var result = TranslationStatusReportBuilder.Build(
            query,
            languageId,
            snapshot.Data,
            link => StatsContentItemPaths.GetPath(adminLinks, link),
            classId => StatsContentItemPaths.GetContentTypePath(adminLinks, classId));

        return result with
        {
            ByContentType = result.ByContentType with { UpdatedAt = snapshot.ReadAt },
            UpdatedAt = snapshot.ReadAt,
        };
    }
}
