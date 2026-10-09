using System.Data;

using CMS.DataEngine;
using CMS.Helpers;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

/// <summary>
/// UTM values stored on landing page activities (<c>ActivityUTMSource</c>, <c>ActivityUTMContent</c>), shared by the web page
/// "Stats (Labs)" tab and the Campaign sources report. Xperience does not fill these columns; only sites with UTM capture code
/// (see "UTM capture" in the usage guide) have values.
/// </summary>
internal static class StatsUtm
{
    /// <summary>
    /// Label of an empty UTM content (or source).
    /// </summary>
    public const string NoValueLabel = "(none)";

    /// <summary>
    /// Trimmed UTM source of activity alias <c>A</c>, <c>NULL</c> when empty. A landing with a source is a campaign landing.
    /// </summary>
    public const string SourceColumn = "NULLIF(LTRIM(RTRIM(A.[ActivityUTMSource])), N'')";

    /// <summary>
    /// Trimmed UTM content of activity alias <c>A</c>, <c>NULL</c> when empty.
    /// </summary>
    public const string ContentColumn = "NULLIF(LTRIM(RTRIM(A.[ActivityUTMContent])), N'')";

    /// <summary>
    /// Selects one row (<c>HasAnyUtmData</c>): whether any activity has a UTM source.
    /// Uses the product index that covers <c>ActivityUTMSource</c>. Empty and whitespace-only values do not count
    /// (trailing spaces are ignored in comparisons).
    /// </summary>
    public const string HasAnyUtmDataQuery = """
        SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM [OM_Activity] A WHERE A.[ActivityUTMSource] <> N'') THEN 1 ELSE 0 END AS bit) AS [HasAnyUtmData];
        """;

    /// <summary>
    /// Stable key of a source and content pair. The unit separator cannot appear in a value typed in a URL, so the key is unique per pair.
    /// </summary>
    /// <param name="source">Trimmed source.</param>
    /// <param name="content">Trimmed content, or <c>null</c> when empty.</param>
    public static string GetPairKey(string source, string? content) => source + '\u001f' + (content ?? string.Empty);

    /// <summary>
    /// Returns whether the site has any UTM values. One site-wide cache item, so the check runs at most once per cache expiry.
    /// Only needed to explain an empty campaign view: "no UTM capture on this site" vs "no campaign landings here".
    /// </summary>
    public static Task<bool> HasAnyUtmData(
        IProgressiveCache cache,
        IStatsCacheInvalidator cacheInvalidator,
        IStatsUtmDataRepository repository,
        bool refresh,
        CancellationToken cancellationToken) =>
        cache.LoadAsync(
            cacheInvalidator,
            StatsCache.CreateSettings("utm-data-exists"),
            refresh,
            repository.HasAnyUtmData,
            cancellationToken);
}

/// <summary>
/// Checks whether the site stores UTM values at all.
/// </summary>
internal interface IStatsUtmDataRepository
{
    /// <summary>
    /// Returns <c>true</c> when any activity in the database has a UTM source, so the site captures UTM values.
    /// </summary>
    public Task<bool> HasAnyUtmData(CancellationToken cancellationToken);
}

internal sealed class StatsUtmDataRepository : IStatsUtmDataRepository
{
    public async Task<bool> HasAnyUtmData(CancellationToken cancellationToken)
    {
        await using var reader = await ConnectionHelper.ExecuteReaderAsync(StatsUtm.HasAnyUtmDataQuery, new QueryDataParameters(), QueryTypeEnum.SQLQuery, CommandBehavior.Default, cancellationToken);

        return await reader.ReadAsync(cancellationToken) && reader.GetBoolean(0);
    }
}
