using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.CampaignSources;

/// <summary>
/// Builds the campaign sources batch (see <see cref="Build"/>). Only constant SQL fragments are combined; all values are parameters.
/// </summary>
/// <remarks>
/// <para>
/// Landings: landing page activities (<c>PredefinedActivityType.LANDING_PAGE</c>) from the start of the previous period to the end of
/// the range, optionally of one website channel (<c>ActivityChannelID</c>), copied once into the <c>@Landings</c> table with the trimmed
/// UTM source and content (<see cref="StatsUtm"/>, as the web page "Stats (Labs)" tab). A landing with a source is a campaign landing.
/// </para>
/// <para>
/// Selected: campaign landings of the source and content filter. KPIs, the series and the pages follow it; the sources list does not
/// (it is the filter's option list), and the source and content list follows the source only.
/// </para>
/// <para>
/// Contact IDs stay in the table variable; the result sets have aggregates only.
/// </para>
/// </remarks>
internal static class CampaignSourcesSql
{
    public const string LandingPageTypeParameter = "@LandingPageType";
    public const string PreviousFromParameter = "@PreviousFrom";
    public const string FromParameter = "@From";
    public const string ToExclusiveParameter = "@ToExclusive";
    public const string ChannelParameter = "@ChannelID";
    public const string SourceParameter = "@Source";
    public const string ContentParameter = "@Content";

    /// <summary>Sources shown as their own series; the rest is "Other".</summary>
    public const string SeriesLimitParameter = "@SeriesLimit";

    /// <summary>Rows of the sources list.</summary>
    public const string SourceLimitParameter = "@SourceLimit";

    /// <summary>Rows of the pages list.</summary>
    public const string PageLimitParameter = "@PageLimit";

    /// <summary>Rows of the source and content list.</summary>
    public const string ContentLimitParameter = "@ContentLimit";

    // {0} = channel condition.
    private const string LandingsTable = $$"""
        DECLARE @Landings TABLE (
            [IsCurrent] bit NOT NULL,
            [Day] date NOT NULL,
            [ContactID] int NOT NULL,
            [PageGUID] uniqueidentifier NULL,
            [LanguageID] int NULL,
            [Source] nvarchar(200) NULL,
            [Content] nvarchar(200) NULL,
            [Url] nvarchar(max) NULL
        );

        INSERT INTO @Landings ([IsCurrent], [Day], [ContactID], [PageGUID], [LanguageID], [Source], [Content], [Url])
        SELECT
            CASE WHEN A.[ActivityCreated] >= @From THEN 1 ELSE 0 END,
            CAST(A.[ActivityCreated] AS date),
            A.[ActivityContactID],
            A.[ActivityWebPageItemGUID],
            A.[ActivityLanguageID],
            {{StatsUtm.SourceColumn}},
            {{StatsUtm.ContentColumn}},
            -- Only needed for page links of campaign landings in the range.
            CASE WHEN A.[ActivityCreated] >= @From AND A.[ActivityUTMSource] <> N'' THEN {{StatsSql.ActivityUrlWithoutQuery}} END
        FROM [OM_Activity] A
        {{StatsSql.ActivityUrlCutApply}}
        WHERE A.[ActivityType] = @LandingPageType
            AND A.[ActivityCreated] >= @PreviousFrom
            AND A.[ActivityCreated] < @ToExclusive{0};
        """;

    private const string ChannelCondition = """

            AND A.[ActivityChannelID] = @ChannelID
        """;

    // 1. Totals (one row). {0} = selected condition.
    private const string TotalsQuery = """
        SELECT
            ISNULL(SUM(CASE WHEN L.[IsCurrent] = 1 THEN 1 ELSE 0 END), 0) AS [Landings],
            ISNULL(SUM(CASE WHEN L.[IsCurrent] = 0 THEN 1 ELSE 0 END), 0) AS [PreviousLandings],
            ISNULL(SUM(CASE WHEN L.[IsCurrent] = 1 AND L.[Source] IS NOT NULL THEN 1 ELSE 0 END), 0) AS [AllCampaignLandings],
            ISNULL(SUM(CASE WHEN L.[IsCurrent] = 1 AND {0} THEN 1 ELSE 0 END), 0) AS [CampaignLandings],
            ISNULL(SUM(CASE WHEN L.[IsCurrent] = 0 AND {0} THEN 1 ELSE 0 END), 0) AS [PreviousCampaignLandings],
            COUNT(DISTINCT CASE WHEN L.[IsCurrent] = 1 AND {0} THEN L.[ContactID] END) AS [CampaignVisitors],
            COUNT(DISTINCT CASE WHEN L.[IsCurrent] = 1 THEN L.[Source] END) AS [Sources],
            COUNT(DISTINCT CASE WHEN L.[IsCurrent] = 0 THEN L.[Source] END) AS [PreviousSources]
        FROM @Landings L;
        """;

    // 2. Selected campaign landings per day in the range, by the top sources; the other sources are one NULL source ("Other").
    // {0} = selected condition.
    private const string DailyQuery = """
        WITH [TopSources] AS (
            SELECT TOP (@SeriesLimit) L.[Source]
            FROM @Landings L
            WHERE L.[IsCurrent] = 1 AND {0}
            GROUP BY L.[Source]
            ORDER BY COUNT(*) DESC, L.[Source]
        )
        SELECT
            T.[Source] AS [Source],
            L.[Day] AS [Day],
            COUNT(*) AS [Landings]
        FROM @Landings L
        LEFT JOIN [TopSources] T ON T.[Source] = L.[Source]
        WHERE L.[IsCurrent] = 1 AND {0}
        GROUP BY T.[Source], L.[Day];
        """;

