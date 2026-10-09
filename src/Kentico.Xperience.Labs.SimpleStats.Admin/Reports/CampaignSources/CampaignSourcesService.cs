using System.Globalization;

using CMS.Helpers;

using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.CampaignSources;

/// <summary>
/// Builds the campaign sources report.
/// </summary>
public interface ICampaignSourcesService
{
    /// <summary>
    /// Returns the report for the query.
    /// </summary>
    /// <param name="query">
    /// Normalized filter (see <see cref="CampaignSourcesFilter.Normalize"/>). A source without campaign landings in the range (not in the
    /// source options) or a content not in the source's content options means all; the result has the applied values.
    /// </param>
    /// <param name="refresh">When <c>true</c>, cached data is dropped and read again from the database (then cached again).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<CampaignSourcesResult> GetReport(CampaignSourcesQuery query, bool refresh, CancellationToken cancellationToken);
}

internal sealed class CampaignSourcesService(
    ICampaignSourcesRepository repository,
    IStatsUtmDataRepository utmDataRepository,
    IProgressiveCache cache,
    IStatsCacheInvalidator cacheInvalidator,
    TimeProvider clock) : ICampaignSourcesService
{
    private readonly ICampaignSourcesRepository repository = repository;
    private readonly IStatsUtmDataRepository utmDataRepository = utmDataRepository;
    private readonly IProgressiveCache cache = cache;
    private readonly IStatsCacheInvalidator cacheInvalidator = cacheInvalidator;
    private readonly TimeProvider clock = clock;

    public async Task<CampaignSourcesResult> GetReport(CampaignSourcesQuery query, bool refresh, CancellationToken cancellationToken)
    {
        // Source and content are free text, so only values the data knows are used (as tag usage does with taxonomies):
        // the unfiltered data has the sources, the source's data has its contents (both usually cached). An unknown value is
        // dropped (all), so the cache key only has values that exist in the range and a request cannot add entries at will.
        var snapshot = await Load(query with { Source = null, Content = null }, refresh, cancellationToken);
        string? source = query.Source is null
            ? null
            : snapshot.Data.Sources.FirstOrDefault(s => string.Equals(s.Source, query.Source, StringComparison.OrdinalIgnoreCase))?.Source;
        string? content = null;
        if (source is not null)
        {
            snapshot = await Load(query with { Source = source, Content = null }, refresh, cancellationToken);
            content = GetKnownContent(query.Content, snapshot.Data.Contents);
            if (content is not null)
            {
                snapshot = await Load(query with { Source = source, Content = content }, refresh, cancellationToken);
            }
        }

        query = query with { Source = source, Content = content };

        // Only needed to explain an empty report: campaign landings in the range prove the site has UTM data.
        bool hasAnyUtmData = snapshot.Data.Totals.AllCampaignLandings > 0
            || await StatsUtm.HasAnyUtmData(cache, cacheInvalidator, utmDataRepository, refresh, cancellationToken);

        var result = CampaignSourcesReportBuilder.Build(query, snapshot.Data, hasAnyUtmData);

        return result with
        {
            Series = result.Series with { UpdatedAt = snapshot.ReadAt },
            BySource = result.BySource with { UpdatedAt = snapshot.ReadAt },
            Pages = result.Pages with { UpdatedAt = snapshot.ReadAt },
            BySourceContent = result.BySourceContent with { UpdatedAt = snapshot.ReadAt },
            UpdatedAt = snapshot.ReadAt,
        };
    }

    // Cache the daily aggregate (not the bucketed result) so switching grouping does not hit the database.
    // Source and content are length-prefixed, so no value can make two filters share a key.
    private Task<CampaignSourcesSnapshot> Load(CampaignSourcesQuery query, bool refresh, CancellationToken cancellationToken)
    {
        var range = query.Range;
        var settings = StatsCache.CreateSettings(
            "campaign-sources",
            range.From.DayNumber,
            range.To.DayNumber,
            range.ChannelId ?? 0,
            KeyPart(query.Source),
            KeyPart(query.Content));

        return cache.LoadAsync(
            cacheInvalidator,
            settings,
            refresh,
            async token => new CampaignSourcesSnapshot(await repository.GetData(query, token), clock.GetUtcNow()),
            cancellationToken);
    }

    /// <summary>
    /// The stored content of the source that matches <paramref name="content"/> (case-insensitive), or <c>null</c> (all) when unknown.
    /// An empty string (landings without a content) is always kept.
    /// </summary>
    private static string? GetKnownContent(string? content, IReadOnlyList<CampaignSourcesContentRow> contents) =>
        content switch
        {
            null => null,
            "" => string.Empty,
            _ => contents.FirstOrDefault(c => string.Equals(c.Content, content, StringComparison.OrdinalIgnoreCase))?.Content,
        };

    private static string KeyPart(string? value) =>
        value is null ? "all" : string.Create(CultureInfo.InvariantCulture, $"{value.Length}:{value}");
}
