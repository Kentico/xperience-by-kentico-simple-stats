using System.Data;
using System.Data.Common;

using CMS.Activities;
using CMS.DataEngine;

using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.CampaignSources;

/// <summary>
/// Reads landing page activities and their UTM values from the database. Never returns contact identifiers.
/// </summary>
internal interface ICampaignSourcesRepository
{
    /// <summary>
    /// Returns totals for the range and the previous period, daily campaign landings, sources, pages and source and content pairs.
    /// </summary>
    /// <param name="query">Normalized filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<CampaignSourcesData> GetData(CampaignSourcesQuery query, CancellationToken cancellationToken);
}

internal sealed class CampaignSourcesRepository : ICampaignSourcesRepository
{
    private const string ReportName = "campaign sources";

    public async Task<CampaignSourcesData> GetData(CampaignSourcesQuery query, CancellationToken cancellationToken)
    {
        var range = query.Range;
        var (previousFrom, _) = StatsComparison.GetPreviousRange(range);
        var content = GetContentFilter(query);

        var parameters = new QueryDataParameters
        {
            new DataParameter(CampaignSourcesSql.LandingPageTypeParameter, PredefinedActivityType.LANDING_PAGE),
            new DataParameter(CampaignSourcesSql.PreviousFromParameter, previousFrom.ToDateTime(TimeOnly.MinValue)),
            new DataParameter(CampaignSourcesSql.FromParameter, range.From.ToDateTime(TimeOnly.MinValue)),
            new DataParameter(CampaignSourcesSql.ToExclusiveParameter, range.To.AddDays(1).ToDateTime(TimeOnly.MinValue)),
            new DataParameter(CampaignSourcesSql.SeriesLimitParameter, CampaignSourcesReportBuilder.SeriesLimit),
            new DataParameter(CampaignSourcesSql.SourceLimitParameter, CampaignSourcesReportBuilder.SourceOptionLimit),
            new DataParameter(CampaignSourcesSql.PageLimitParameter, CampaignSourcesReportBuilder.PageLimit),
            new DataParameter(CampaignSourcesSql.ContentLimitParameter, CampaignSourcesReportBuilder.ContentLimit),
        };
        if (range.ChannelId is int channelId)
        {
            parameters.Add(new DataParameter(CampaignSourcesSql.ChannelParameter, channelId));
        }
        if (query.Source is string source)
        {
            parameters.Add(new DataParameter(CampaignSourcesSql.SourceParameter, source));
        }
        if (content == CampaignContentFilter.Value)
        {
            parameters.Add(new DataParameter(CampaignSourcesSql.ContentParameter, query.Content));
        }

        string sql = CampaignSourcesSql.Build(range.ChannelId is not null, query.Source is not null, content);

        await using var reader = await ConnectionHelper.ExecuteReaderAsync(sql, parameters, QueryTypeEnum.SQLQuery, CommandBehavior.Default, cancellationToken);

        var totals = await ReadTotals(reader, cancellationToken);
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var daily = await ReadDaily(reader, cancellationToken);
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var sources = await ReadSources(reader, cancellationToken);
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var (pages, pageCount) = await ReadPages(reader, cancellationToken);
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var (contents, contentCount) = await ReadContents(reader, cancellationToken);

        return new(totals, daily, sources, pages, pageCount, contents, contentCount);
    }

    internal static CampaignContentFilter GetContentFilter(CampaignSourcesQuery query)
    {
        if (query.Source is null || query.Content is null)
        {
            return CampaignContentFilter.All;
        }

        return query.Content.Length == 0 ? CampaignContentFilter.None : CampaignContentFilter.Value;
    }

    private static async Task<CampaignSourcesTotalsRow> ReadTotals(DbDataReader reader, CancellationToken cancellationToken)
    {
        if (!await reader.ReadAsync(cancellationToken))
        {
            return CampaignSourcesTotalsRow.Empty;
        }

        return new(
            reader.GetInt32(reader.GetOrdinal("Landings")),
            reader.GetInt32(reader.GetOrdinal("PreviousLandings")),
            reader.GetInt32(reader.GetOrdinal("AllCampaignLandings")),
            reader.GetInt32(reader.GetOrdinal("CampaignLandings")),
            reader.GetInt32(reader.GetOrdinal("PreviousCampaignLandings")),
            reader.GetInt32(reader.GetOrdinal("CampaignVisitors")),
            reader.GetInt32(reader.GetOrdinal("Sources")),
            reader.GetInt32(reader.GetOrdinal("PreviousSources")));
    }