    // 3. Sources in the range (all, not the selected only), with the previous period.
    private const string SourcesQuery = """
        SELECT TOP (@SourceLimit)
            L.[Source] AS [Source],
            SUM(CASE WHEN L.[IsCurrent] = 1 THEN 1 ELSE 0 END) AS [Landings],
            COUNT(DISTINCT CASE WHEN L.[IsCurrent] = 1 THEN L.[ContactID] END) AS [Visitors],
            SUM(CASE WHEN L.[IsCurrent] = 0 THEN 1 ELSE 0 END) AS [PreviousLandings]
        FROM @Landings L
        WHERE L.[Source] IS NOT NULL
        GROUP BY L.[Source]
        HAVING SUM(CASE WHEN L.[IsCurrent] = 1 THEN 1 ELSE 0 END) > 0
        ORDER BY [Landings] DESC, L.[Source];
        """;

    // 4. Landing pages of the selected campaign landings in the range, with the page name in the landing's language and its channel.
    // {0} = selected condition.
    private const string PagesQuery = """
        WITH [Pages] AS (
            SELECT
                L.[PageGUID],
                L.[LanguageID],
                COUNT(*) AS [Landings],
                COUNT(DISTINCT L.[ContactID]) AS [Visitors],
                MAX(L.[Url]) AS [Url],
                COUNT(*) OVER () AS [PageCount]
            FROM @Landings L
            WHERE L.[IsCurrent] = 1 AND {0}
            GROUP BY L.[PageGUID], L.[LanguageID]
        )
        SELECT TOP (@PageLimit)
            X.[PageGUID],
            X.[LanguageID],
            X.[Landings],
            X.[Visitors],
            X.[Url],
            X.[PageCount],
            M.[ContentItemLanguageMetadataDisplayName] AS [DisplayName],
            CL.[ContentLanguageDisplayName] AS [Language],
            CH.[ChannelDisplayName] AS [Channel]
        FROM [Pages] X
        LEFT JOIN [CMS_WebPageItem] P ON P.[WebPageItemGUID] = X.[PageGUID]
        LEFT JOIN [CMS_ContentItemLanguageMetadata] M
            ON M.[ContentItemLanguageMetadataContentItemID] = P.[WebPageItemContentItemID]
            AND M.[ContentItemLanguageMetadataContentLanguageID] = X.[LanguageID]
        LEFT JOIN [CMS_ContentLanguage] CL ON CL.[ContentLanguageID] = X.[LanguageID]
        LEFT JOIN [CMS_WebsiteChannel] W ON W.[WebsiteChannelID] = P.[WebPageItemWebsiteChannelID]
        LEFT JOIN [CMS_Channel] CH ON CH.[ChannelID] = W.[WebsiteChannelChannelID]
        ORDER BY X.[Landings] DESC, X.[Visitors] DESC, M.[ContentItemLanguageMetadataDisplayName], X.[Url];
        """;

    // 5. Source and content pairs in the range (of the selected source; not limited by the content filter). {0} = source condition.
    private const string ContentsQuery = """
        SELECT TOP (@ContentLimit)
            L.[Source] AS [Source],
            L.[Content] AS [Content],
            COUNT(*) AS [Landings],
            COUNT(DISTINCT L.[ContactID]) AS [Visitors],
            COUNT(*) OVER () AS [GroupCount]
        FROM @Landings L
        WHERE L.[IsCurrent] = 1 AND {0}
        GROUP BY L.[Source], L.[Content]
        ORDER BY COUNT(*) DESC, L.[Source], L.[Content];
        """;

    /// <summary>
    /// Returns the batch. Add <see cref="ChannelParameter"/>, <see cref="SourceParameter"/> and <see cref="ContentParameter"/> only when
    /// the matching flag is set; all other parameters of this class are always used.
    /// </summary>
    /// <remarks>Result sets, in order: totals (one row), daily series, sources, pages, source and content pairs.</remarks>
    /// <param name="hasChannel">Filter by website channel.</param>
    /// <param name="hasSource">Filter by source.</param>
    /// <param name="content">
    /// Content filter (needs <paramref name="hasSource"/>): <see cref="CampaignContentFilter.All"/>, <see cref="CampaignContentFilter.Value"/>
    /// (equal to the parameter) or <see cref="CampaignContentFilter.None"/> (no content).
    /// </param>
    public static string Build(bool hasChannel, bool hasSource, CampaignContentFilter content)
    {
        string sourceCondition = "L.[Source] IS NOT NULL" + (hasSource ? " AND L.[Source] = " + SourceParameter : string.Empty);
        string selectedCondition = sourceCondition + (hasSource
            ? content switch
            {
                CampaignContentFilter.All => string.Empty,
                CampaignContentFilter.Value => " AND L.[Content] = " + ContentParameter,
                CampaignContentFilter.None => " AND L.[Content] IS NULL",
                _ => throw new ArgumentOutOfRangeException(nameof(content), content, null),
            }
            : string.Empty);

        return string.Join(
            Environment.NewLine,
            "SET NOCOUNT ON;",
            string.Format(null, LandingsTable, hasChannel ? ChannelCondition : string.Empty),
            string.Format(null, TotalsQuery, "(" + selectedCondition + ")"),
            string.Format(null, DailyQuery, "(" + selectedCondition + ")"),
            SourcesQuery,
            string.Format(null, PagesQuery, "(" + selectedCondition + ")"),
            string.Format(null, ContentsQuery, "(" + sourceCondition + ")"));
    }
}

/// <summary>
/// Kind of content filter of the campaign sources batch.
/// </summary>
internal enum CampaignContentFilter
{
    /// <summary>All contents.</summary>
    All,

    /// <summary>One content value.</summary>
    Value,

    /// <summary>Landings without a content.</summary>
    None,
}
