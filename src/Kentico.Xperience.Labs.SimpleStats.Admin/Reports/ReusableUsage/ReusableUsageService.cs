using CMS.Helpers;

using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ReusableUsage;

/// <summary>
/// Builds the reusable content usage report.
/// </summary>
public interface IReusableUsageService
{
    /// <summary>
    /// Returns the report for the content type filter.
    /// </summary>
    /// <param name="contentTypeId">
    /// Content type (class ID) sent by the client, or <c>null</c> for all. An ID that is not a reusable content type with items means all.
    /// </param>
    /// <param name="refresh">When <c>true</c>, cached data is dropped and read again from the database (then cached again).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<ReusableUsageResult> GetReport(int? contentTypeId, bool refresh, CancellationToken cancellationToken);
}

internal sealed class ReusableUsageService(
    IReusableUsageRepository repository,
    IProgressiveCache cache,
    IStatsCacheInvalidator cacheInvalidator,
    IStatsAdminLinks adminLinks,
    TimeProvider clock) : IReusableUsageService
{
    private readonly IReusableUsageRepository repository = repository;
    private readonly IProgressiveCache cache = cache;
    private readonly IStatsCacheInvalidator cacheInvalidator = cacheInvalidator;
    private readonly IStatsAdminLinks adminLinks = adminLinks;
    private readonly TimeProvider clock = clock;

    public async Task<ReusableUsageResult> GetReport(int? contentTypeId, bool refresh, CancellationToken cancellationToken)
    {
        // The data of all content types is the source of the content type options, so it is read first (usually from the cache).
        // An unknown content type falls back to it, so the cache key only has content types that exist.
        var all = await Load(null, refresh, cancellationToken);
        var options = ReusableUsageReportBuilder.GetContentTypeOptions(all.Data.ContentTypes);
        int? query = ReusableUsageReportBuilder.NormalizeContentType(contentTypeId, options);
        var snapshot = query is null ? all : await Load(query, refresh, cancellationToken);

        var result = ReusableUsageReportBuilder.Build(
            query,
            snapshot.Data,
            options,
            link => StatsContentItemPaths.GetPath(adminLinks, link),
            classId => StatsContentItemPaths.GetContentTypePath(adminLinks, classId));

        return result with
        {
            Distribution = result.Distribution with { UpdatedAt = snapshot.ReadAt },
            ByContentType = result.ByContentType with { UpdatedAt = snapshot.ReadAt },
            UpdatedAt = snapshot.ReadAt,
        };
    }

    // Current state only: the key is the normalized content type, nothing time-based.
    private Task<ReusableUsageSnapshot> Load(int? contentTypeId, bool refresh, CancellationToken cancellationToken) =>
        cache.LoadAsync(
            cacheInvalidator,
            StatsCache.CreateSettings("reusable-usage", contentTypeId ?? 0),
            refresh,
            async token => new ReusableUsageSnapshot(await repository.GetData(contentTypeId, token), clock.GetUtcNow()),
            cancellationToken);
}