    private static async Task<IReadOnlyList<CampaignSourcesDailyRow>> ReadDaily(DbDataReader reader, CancellationToken cancellationToken)
    {
        int sourceOrdinal = reader.GetOrdinal("Source");
        int dayOrdinal = reader.GetOrdinal("Day");
        int landingsOrdinal = reader.GetOrdinal("Landings");

        var rows = new List<CampaignSourcesDailyRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                reader.IsDBNull(sourceOrdinal) ? null : reader.GetString(sourceOrdinal),
                DateOnly.FromDateTime(reader.GetDateTime(dayOrdinal)),
                reader.GetInt32(landingsOrdinal)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<CampaignSourcesSourceRow>> ReadSources(DbDataReader reader, CancellationToken cancellationToken)
    {
        int sourceOrdinal = reader.GetOrdinal("Source");
        int landingsOrdinal = reader.GetOrdinal("Landings");
        int visitorsOrdinal = reader.GetOrdinal("Visitors");
        int previousOrdinal = reader.GetOrdinal("PreviousLandings");

        var rows = new List<CampaignSourcesSourceRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                reader.GetString(sourceOrdinal),
                reader.GetInt32(landingsOrdinal),
                reader.GetInt32(visitorsOrdinal),
                reader.GetInt32(previousOrdinal)));
        }

        return rows;
    }

    private static async Task<(IReadOnlyList<CampaignSourcesPageRow> Rows, int Count)> ReadPages(DbDataReader reader, CancellationToken cancellationToken)
    {
        int guidOrdinal = reader.GetOrdinal("PageGUID");
        int languageIdOrdinal = reader.GetOrdinal("LanguageID");
        int landingsOrdinal = reader.GetOrdinal("Landings");
        int visitorsOrdinal = reader.GetOrdinal("Visitors");
        int urlOrdinal = reader.GetOrdinal("Url");
        int countOrdinal = reader.GetOrdinal("PageCount");
        int nameOrdinal = reader.GetOrdinal("DisplayName");
        int languageOrdinal = reader.GetOrdinal("Language");
        int channelOrdinal = reader.GetOrdinal("Channel");

        var rows = new List<CampaignSourcesPageRow>();
        int count = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                reader.IsDBNull(guidOrdinal) ? null : reader.GetGuid(guidOrdinal),
                reader.IsDBNull(languageIdOrdinal) ? null : reader.GetInt32(languageIdOrdinal),
                reader.GetInt32(landingsOrdinal),
                reader.GetInt32(visitorsOrdinal),
                reader.IsDBNull(urlOrdinal) ? null : reader.GetString(urlOrdinal),
                reader.IsDBNull(nameOrdinal) ? null : reader.GetString(nameOrdinal),
                reader.IsDBNull(languageOrdinal) ? null : reader.GetString(languageOrdinal),
                reader.IsDBNull(channelOrdinal) ? null : reader.GetString(channelOrdinal)));

            // Same value on every row.
            count = reader.GetInt32(countOrdinal);
        }

        return (rows, count);
    }

    private static async Task<(IReadOnlyList<CampaignSourcesContentRow> Rows, int Count)> ReadContents(DbDataReader reader, CancellationToken cancellationToken)
    {
        int sourceOrdinal = reader.GetOrdinal("Source");
        int contentOrdinal = reader.GetOrdinal("Content");
        int landingsOrdinal = reader.GetOrdinal("Landings");
        int visitorsOrdinal = reader.GetOrdinal("Visitors");
        int countOrdinal = reader.GetOrdinal("GroupCount");

        var rows = new List<CampaignSourcesContentRow>();
        int count = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                reader.GetString(sourceOrdinal),
                reader.IsDBNull(contentOrdinal) ? null : reader.GetString(contentOrdinal),
                reader.GetInt32(landingsOrdinal),
                reader.GetInt32(visitorsOrdinal)));

            // Same value on every row.
            count = reader.GetInt32(countOrdinal);
        }

        return (rows, count);
    }
}
