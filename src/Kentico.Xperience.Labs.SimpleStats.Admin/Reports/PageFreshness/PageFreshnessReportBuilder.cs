using System.Globalization;

using CMS.ContentEngine;

using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContentInventory;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PageFreshness;

/// <summary>
/// Turns page totals, age buckets and lists into the page freshness report.
/// </summary>
internal static class PageFreshnessReportBuilder
{
    /// <summary>
    /// Rows of each list (stale pages with visits, pages without visits).
    /// </summary>
    public const int ListLimit = 25;

    /// <summary>
    /// Channel types of the channel filter: pages are in website channels only.
    /// </summary>
    public static IReadOnlyList<ChannelType> ChannelTypes { get; } = [ChannelType.Website];

    /// <summary>
    /// Normalizes the range like the other range reports (grouping is not used and always <see cref="StatsGrouping.Day"/>, so it does not
    /// split the cache) and drops a channel that is not one of <paramref name="channels"/> (website channels).
    /// </summary>
    public static StatsQuery Normalize(StatsFilter? filter, DateOnly today, IEnumerable<StatsChannelOption> channels)
    {
        var query = (filter ?? new StatsFilter()).Normalize(today);
        int? channelId = query.ChannelId is int id && channels.Any(c => c.Id == id) ? id : null;

        return query with { Grouping = StatsGrouping.Day, ChannelId = channelId };
    }

    /// <summary>
    /// Builds the report.
    /// </summary>
    /// <param name="query">Normalized filter.</param>
    /// <param name="data">Data for the filter.</param>
    public static PageFreshnessResult Build(StatsQuery query, PageFreshnessData data)
    {
        var totals = data.Totals;
        int published = Math.Max(totals.PublishedPages, 0);
        int stale = Math.Clamp(totals.StalePages, 0, published);
        int visits = Math.Max(totals.Visits, 0);
        int staleVisits = Math.Clamp(totals.StaleVisits, 0, visits);

        var ageBuckets = StatsRankedBuilder.Build(
            query,
            ContentInventoryReportBuilder.GetAgeEntries(data.AgePages, data.AgeVisits),
            published,
            0,
            limit: int.MaxValue,
            includeZero: true,
            keepOrder: true);

        return new(
            query.From,
            query.To,
            query.ChannelId,
            published,
            stale,
            Share(stale, published),
            visits,
            staleVisits,
            Share(staleVisits, visits),
            Math.Max(totals.StalePopularPages, data.StalePopular.Count),
            Math.Max(totals.NoVisitPages, data.NoVisits.Count),
            ContentInventoryReportBuilder.StaleMonths,
            ageBuckets,
            ToItems(data.StalePopular),
            ToItems(data.NoVisits));
    }

    private static double Share(int part, int total) => total > 0 ? (double)part / total : 0;

    private static List<PageFreshnessItem> ToItems(IReadOnlyList<PageFreshnessRow> rows) =>
        rows
            .Take(ListLimit)
            .Select(row => new PageFreshnessItem(
                string.Create(CultureInfo.InvariantCulture, $"{row.VariantId}-{row.WebPageItemId}"),
                row.DisplayName,
                string.IsNullOrWhiteSpace(row.Channel) ? null : row.Channel,
                row.Language,
                row.TreePath,
                DateOnly.FromDateTime(row.ModifiedWhen),
                row.FirstPublishedWhen is DateTime firstPublished ? DateOnly.FromDateTime(firstPublished) : null,
                Math.Max(row.Visits, 0),
                Math.Max(row.Visitors, 0))
            {
                // The page's Content tab in its website channel, in the variant's language.
                AdminPath = StatsChannelItemPaths.GetWebPagePath(row.WebsiteChannelId, row.LanguageName, row.WebPageItemId),
            })
            .ToList();
}
