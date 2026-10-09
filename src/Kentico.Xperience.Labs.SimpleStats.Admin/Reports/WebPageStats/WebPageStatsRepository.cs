using System.Data;
using System.Data.Common;

using CMS.Activities;
using CMS.DataEngine;

using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.WebPageStats;

/// <summary>
/// Reads aggregated activities of one web page from the database.
/// </summary>
internal interface IWebPageStatsRepository
{
    /// <summary>
    /// Returns activity counts per type and day and distinct contact counts. Never returns contact identifiers.
    /// </summary>
    public Task<WebPageStatsData> GetData(WebPageStatsTarget target, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}

internal sealed class WebPageStatsRepository : IWebPageStatsRepository
{
    // Constant SQL, all values are parameters. Only aggregates leave the database, so no contact can be told apart in the report.
    //
    // Activities of the page:
    // - all types except form submissions, linked by ActivityWebPageItemGUID and the language,
    // - form submissions, which have no page link or language: matched by ActivityURL in the page's channel.
    //   The URL path is cut like WebPageStatsUrlPath.Normalize (no scheme and host, query string or fragment), and
    //   compared with and without one trailing slash. The database collation decides letter case (case-insensitive by default).
    //   With language-specific domains all languages share the same paths, so the URL host (with port) must also be one of
    //   the language's domains. @FormUrlHosts is the list delimited and wrapped by '|' (which hosts cannot contain), so one
    //   constant CHARINDEX check works for any number of hosts without STRING_SPLIT (database compatibility level 130+).
    //
    // The grouping set () adds one total row (IsTotal = 1) with distinct contact counts over the whole range.
    internal const string Query = """
        WITH [PageActivities] AS (
            SELECT A.[ActivityType], A.[ActivityCreated], A.[ActivityContactID]
            FROM [OM_Activity] A
            WHERE A.[ActivityWebPageItemGUID] = @WebPageItemGUID
                AND A.[ActivityLanguageID] = @LanguageID
                AND A.[ActivityType] <> @FormSubmitType
                AND A.[ActivityCreated] >= @From
                AND A.[ActivityCreated] < @ToExclusive
            UNION ALL
            SELECT A.[ActivityType], A.[ActivityCreated], A.[ActivityContactID]
            FROM [OM_Activity] A
            CROSS APPLY (SELECT CHARINDEX(N'://', A.[ActivityURL]) AS [SchemeEnd]) S
            CROSS APPLY (SELECT CASE WHEN S.[SchemeEnd] > 0 THEN CHARINDEX(N'/', A.[ActivityURL], S.[SchemeEnd] + 3) ELSE 1 END AS [PathStart]) P
            CROSS APPLY (SELECT CASE WHEN P.[PathStart] > 0 THEN SUBSTRING(A.[ActivityURL], P.[PathStart], 4000) ELSE N'' END AS [RawPath]) R
            CROSS APPLY (SELECT PATINDEX(N'%[?#]%', R.[RawPath]) AS [Cut]) U
            CROSS APPLY (SELECT CASE WHEN U.[Cut] > 0 THEN LEFT(R.[RawPath], U.[Cut] - 1) ELSE R.[RawPath] END AS [UrlPath]) Q
            CROSS APPLY (SELECT CASE WHEN S.[SchemeEnd] > 0 THEN SUBSTRING(A.[ActivityURL], S.[SchemeEnd] + 3, 4000) + N'/' ELSE N'' END AS [AfterScheme]) HR
            CROSS APPLY (SELECT CASE WHEN S.[SchemeEnd] > 0 THEN LEFT(HR.[AfterScheme], PATINDEX(N'%[/?#]%', HR.[AfterScheme]) - 1) ELSE N'' END AS [UrlHost]) H
            WHERE @MatchForms = 1
                AND A.[ActivityType] = @FormSubmitType
                AND A.[ActivityChannelID] = @ChannelID
                AND A.[ActivityCreated] >= @From
                AND A.[ActivityCreated] < @ToExclusive
                AND (Q.[UrlPath] = @FormUrlPath OR Q.[UrlPath] = @FormUrlPath + N'/')
                AND (@MatchHosts = 0 OR CHARINDEX(N'|' + H.[UrlHost] + N'|', @FormUrlHosts) > 0)
        )
        SELECT
            GROUPING(A.[ActivityType]) AS [IsTotal],
            A.[ActivityType] AS [ActivityType],
            CAST(A.[ActivityCreated] AS date) AS [ActivityDate],
            COUNT(*) AS [ActivityCount],
            COUNT(DISTINCT A.[ActivityContactID]) AS [ContactCount],
            COUNT(DISTINCT CASE WHEN A.[ActivityType] = @PageVisitType THEN A.[ActivityContactID] END) AS [VisitorCount],
            COUNT(DISTINCT CASE WHEN A.[ActivityType] = @FormSubmitType THEN A.[ActivityContactID] END) AS [SubmitterCount]
        FROM [PageActivities] A
        GROUP BY GROUPING SETS ((A.[ActivityType], CAST(A.[ActivityCreated] AS date)), ());
        """;

