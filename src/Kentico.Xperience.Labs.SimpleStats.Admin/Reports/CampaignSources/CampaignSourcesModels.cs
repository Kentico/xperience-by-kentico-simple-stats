using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.CampaignSources;

/// <summary>
/// Filter of the campaign sources report, sent by the admin client: the shared range, grouping and website channel (<see cref="StatsFilter"/>)
/// plus an optional UTM source and content. The shared filter is wrapped, not changed, so other reports keep their filter and cache keys.
/// </summary>
public sealed record CampaignSourcesFilter
{
    /// <summary>
    /// Range, grouping and website channel. A channel that is not a website channel is dropped. <c>null</c> means defaults.
    /// </summary>
    public StatsFilter? Range { get; init; }

    /// <summary>
    /// Optional UTM source. <c>null</c> or empty means all sources. Compared as the database compares text (case-insensitive by default).
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// Optional UTM content of <see cref="Source"/>. <c>null</c> means all contents, an empty string means landings without a content.
    /// Ignored without a source.
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// Applies defaults and limits and returns a query that is safe to run.
    /// </summary>
    /// <param name="today">Current date used for the default range.</param>
    /// <param name="channels">Website channels.</param>
    public CampaignSourcesQuery Normalize(DateOnly today, IEnumerable<StatsChannelOption> channels)
    {
        var range = (Range ?? new StatsFilter()).Normalize(today);
        int? channelId = range.ChannelId is int id && channels.Any(c => c.Id == id) ? id : null;

        string? source = Trim(Source);
        string? content = source is null || Content is null ? null : Trim(Content) ?? string.Empty;

        return new(range with { ChannelId = channelId }, source, content);
    }

    /// <summary>
    /// Trimmed value (as the SQL trims stored values), cut to the column length; <c>null</c> when empty.
    /// </summary>
    private static string? Trim(string? value)
    {
        string trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return null;
        }

        return trimmed.Length > CampaignSourcesReportBuilder.ValueMaxLength ? trimmed[..CampaignSourcesReportBuilder.ValueMaxLength] : trimmed;
    }
}

/// <summary>
/// Normalized campaign sources filter.
/// </summary>
/// <param name="Range">Range, grouping and website channel.</param>
/// <param name="Source">Trimmed UTM source, or <c>null</c> for all.</param>
/// <param name="Content">Trimmed UTM content of the source, an empty string for landings without a content, or <c>null</c> for all.</param>
public sealed record CampaignSourcesQuery(StatsQuery Range, string? Source, string? Content);

/// <summary>
/// Input of the campaign sources <c>LOAD</c> page command.
/// </summary>
public sealed record CampaignSourcesLoadRequest
{
    /// <summary>
    /// Report filter. <c>null</c> means defaults.
    /// </summary>
    public CampaignSourcesFilter? Filter { get; init; }

    /// <summary>
    /// When <c>true</c>, cached data for the filter is dropped and read again from the database.
    /// </summary>
    public bool Refresh { get; init; }
}

/// <summary>
/// Campaign sources report: landing page activities in the range and the UTM values stored on them
/// (Xperience does not fill them; see "UTM capture" in the usage guide). Aggregates only, no contact details.
/// </summary>
/// <param name="From">Applied range start (inclusive).</param>
/// <param name="To">Applied range end (inclusive).</param>
/// <param name="Grouping">Applied grouping.</param>
/// <param name="ChannelId">Applied website channel filter, or <c>null</c> for all.</param>
/// <param name="Source">Applied source filter, or <c>null</c> for all.</param>
/// <param name="Content">Applied content filter (empty string: no content), or <c>null</c> for all.</param>
/// <param name="Landings">All landings (with or without UTM values) vs the previous period. Not limited by the source filter.</param>
/// <param name="CampaignLandings">Campaign landings (of the selected source and content) vs the previous period.</param>
/// <param name="CampaignShare"><paramref name="CampaignLandings"/> / <paramref name="Landings"/> in the range (0-1), or <c>null</c> without landings.</param>
/// <param name="CampaignVisitors">Distinct contacts with a campaign landing (of the selected source and content) in the range.</param>
/// <param name="Sources">Distinct UTM sources in the range vs the previous period. Not limited by the source filter.</param>
/// <param name="Series">Campaign landings (of the selected source and content) per period, by the top sources plus "Other".</param>
/// <param name="BySource">
/// Top sources in the range (not limited by the source filter): value = landings, secondary value = visitors, previous value = landings
/// in the previous period. Share is of all campaign landings.
/// </param>
/// <param name="Pages">
/// Top landing pages of the campaign landings (of the selected source and content): label = page name, secondary label = channel and language,
/// value = landings, secondary value = visitors, URL = the public page URL. Share is of those campaign landings.
/// </param>
/// <param name="BySourceContent">
/// Top source and content pairs (of the selected source, not limited by the content filter): label = source, secondary label = content
/// (<c>(none)</c> when empty), value = landings, secondary value = visitors.
/// </param>
/// <param name="SourceOptions">Sources with campaign landings in the range, most first, plus the selected source. For the source filter.</param>
/// <param name="ContentOptions">
/// Contents of the selected source in the range, most first (empty string: no content), plus the selected content. Empty without a source.
/// </param>
/// <param name="HasAnyUtmData">
/// Any activity in the database has a UTM source. <c>false</c> means UTM values are not captured on this site,
/// as opposed to no campaign landings in the range.
/// </param>
public sealed record CampaignSourcesResult(
    DateOnly From,
    DateOnly To,
    StatsGrouping Grouping,
    int? ChannelId,
    string? Source,
    string? Content,
    StatsComparison Landings,
    StatsComparison CampaignLandings,
    double? CampaignShare,
    int CampaignVisitors,
    StatsComparison Sources,
    StatsTimeSeriesResult Series,
    StatsRankedResult BySource,
    StatsRankedResult Pages,
    StatsRankedResult BySourceContent,
    IReadOnlyList<string> SourceOptions,
    IReadOnlyList<string> ContentOptions,
    bool HasAnyUtmData)
{
    /// <summary>
    /// When the data was read from the database. Can be older than the request when served from cache.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// Totals (one row).
/// </summary>
/// <param name="Landings">Landings in the range.</param>
/// <param name="PreviousLandings">Landings in the previous period.</param>
/// <param name="AllCampaignLandings">Landings with any UTM source in the range (not limited by the source filter).</param>
/// <param name="CampaignLandings">Campaign landings of the selected source and content in the range.</param>
/// <param name="PreviousCampaignLandings">The same in the previous period.</param>
/// <param name="CampaignVisitors">Distinct contacts of <paramref name="CampaignLandings"/>.</param>
/// <param name="Sources">Distinct sources in the range.</param>
/// <param name="PreviousSources">Distinct sources in the previous period.</param>
internal sealed record CampaignSourcesTotalsRow(
    int Landings,
    int PreviousLandings,
    int AllCampaignLandings,
    int CampaignLandings,
    int PreviousCampaignLandings,
    int CampaignVisitors,
    int Sources,
    int PreviousSources)
{
    public static CampaignSourcesTotalsRow Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0);
}

