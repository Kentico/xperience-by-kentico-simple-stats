using CMS.Helpers;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.Base.UIPages;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.TagUsage;

/// <summary>
/// Builds the tag usage report.
/// </summary>
public interface ITagUsageService
{
    /// <summary>
    /// Returns the report for the content type type and taxonomy.
    /// </summary>
    /// <param name="kind">Normalized content type type (see <see cref="StatsContentKinds.Kinds"/>), or <c>null</c> for all.</param>
    /// <param name="taxonomyId">Taxonomy sent by the client, or <c>null</c> for all. An unknown ID means all.</param>
    /// <param name="refresh">When <c>true</c>, cached data is dropped and read again from the database (then cached again).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<TagUsageResult> GetReport(string? kind, int? taxonomyId, bool refresh, CancellationToken cancellationToken);
}

internal sealed class TagUsageService(
    ITagUsageRepository repository,
    ITagUsageFieldProvider fieldProvider,
    IProgressiveCache cache,
    IStatsCacheInvalidator cacheInvalidator,
    IStatsAdminLinks adminLinks,
    TimeProvider clock) : ITagUsageService
{
    private readonly ITagUsageRepository repository = repository;
    private readonly ITagUsageFieldProvider fieldProvider = fieldProvider;
    private readonly IProgressiveCache cache = cache;
    private readonly IStatsCacheInvalidator cacheInvalidator = cacheInvalidator;
    private readonly IStatsAdminLinks adminLinks = adminLinks;
    private readonly TimeProvider clock = clock;

    public async Task<TagUsageResult> GetReport(string? kind, int? taxonomyId, bool refresh, CancellationToken cancellationToken)
    {
        // The data of all taxonomies is the source of the taxonomy options, so it is read first (usually from the cache).
        // An unknown taxonomy falls back to it, so the cache key only has taxonomies that exist.
        var all = await Load(kind, null, refresh, cancellationToken);
        var options = TagUsageReportBuilder.GetTaxonomyOptions(all.Data.Taxonomies);
        int? query = TagUsageReportBuilder.NormalizeTaxonomy(taxonomyId, options);
        var snapshot = query is null ? all : await Load(kind, query, refresh, cancellationToken);

        // Taxonomy fields change only with content types, so they are cached on their own (same expiry and refresh).
        var fields = await cache.LoadAsync(
            cacheInvalidator,
            StatsCache.CreateSettings("tag-usage-fields"),
            refresh,
            fieldProvider.GetFields,
            cancellationToken);

        var result = TagUsageReportBuilder.Build(kind, query, snapshot.Data, fields, options, GetTagPath);

        return result with
        {
            TopTags = result.TopTags with { UpdatedAt = snapshot.ReadAt },
            UpdatedAt = snapshot.ReadAt,
        };
    }

    // Current state only: the key is the normalized kind and taxonomy, nothing time-based.
    private Task<TagUsageSnapshot> Load(string? kind, int? taxonomyId, bool refresh, CancellationToken cancellationToken) =>
        cache.LoadAsync(
            cacheInvalidator,
            StatsCache.CreateSettings("tag-usage", kind ?? "all", taxonomyId ?? 0),
            refresh,
            async token => new TagUsageSnapshot(await repository.GetData(kind, taxonomyId, token), clock.GetUtcNow()),
            cancellationToken);

    // The tag's edit page in the Taxonomies application.
    private string? GetTagPath(int taxonomyId, int tagId) =>
        adminLinks.GetPath<TagEdit>(new PageParameterValues
        {
            { typeof(TaxonomyEditSection), taxonomyId },
            { typeof(TagEditLayout), tagId },
        });
}