    // Campaign sources: landing page activities of the variant (same page, language and range match as above) with their
    // UTM values, trimmed, empty as NULL. Contact IDs stay in the table variable; the result sets have aggregates only.
    // Result sets: totals (one row), top sources, top source + content pairs (with the number of all pairs).
    internal const string CampaignQuery = $$"""
        DECLARE @Landings TABLE ([ContactID] int NULL, [Source] nvarchar(200) NULL, [Content] nvarchar(200) NULL);

        INSERT INTO @Landings ([ContactID], [Source], [Content])
        SELECT
            A.[ActivityContactID],
            {{StatsUtm.SourceColumn}},
            {{StatsUtm.ContentColumn}}
        FROM [OM_Activity] A
        WHERE A.[ActivityWebPageItemGUID] = @WebPageItemGUID
            AND A.[ActivityLanguageID] = @LanguageID
            AND A.[ActivityType] = @LandingPageType
            AND A.[ActivityCreated] >= @From
            AND A.[ActivityCreated] < @ToExclusive;

        SELECT
            COUNT(*) AS [Landings],
            COUNT(L.[Source]) AS [CampaignLandings],
            COUNT(DISTINCT CASE WHEN L.[Source] IS NOT NULL THEN L.[ContactID] END) AS [CampaignVisitors],
            COUNT(DISTINCT L.[Source]) AS [SourceCount]
        FROM @Landings L;

        SELECT TOP (@SourceLimit)
            L.[Source] AS [Source],
            COUNT(*) AS [Landings],
            COUNT(DISTINCT L.[ContactID]) AS [Visitors]
        FROM @Landings L
        WHERE L.[Source] IS NOT NULL
        GROUP BY L.[Source]
        ORDER BY COUNT(*) DESC, L.[Source];

        SELECT TOP (@SourceContentLimit)
            L.[Source] AS [Source],
            L.[Content] AS [Content],
            COUNT(*) AS [Landings],
            COUNT(*) OVER () AS [GroupCount]
        FROM @Landings L
        WHERE L.[Source] IS NOT NULL
        GROUP BY L.[Source], L.[Content]
        ORDER BY COUNT(*) DESC, L.[Source], L.[Content];
        """;

    /// <summary>
    /// <see cref="Query"/> and <see cref="CampaignQuery"/> in one round trip.
    /// </summary>
    internal const string Batch = Query + "\n" + CampaignQuery;

