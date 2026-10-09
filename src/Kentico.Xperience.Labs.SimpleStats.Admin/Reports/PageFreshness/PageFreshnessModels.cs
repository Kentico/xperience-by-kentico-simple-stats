using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContentInventory;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PageFreshness;

/// <summary>
/// Published pages by time since their last change, combined with their page visits in a date range:
/// stale pages people still visit, and published pages nobody visits. Counts are per page language variant.
/// </summary>
/// <param name="From">Applied range start (inclusive).</param>
/// <param name="To">Applied range end (inclusive).</param>
/// <param name="ChannelId">Applied website channel filter (<c>CMS_Channel</c> ID), or <c>null</c> for all.</param>
/// <param name="PublishedPages">Published page variants (pages with a URL, folders left out).</param>
/// <param name="StalePages">Of <paramref name="PublishedPages"/>, variants not changed in <paramref name="StaleMonths"/> months.</param>
/// <param name="StaleShare"><paramref name="StalePages"/> / <paramref name="PublishedPages"/> (0–1), 0 without pages.</param>
/// <param name="Visits">Page visits of the published page variants in the range.</param>
/// <param name="StaleVisits">Of <paramref name="Visits"/>, visits of stale variants.</param>
/// <param name="StaleVisitShare"><paramref name="StaleVisits"/> / <paramref name="Visits"/> (0–1), 0 without visits.</param>
/// <param name="StalePopularPages">Stale variants with at least one visit in the range (all, not only <paramref name="StalePopular"/>).</param>
/// <param name="NoVisitPages">
/// Published variants with no visit in the range that were first published before the range start (all, not only <paramref name="NoVisits"/>).
/// </param>
/// <param name="StaleMonths">Months without a change after which a page is stale (same as the content inventory).</param>
/// <param name="AgeBuckets">
/// Published variants per time since their last change, in a fixed order (keys of the content inventory age buckets), 0 included.
/// <see cref="StatsRankedItem.SecondaryValue"/> is the visits of the bucket's variants in the range.
/// </param>
/// <param name="StalePopular">Stale variants with visits, most visits first (up to <see cref="PageFreshnessReportBuilder.ListLimit"/>).</param>
/// <param name="NoVisits">
/// Variants counted in <paramref name="NoVisitPages"/>, first published longest ago first (unknown first publish first),
/// up to <see cref="PageFreshnessReportBuilder.ListLimit"/>.
/// </param>
public sealed record PageFreshnessResult(
    DateOnly From,
    DateOnly To,
    int? ChannelId,
    int PublishedPages,
    int StalePages,
    double StaleShare,
    int Visits,
    int StaleVisits,
    double StaleVisitShare,
    int StalePopularPages,
    int NoVisitPages,
    int StaleMonths,
    StatsRankedResult AgeBuckets,
    IReadOnlyList<PageFreshnessItem> StalePopular,
    IReadOnlyList<PageFreshnessItem> NoVisits)
{
    /// <summary>
    /// When the data was read from the database. Can be older than the request when served from cache.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// One page language variant of a list.
/// </summary>
/// <param name="Key">Language metadata ID with the web page item ID. Unique in the list.</param>
/// <param name="Name">Variant display name.</param>
/// <param name="Channel">Website channel display name, or <c>null</c>.</param>
/// <param name="Language">Language display name.</param>
/// <param name="TreePath">Page's position in the content tree (<c>WebPageItemTreePath</c>, the same in all languages), not its live URL.</param>
/// <param name="LastModified">Last change of the variant's latest version (server date).</param>
/// <param name="FirstPublished">
/// First publish of the variant (server date); its last publish when the first is not stored. <c>null</c> when neither is stored.
/// </param>
/// <param name="Visits">Page visits in the range.</param>
/// <param name="Visitors">Distinct contacts with a page visit in the range.</param>
public sealed record PageFreshnessItem(
    string Key,
    string Name,
    string? Channel,
    string Language,
    string TreePath,
    DateOnly LastModified,
    DateOnly? FirstPublished,
    int Visits,
    int Visitors)
{
    /// <summary>
    /// Content tab of the page in its website channel (relative to the admin root, see <see cref="StatsChannelItemPaths"/>), or <c>null</c>.
    /// </summary>
    public string? AdminPath { get; init; }
}

/// <summary>
/// Totals over all published page variants that match the filter.
/// </summary>
internal sealed record PageFreshnessTotals(int PublishedPages, int StalePages, int Visits, int StaleVisits, int StalePopularPages, int NoVisitPages)
{
    public static PageFreshnessTotals Empty { get; } = new(0, 0, 0, 0, 0, 0);
}

/// <summary>
/// One page language variant of a list, as read from the database.
/// </summary>
/// <param name="VariantId">Language metadata ID.</param>
/// <param name="WebPageItemId">Web page item ID.</param>
/// <param name="WebsiteChannelId"><c>WebsiteChannelID</c> of the page.</param>
/// <param name="DisplayName">Variant display name.</param>
/// <param name="Channel">Website channel display name, or <c>null</c>.</param>
/// <param name="LanguageName">Language code name.</param>
/// <param name="Language">Language display name.</param>
/// <param name="TreePath">Page's tree path.</param>
/// <param name="ModifiedWhen">Last change of the variant's latest version (server time).</param>
/// <param name="FirstPublishedWhen">First publish (or last publish when the first is not stored), server time, or <c>null</c>.</param>
/// <param name="Visits">Page visits in the range.</param>
/// <param name="Visitors">Distinct contacts with a page visit in the range.</param>
internal sealed record PageFreshnessRow(
    int VariantId,
    int WebPageItemId,
    int WebsiteChannelId,
    string DisplayName,
    string? Channel,
    string LanguageName,
    string Language,
    string TreePath,
    DateTime ModifiedWhen,
    DateTime? FirstPublishedWhen,
    int Visits,
    int Visitors);

/// <summary>
/// Data read by <see cref="IPageFreshnessRepository"/>.
/// </summary>
/// <param name="Totals">Totals over all published page variants.</param>
/// <param name="AgePages">Published page variants per age bucket.</param>
/// <param name="AgeVisits">Visits of the variants per age bucket.</param>
/// <param name="StalePopular">Stale variants with visits, most visits first.</param>
/// <param name="NoVisits">Variants without visits (first published before the range), first published longest ago first.</param>
internal sealed record PageFreshnessData(
    PageFreshnessTotals Totals,
    ContentAgeRow AgePages,
    ContentAgeRow AgeVisits,
    IReadOnlyList<PageFreshnessRow> StalePopular,
    IReadOnlyList<PageFreshnessRow> NoVisits)
{
    public static PageFreshnessData Empty { get; } = new(PageFreshnessTotals.Empty, ContentAgeRow.Empty, ContentAgeRow.Empty, [], []);
}

/// <summary>
/// Page freshness data with the time it was read. This is the cached value.
/// </summary>
internal sealed record PageFreshnessSnapshot(PageFreshnessData Data, DateTimeOffset ReadAt);
