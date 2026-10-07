using CMS.Helpers;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.Base.UIPages;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContentInventory;

/// <summary>
/// Builds the content inventory report.
/// </summary>
public interface IContentInventoryService
{
    /// <summary>
    /// Returns the report for the query.
    /// </summary>
    /// <param name="query">Normalized filter (see <see cref="StatsSnapshotFilter.Normalize"/>).</param>
    /// <param name="refresh">When <c>true</c>, cached data is dropped and read again from the database (then cached again).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<ContentInventoryResult> GetReport(StatsSnapshotQuery query, bool refresh, CancellationToken cancellationToken);
}

internal sealed class ContentInventoryService(
    IContentInventoryRepository repository,
    IProgressiveCache cache,
    IStatsCacheInvalidator cacheInvalidator,
    IStatsAdminLinks adminLinks,
    TimeProvider clock) : IContentInventoryService
{
    private readonly IContentInventoryRepository repository = repository;
    private readonly IProgressiveCache cache = cache;
    private readonly IStatsCacheInvalidator cacheInvalidator = cacheInvalidator;
    private readonly IStatsAdminLinks adminLinks = adminLinks;
    private readonly TimeProvider clock = clock;

    public async Task<ContentInventoryResult> GetReport(StatsSnapshotQuery query, bool refresh, CancellationToken cancellationToken)
    {
        // Current state only: the key is the normalized kind and channel, nothing time-based.
        // Ages (days since the last change) are counted to the read time, so they are as old as the cached data.
        var settings = StatsCache.CreateSettings(
            "content-inventory",
            query.Kind ?? "all",
            query.ChannelId ?? 0);

        var snapshot = await cache.LoadAsync(
            cacheInvalidator,
            settings,
            refresh,
            async token => new ContentInventorySnapshot(
                await repository.GetData(query, clock.GetLocalNow().DateTime, token),
                clock.GetUtcNow()),
            cancellationToken);

        var result = ContentInventoryReportBuilder.Build(query, snapshot.Data, GetContentTypePath, GetWorkflowPath, GetContentItemPath);

        return result with
        {
            ByKind = result.ByKind with { UpdatedAt = snapshot.ReadAt },
            ByContentType = result.ByContentType with { UpdatedAt = snapshot.ReadAt },
            ByStatus = result.ByStatus with { UpdatedAt = snapshot.ReadAt },
            Age = result.Age with { Buckets = result.Age.Buckets with { UpdatedAt = snapshot.ReadAt } },
            UnusedReusable = result.UnusedReusable is { } unused
                ? unused with { ByContentType = unused.ByContentType with { UpdatedAt = snapshot.ReadAt } }
                : null,
            UpdatedAt = snapshot.ReadAt,
        };
    }

    /// <inheritdoc cref="StatsContentItemPaths.GetContentTypePath"/>
    private string? GetContentTypePath(int classId) => StatsContentItemPaths.GetContentTypePath(adminLinks, classId);

    /// <summary>
    /// Path of the "Steps" tab of the workflow in the Workflows application.
    /// </summary>
    private string? GetWorkflowPath(int workflowId) =>
        adminLinks.GetPath<WorkflowSteps>(new PageParameterValues
        {
            { typeof(WorkflowEditSection), workflowId },
        });

    /// <inheritdoc cref="StatsContentItemPaths.GetPath"/>
    private string? GetContentItemPath(ContentItemLink link) => StatsContentItemPaths.GetPath(adminLinks, link);
}
