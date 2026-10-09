using System.Globalization;

using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.TopPages;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.CampaignSources;

/// <summary>
/// Turns totals, daily counts and lists into the campaign sources report.
/// </summary>
internal static class CampaignSourcesReportBuilder
{
    /// <summary>
    /// Sources shown as their own series in the chart; the rest is "Other".
    /// </summary>
    public const int SeriesLimit = 5;

    /// <summary>
    /// Rows of the top sources list.
    /// </summary>
    public const int SourceLimit = 10;

    /// <summary>
    /// Sources read for the source filter options (and the top sources list).
    /// </summary>
    public const int SourceOptionLimit = 100;

    /// <summary>
    /// Rows of the top landing pages list.
    /// </summary>
    public const int PageLimit = 25;

    /// <summary>
    /// Rows of the source and content list (also the content filter options).
    /// </summary>
    public const int ContentLimit = 25;

    /// <summary>
    /// Length of the UTM columns (<c>nvarchar(200)</c>). Longer filter values cannot match.
    /// </summary>
    public const int ValueMaxLength = 200;

    /// <summary>
    /// Label of a landing page that no longer exists and has no URL.
    /// </summary>
    public const string UnknownPageLabel = "(unknown page)";

    /// <summary>
    /// Prefix of the source series keys, so a source cannot collide with <see cref="StatsTimeSeriesBuilder.OtherSeries"/>.
    /// </summary>
    public const string SourceSeriesPrefix = "source:";

    /// <summary>
    /// Builds the report.
    /// </summary>
    /// <param name="query">Normalized filter.</param>
    /// <param name="data">Data for the filter.</param>
    /// <param name="hasAnyUtmData">Any activity in the database has a UTM source.</param>
    public static CampaignSourcesResult Build(CampaignSourcesQuery query, CampaignSourcesData data, bool hasAnyUtmData)
    {
        var range = query.Range;
        var totals = data.Totals;
        int landings = Math.Max(totals.Landings, 0);
        int campaignLandings = Math.Max(totals.CampaignLandings, 0);

        var bySource = StatsRankedBuilder.Build(
            range,
            data.Sources.Select(s => new StatsRankedEntry(s.Source, s.Source, null, s.Landings, s.Visitors, null)
            {
                PreviousValue = Math.Max(s.PreviousLandings, 0),
            }),
            Math.Max(totals.AllCampaignLandings, 0),
            Math.Max(totals.Sources, 0),
            SourceLimit);

        var pages = StatsRankedBuilder.Build(
            range,
            data.Pages.Select(ToPageEntry),
            campaignLandings,
            data.PageCount,
            PageLimit);

        // The pairs follow the source filter only, so their total is the selected source's landings (or all campaign landings).
        int contentTotal = query.Source is null
            ? totals.AllCampaignLandings
            : data.Sources.FirstOrDefault(s => string.Equals(s.Source, query.Source, StringComparison.OrdinalIgnoreCase))?.Landings ?? 0;

        var bySourceContent = StatsRankedBuilder.Build(
            range,
            data.Contents.Select(c => new StatsRankedEntry(
                StatsUtm.GetPairKey(c.Source, c.Content),
                c.Source,
                c.Content ?? StatsUtm.NoValueLabel,
                c.Landings,
                c.Visitors,
                null)),
            Math.Max(contentTotal, 0),
            data.ContentCount,
            ContentLimit);

        return new(
            range.From,
            range.To,
            range.Grouping,
            range.ChannelId,
            query.Source,
            query.Content,
            StatsComparison.Create(range, landings, Math.Max(totals.PreviousLandings, 0)),
            StatsComparison.Create(range, campaignLandings, Math.Max(totals.PreviousCampaignLandings, 0)),
            landings > 0 ? (double)campaignLandings / landings : null,
            Math.Max(totals.CampaignVisitors, 0),
            StatsComparison.Create(range, Math.Max(totals.Sources, 0), Math.Max(totals.PreviousSources, 0)),
            BuildSeries(range, data.Daily),
            bySource,
            pages,
            bySourceContent,
            WithSelected(data.Sources.Select(s => s.Source), query.Source),
            query.Source is null ? [] : WithSelected(data.Contents.Select(c => c.Content ?? string.Empty), query.Content),
            // Campaign landings in the range prove the site has UTM data.
            hasAnyUtmData || totals.AllCampaignLandings > 0);
    }

    /// <summary>
    /// One series per top source (most landings first), then "Other" for the rest when it has landings.
    /// </summary>
    internal static StatsTimeSeriesResult BuildSeries(StatsQuery range, IReadOnlyList<CampaignSourcesDailyRow> daily)
    {
        var other = StatsTimeSeriesBuilder.OtherSeries;

        var definitions = daily
            .Where(row => row.Source is not null && row.Landings > 0)
            .GroupBy(row => row.Source!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Sum(row => row.Landings))
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new StatsSeriesDefinition(SourceSeriesPrefix + group.Key, group.Key))
            .ToList();

        if (daily.Any(row => row.Source is null && row.Landings > 0))
        {
            definitions.Add(other);
        }

        var counts = daily.Select(row => new StatsDailyCount(row.Source is null ? other.Key : SourceSeriesPrefix + row.Source, row.Date, row.Landings));

        return StatsTimeSeriesBuilder.BuildFixed(range, counts, definitions);
    }

    private static StatsRankedEntry ToPageEntry(CampaignSourcesPageRow row)
    {
        string? url = string.IsNullOrWhiteSpace(row.Url) ? null : row.Url;
        string key = row.PageGuid is Guid guid
            ? string.Create(CultureInfo.InvariantCulture, $"{guid:N}-{row.LanguageId ?? 0}")
            : "url:" + (url ?? string.Empty);
        string label = !string.IsNullOrWhiteSpace(row.DisplayName) ? row.DisplayName : url ?? UnknownPageLabel;
        string? details = string.Join(
            " · ",
            new[] { row.Channel, row.Language }.Where(value => !string.IsNullOrWhiteSpace(value)));

        // Same link as the top pages report: the public URL, opened in a new tab.
        return new(key, label, details.Length > 0 ? details : null, row.Landings, row.Visitors, TopPagesReportBuilder.GetPublicUrl(url));
    }

    private static List<string> WithSelected(IEnumerable<string> options, string? selected)
    {
        var list = options.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (selected is not null && !list.Contains(selected, StringComparer.OrdinalIgnoreCase))
        {
            list.Add(selected);
        }

        return list;
    }
}