    public async Task<WebPageStatsData> GetData(WebPageStatsTarget target, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        string hosts = FormatHosts(target.FormUrlHosts);
        var parameters = new QueryDataParameters
        {
            new DataParameter("@WebPageItemGUID", target.WebPageItemGuid),
            new DataParameter("@LanguageID", target.LanguageId),
            new DataParameter("@ChannelID", target.ChannelId),
            new DataParameter("@MatchForms", target.FormUrlPath is not null),
            new DataParameter("@FormUrlPath", target.FormUrlPath ?? string.Empty),
            new DataParameter("@MatchHosts", hosts.Length > 0),
            new DataParameter("@FormUrlHosts", hosts),
            new DataParameter("@From", from.ToDateTime(TimeOnly.MinValue)),
            new DataParameter("@ToExclusive", to.AddDays(1).ToDateTime(TimeOnly.MinValue)),
            new DataParameter("@PageVisitType", PredefinedActivityType.PAGE_VISIT),
            new DataParameter("@FormSubmitType", PredefinedActivityType.BIZFORM_SUBMIT),
            new DataParameter("@LandingPageType", PredefinedActivityType.LANDING_PAGE),
            new DataParameter("@SourceLimit", WebPageStatsReportBuilder.SourceLimit),
            new DataParameter("@SourceContentLimit", WebPageStatsReportBuilder.SourceContentLimit),
        };

        await using var reader = await ConnectionHelper.ExecuteReaderAsync(Batch, parameters, QueryTypeEnum.SQLQuery, CommandBehavior.Default, cancellationToken);

        var rows = new List<StatsDailyCount>();
        int contacts = 0;
        int visitors = 0;
        int submitters = 0;

        int totalOrdinal = reader.GetOrdinal("IsTotal");
        int typeOrdinal = reader.GetOrdinal("ActivityType");
        int dateOrdinal = reader.GetOrdinal("ActivityDate");
        int countOrdinal = reader.GetOrdinal("ActivityCount");
        int contactsOrdinal = reader.GetOrdinal("ContactCount");
        int visitorsOrdinal = reader.GetOrdinal("VisitorCount");
        int submittersOrdinal = reader.GetOrdinal("SubmitterCount");

        while (await reader.ReadAsync(cancellationToken))
        {
            // GROUPING() returns tinyint.
            if (Convert.ToInt32(reader.GetValue(totalOrdinal)) == 1)
            {
                contacts = reader.GetInt32(contactsOrdinal);
                visitors = reader.GetInt32(visitorsOrdinal);
                submitters = reader.GetInt32(submittersOrdinal);
                continue;
            }

            rows.Add(new(
                reader.IsDBNull(typeOrdinal) ? string.Empty : reader.GetString(typeOrdinal),
                DateOnly.FromDateTime(reader.GetDateTime(dateOrdinal)),
                reader.GetInt32(countOrdinal)));
        }

        return new(rows, contacts, visitors, submitters)
        {
            Campaigns = await ReadCampaigns(reader, cancellationToken),
        };
    }

    private static async Task<WebPageCampaignData> ReadCampaigns(DbDataReader reader, CancellationToken cancellationToken)
    {
        // The INSERT into @Landings returns no result set, so the totals follow the activity result set.
        await StatsSql.NextResult(reader, "web page stats", cancellationToken);
        int landings = 0;
        int campaignLandings = 0;
        int campaignVisitors = 0;
        int sourceCount = 0;
        if (await reader.ReadAsync(cancellationToken))
        {
            landings = reader.GetInt32(reader.GetOrdinal("Landings"));
            campaignLandings = reader.GetInt32(reader.GetOrdinal("CampaignLandings"));
            campaignVisitors = reader.GetInt32(reader.GetOrdinal("CampaignVisitors"));
            sourceCount = reader.GetInt32(reader.GetOrdinal("SourceCount"));
        }

        await StatsSql.NextResult(reader, "web page stats", cancellationToken);
        var sources = new List<WebPageCampaignSourceRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            sources.Add(new(
                reader.GetString(reader.GetOrdinal("Source")),
                reader.GetInt32(reader.GetOrdinal("Landings")),
                reader.GetInt32(reader.GetOrdinal("Visitors"))));
        }

        await StatsSql.NextResult(reader, "web page stats", cancellationToken);
        var contents = new List<WebPageCampaignContentRow>();
        int contentCount = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            int contentOrdinal = reader.GetOrdinal("Content");
            contents.Add(new(
                reader.GetString(reader.GetOrdinal("Source")),
                reader.IsDBNull(contentOrdinal) ? null : reader.GetString(contentOrdinal),
                reader.GetInt32(reader.GetOrdinal("Landings"))));

            // Same value on every row.
            contentCount = reader.GetInt32(reader.GetOrdinal("GroupCount"));
        }

        return new(landings, campaignLandings, campaignVisitors, sourceCount, sources, contentCount, contents);
    }

    /// <summary>
    /// Returns the hosts as <c>|host1|host2|</c> for the <c>@FormUrlHosts</c> parameter, or an empty string for none.
    /// Hosts with a '|' (not a valid host character) are skipped.
    /// </summary>
    internal static string FormatHosts(IReadOnlyList<string> hosts) => StatsSql.FormatDelimitedList(hosts);
}
