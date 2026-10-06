using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.WebPageStats;

/// <summary>
/// Web page and language variant the report is for.
/// </summary>
/// <param name="WebPageItemGuid">GUID of the web page item. Activities link to pages by this GUID.</param>
/// <param name="LanguageId">Content language of the variant.</param>
/// <param name="ChannelId">Channel (<c>CMS_Channel</c>) of the page's website channel. Form submissions are matched in this channel only.</param>
/// <param name="FormUrlPath">
/// Live URL path of the variant normalized by <see cref="WebPageStatsUrlPath.Normalize"/> (for example <c>/articles/coffee</c>, or an empty string for the home page),
/// or <c>null</c> when the variant has no live URL. Form submission activities have no web page link, so they are matched by this path.
/// </param>
/// <param name="FormUrlHosts">
/// Hosts (with port, lowercase) form submissions must be logged on, normalized by <see cref="WebPageStatsUrlPath.NormalizeHost"/>.
/// Set for channels with language-specific domains, where all languages share the same paths: the language's domain and its aliases.
/// Empty means any host (language prefix channels, where the path already identifies the language, or no domains configured for the language).
/// </param>
/// <param name="UsesLanguageDomains">The channel uses language-specific domains (<see cref="CMS.Websites.WebsiteChannelLanguageRoutingMode.LanguageDomains"/>).</param>
public sealed record WebPageStatsTarget(
    Guid WebPageItemGuid,
    int LanguageId,
    int ChannelId,
    string? FormUrlPath,
    IReadOnlyList<string> FormUrlHosts,
    bool UsesLanguageDomains)
{
    /// <summary>
    /// Target of a language prefix channel (no host matching).
    /// </summary>
    public WebPageStatsTarget(Guid webPageItemGuid, int languageId, int channelId, string? formUrlPath)
        : this(webPageItemGuid, languageId, channelId, formUrlPath, [], UsesLanguageDomains: false)
    {
    }
}

/// <summary>
/// Activity counts of one web page per type and day, plus distinct contacts. Aggregates only, no contact data.
/// </summary>
/// <param name="DailyCounts">Activity count per activity type and day.</param>
/// <param name="UniqueContacts">Distinct contacts with at least one activity on the page in the range.</param>
/// <param name="UniqueVisitors">Distinct contacts with at least one page visit in the range.</param>
/// <param name="UniqueSubmitters">Distinct contacts with at least one form submission on the page in the range.</param>
public sealed record WebPageStatsData(IReadOnlyList<StatsDailyCount> DailyCounts, int UniqueContacts, int UniqueVisitors, int UniqueSubmitters)
{
    public static WebPageStatsData Empty { get; } = new([], 0, 0, 0);
}

/// <summary>
/// Web page data with the time it was read. This is the cached value.
/// </summary>
internal sealed record WebPageStatsSnapshot(WebPageStatsData Data, DateTimeOffset ReadAt);

/// <summary>
/// Contact activities on one web page per type per period.
/// </summary>
/// <param name="From">Applied range start (inclusive).</param>
/// <param name="To">Applied range end (inclusive).</param>
/// <param name="Grouping">Applied grouping.</param>
/// <param name="Periods">Continuous period axis.</param>
/// <param name="Series">One series per activity type, sorted by total (descending). Values align with <paramref name="Periods"/>.</param>
/// <param name="Total">Sum of all activities in the range.</param>
/// <param name="PageVisits">Page visit activities in the range.</param>
/// <param name="UniqueContacts">Distinct contacts with at least one activity in the range.</param>
/// <param name="UniqueVisitors">Distinct contacts with at least one page visit in the range.</param>
/// <param name="FormSubmissions">Form submission activities matched by the page URL in the range.</param>
/// <param name="UniqueSubmitters">Distinct contacts with at least one matched form submission in the range.</param>
/// <param name="FormUrlPath">Path form submissions were matched by (see <see cref="WebPageStatsTarget.FormUrlPath"/>). <c>null</c> when the page has no live URL.</param>
/// <param name="FormUrlHosts">Hosts form submissions were matched by (see <see cref="WebPageStatsTarget.FormUrlHosts"/>). Empty means any host.</param>
/// <param name="UsesLanguageDomains">The page's channel uses language-specific domains.</param>
public sealed record WebPageStatsResult(
    DateOnly From,
    DateOnly To,
    StatsGrouping Grouping,
    IReadOnlyList<StatsPeriod> Periods,
    IReadOnlyList<StatsTimeSeries> Series,
    int Total,
    int PageVisits,
    int UniqueContacts,
    int UniqueVisitors,
    int FormSubmissions,
    int UniqueSubmitters,
    string? FormUrlPath,
    IReadOnlyList<string> FormUrlHosts,
    bool UsesLanguageDomains)
{
    /// <summary>
    /// When the data was read from the database. Can be older than the request when served from cache.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }
}