/// <summary>
/// Campaign landings of one source on one day. <paramref name="Source"/> is <c>null</c> for the sources outside the top ones ("Other").
/// </summary>
internal sealed record CampaignSourcesDailyRow(string? Source, DateOnly Date, int Landings);

/// <summary>
/// Campaign landings of one source in the range.
/// </summary>
/// <param name="Source">Trimmed source, never empty.</param>
/// <param name="Landings">Landings in the range.</param>
/// <param name="Visitors">Distinct contacts in the range.</param>
/// <param name="PreviousLandings">Landings in the previous period.</param>
internal sealed record CampaignSourcesSourceRow(string Source, int Landings, int Visitors, int PreviousLandings);

/// <summary>
/// Campaign landings of one page variant in the range.
/// </summary>
/// <param name="PageGuid"><c>ActivityWebPageItemGUID</c>, or <c>null</c> when not logged.</param>
/// <param name="LanguageId"><c>ActivityLanguageID</c>, or <c>null</c>.</param>
/// <param name="Landings">Landings.</param>
/// <param name="Visitors">Distinct contacts.</param>
/// <param name="Url">One landing URL without query string and fragment, or <c>null</c>.</param>
/// <param name="DisplayName">Page name in the language, or <c>null</c> when the page or variant no longer exists.</param>
/// <param name="Language">Language display name, or <c>null</c>.</param>
/// <param name="Channel">Website channel display name, or <c>null</c>.</param>
internal sealed record CampaignSourcesPageRow(
    Guid? PageGuid,
    int? LanguageId,
    int Landings,
    int Visitors,
    string? Url,
    string? DisplayName,
    string? Language,
    string? Channel);

/// <summary>
/// Campaign landings of one source and content pair in the range.
/// </summary>
/// <param name="Source">Trimmed source, never empty.</param>
/// <param name="Content">Trimmed content, or <c>null</c> when empty.</param>
/// <param name="Landings">Landings.</param>
/// <param name="Visitors">Distinct contacts.</param>
internal sealed record CampaignSourcesContentRow(string Source, string? Content, int Landings, int Visitors);

/// <summary>
/// Data read by <see cref="ICampaignSourcesRepository"/>.
/// </summary>
/// <param name="Totals">Totals.</param>
/// <param name="Daily">Campaign landings (of the selected source and content) per day and top source in the range.</param>
/// <param name="Sources">Sources with campaign landings in the range, most first (up to <see cref="CampaignSourcesReportBuilder.SourceOptionLimit"/>).</param>
/// <param name="Pages">Top landing pages (of the selected source and content).</param>
/// <param name="PageCount">Distinct landing pages (of the selected source and content), not only <paramref name="Pages"/>.</param>
/// <param name="Contents">Top source and content pairs (of the selected source).</param>
/// <param name="ContentCount">Distinct pairs, not only <paramref name="Contents"/>.</param>
internal sealed record CampaignSourcesData(
    CampaignSourcesTotalsRow Totals,
    IReadOnlyList<CampaignSourcesDailyRow> Daily,
    IReadOnlyList<CampaignSourcesSourceRow> Sources,
    IReadOnlyList<CampaignSourcesPageRow> Pages,
    int PageCount,
    IReadOnlyList<CampaignSourcesContentRow> Contents,
    int ContentCount)
{
    public static CampaignSourcesData Empty { get; } = new(CampaignSourcesTotalsRow.Empty, [], [], [], 0, [], 0);
}

/// <summary>
/// Campaign sources data with the time it was read. This is the cached value.
/// </summary>
internal sealed record CampaignSourcesSnapshot(CampaignSourcesData Data, DateTimeOffset ReadAt);
