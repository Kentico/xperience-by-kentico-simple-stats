using System.Data;

using CMS.Activities;
using CMS.DataEngine;

using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.TopPages;

/// <summary>
/// Reads aggregated page visits from the database.
/// </summary>
internal interface ITopPagesRepository
{
    /// <summary>
    /// Returns the most visited URLs and the range totals.
    /// </summary>
    public Task<TopPagesData> GetTopPages(DateOnly from, DateOnly to, int? channelId, int limit, CancellationToken cancellationToken);
}

internal sealed class TopPagesRepository : ITopPagesRepository
{
    // Only constant SQL fragments are combined here. All values are passed as parameters.
    // The URL is cut at the first '?' or '#' (see StatsSql.ActivityUrlWithoutQuery).
    // Window aggregates run over all grouped URLs before TOP, so totals cover the whole range in one query.
    private const string SelectClause = $$"""
        WITH [Visits] AS (
            SELECT
                ISNULL({{StatsSql.ActivityUrlWithoutQuery}}, N'') AS [PageURL],
                A.[ActivityContactID],
                A.[ActivityTitle]
            FROM [OM_Activity] A
            {{StatsSql.ActivityUrlCutApply}}
            WHERE A.[ActivityType] = @ActivityType
                AND A.[ActivityCreated] >= @From
                AND A.[ActivityCreated] < @ToExclusive
        """;

    private const string ChannelClause = """

                AND A.[ActivityChannelID] = @ChannelID
        """;

    private const string GroupClause = """

        ),
        [Pages] AS (
            SELECT
                [PageURL],
                COUNT(*) AS [VisitCount],
                COUNT(DISTINCT [ActivityContactID]) AS [ContactCount],
                MAX([ActivityTitle]) AS [PageTitle]
            FROM [Visits]
            GROUP BY [PageURL]
        )
        SELECT TOP (@Limit)
            [PageURL],
            [VisitCount],
            [ContactCount],
            [PageTitle],
            SUM([VisitCount]) OVER () AS [TotalVisits],
            COUNT(*) OVER () AS [PageCount]
        FROM [Pages]
        ORDER BY [VisitCount] DESC, [PageURL]
        """;

    public async Task<TopPagesData> GetTopPages(DateOnly from, DateOnly to, int? channelId, int limit, CancellationToken cancellationToken)
    {
        var parameters = new QueryDataParameters
        {
            new DataParameter("@ActivityType", PredefinedActivityType.PAGE_VISIT),
            new DataParameter("@From", from.ToDateTime(TimeOnly.MinValue)),
            new DataParameter("@ToExclusive", to.AddDays(1).ToDateTime(TimeOnly.MinValue)),
            new DataParameter("@Limit", limit),
        };

        string query = SelectClause;
        if (channelId is int id)
        {
            parameters.Add(new DataParameter("@ChannelID", id));
            query += ChannelClause;
        }

        query += GroupClause;

        await using var reader = await ConnectionHelper.ExecuteReaderAsync(query, parameters, QueryTypeEnum.SQLQuery, CommandBehavior.Default, cancellationToken);

        var rows = new List<TopPageRow>();
        int totalVisits = 0;
        int pageCount = 0;

        int urlOrdinal = reader.GetOrdinal("PageURL");
        int visitsOrdinal = reader.GetOrdinal("VisitCount");
        int contactsOrdinal = reader.GetOrdinal("ContactCount");
        int titleOrdinal = reader.GetOrdinal("PageTitle");
        int totalOrdinal = reader.GetOrdinal("TotalVisits");
        int pageCountOrdinal = reader.GetOrdinal("PageCount");

        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                reader.IsDBNull(urlOrdinal) ? string.Empty : reader.GetString(urlOrdinal),
                reader.GetInt32(visitsOrdinal),
                reader.GetInt32(contactsOrdinal),
                reader.IsDBNull(titleOrdinal) ? null : reader.GetString(titleOrdinal)));

            // Same value on every row.
            totalVisits = reader.GetInt32(totalOrdinal);
            pageCount = reader.GetInt32(pageCountOrdinal);
        }

        return new(rows, totalVisits, pageCount);
    }
}
