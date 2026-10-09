using System.Data;
using System.Data.Common;

using CMS.Activities;
using CMS.ContentEngine;
using CMS.DataEngine;

using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContentInventory;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PageFreshness;

/// <summary>
/// Reads published pages with their last change and page visits from the database.
/// </summary>
internal interface IPageFreshnessRepository
{
    /// <summary>
    /// Returns totals, age buckets and the stale and no-visit lists for the query.
    /// </summary>
    /// <param name="query">Normalized filter (range and website channel).</param>
    /// <param name="now">Server time page ages are counted to (<c>ModifiedWhen</c> is compared as stored).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<PageFreshnessData> GetData(StatsQuery query, DateTime now, CancellationToken cancellationToken);
}

internal sealed class PageFreshnessRepository : IPageFreshnessRepository
{
    private const string ReportName = "page freshness";

    public async Task<PageFreshnessData> GetData(StatsQuery query, DateTime now, CancellationToken cancellationToken)
    {
        var parameters = new QueryDataParameters
        {
            new DataParameter(StatsContentSql.ClassTypeParameter, ClassType.CONTENT_TYPE),
            new DataParameter(StatsContentSql.KindParameter, ClassContentTypeType.WEBSITE),
            new DataParameter(PageFreshnessSql.PublishedStatusParameter, (int)VersionStatus.Published),
            new DataParameter(PageFreshnessSql.PageVisitTypeParameter, PredefinedActivityType.PAGE_VISIT),
            new DataParameter(PageFreshnessSql.FromParameter, query.From.ToDateTime(TimeOnly.MinValue)),
            new DataParameter(PageFreshnessSql.ToExclusiveParameter, query.To.AddDays(1).ToDateTime(TimeOnly.MinValue)),
            new DataParameter(PageFreshnessSql.LimitParameter, PageFreshnessReportBuilder.ListLimit),
        };
        foreach (var parameter in ContentInventorySql.GetAgeParameters(now))
        {
            parameters.Add(parameter);
        }
        if (query.ChannelId is int channelId)
        {
            parameters.Add(new DataParameter(StatsContentSql.ChannelParameter, channelId));
        }

        string sql = PageFreshnessSql.Build(query.ChannelId is not null);

        await using var reader = await ConnectionHelper.ExecuteReaderAsync(sql, parameters, QueryTypeEnum.SQLQuery, CommandBehavior.Default, cancellationToken);

        var totals = await ReadTotals(reader, cancellationToken);
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var (agePages, ageVisits) = await ReadAge(reader, cancellationToken);
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var stalePopular = await ReadRows(reader, cancellationToken);
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var noVisits = await ReadRows(reader, cancellationToken);

        return new(totals, agePages, ageVisits, stalePopular, noVisits);
    }

    private static async Task<PageFreshnessTotals> ReadTotals(DbDataReader reader, CancellationToken cancellationToken)
    {
        if (!await reader.ReadAsync(cancellationToken))
        {
            return PageFreshnessTotals.Empty;
        }

        return new(
            reader.GetInt32(reader.GetOrdinal("PublishedPages")),
            reader.GetInt32(reader.GetOrdinal("StalePages")),
            reader.GetInt32(reader.GetOrdinal("Visits")),
            reader.GetInt32(reader.GetOrdinal("StaleVisits")),
            reader.GetInt32(reader.GetOrdinal("StalePopularPages")),
            reader.GetInt32(reader.GetOrdinal("NoVisitPages")));
    }

    private static async Task<(ContentAgeRow Pages, ContentAgeRow Visits)> ReadAge(DbDataReader reader, CancellationToken cancellationToken)
    {
        if (!await reader.ReadAsync(cancellationToken))
        {
            return (ContentAgeRow.Empty, ContentAgeRow.Empty);
        }

        return (ContentAgeRow.Read(reader), ContentAgeRow.Read(reader, PageFreshnessSql.VisitsSuffix));
    }

    private static async Task<IReadOnlyList<PageFreshnessRow>> ReadRows(DbDataReader reader, CancellationToken cancellationToken)
    {
        int variantOrdinal = reader.GetOrdinal("VariantID");
        int pageOrdinal = reader.GetOrdinal("WebPageItemID");
        int websiteChannelOrdinal = reader.GetOrdinal("WebsiteChannelID");
        int nameOrdinal = reader.GetOrdinal("DisplayName");
        int channelOrdinal = reader.GetOrdinal("ChannelDisplayName");
        int languageNameOrdinal = reader.GetOrdinal("ContentLanguageName");
        int languageOrdinal = reader.GetOrdinal("ContentLanguageDisplayName");
        int treePathOrdinal = reader.GetOrdinal("TreePath");
        int modifiedOrdinal = reader.GetOrdinal("ModifiedWhen");
        int firstPublishedOrdinal = reader.GetOrdinal("FirstPublishedWhen");
        int visitsOrdinal = reader.GetOrdinal("Visits");
        int visitorsOrdinal = reader.GetOrdinal("Visitors");

        var rows = new List<PageFreshnessRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                reader.GetInt32(variantOrdinal),
                reader.GetInt32(pageOrdinal),
                reader.GetInt32(websiteChannelOrdinal),
                reader.GetString(nameOrdinal),
                reader.IsDBNull(channelOrdinal) ? null : reader.GetString(channelOrdinal),
                reader.GetString(languageNameOrdinal),
                reader.GetString(languageOrdinal),
                reader.IsDBNull(treePathOrdinal) ? string.Empty : reader.GetString(treePathOrdinal),
                reader.GetDateTime(modifiedOrdinal),
                reader.IsDBNull(firstPublishedOrdinal) ? null : reader.GetDateTime(firstPublishedOrdinal),
                reader.GetInt32(visitsOrdinal),
                reader.GetInt32(visitorsOrdinal)));
        }

        return rows;
    }
}
