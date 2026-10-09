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
    /// <param name="query">Normalized filter (see <see cref="CampaignSourcesFilter.Normalize"/>).</param>
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
        var range = query.Range;

        // Cache the daily aggregate (not the bucketed result) so switching grouping does not hit the database.
        // Source and content are free text: they are length-prefixed, so no value can make two filters share a key.
        var settings = StatsCache.CreateSettings(
            "campaign-sources",
            range.From.DayNumber,
            range.To.DayNumber,
            range.ChannelId ?? 0,
            KeyPart(query.Source),
            KeyPart(query.Content));

        var snapshot = await cache.LoadAsync(
            cacheInvalidator,
            settings,
            refresh,
            async token => new CampaignSourcesSnapshot(await repository.GetData(query, token), clock.GetUtcNow()),
            cancellationToken);

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

    private static string KeyPart(string? value) =>
        value is null ? "all" : string.Create(CultureInfo.InvariantCulture, $"{value.Length}:{value}");
}
