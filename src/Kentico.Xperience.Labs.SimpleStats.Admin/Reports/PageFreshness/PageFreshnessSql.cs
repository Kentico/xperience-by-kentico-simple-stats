using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContentInventory;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PageFreshness;

/// <summary>
/// Builds the page freshness batch: totals, age buckets and two lists in one round trip (see <see cref="Build"/>).
/// Only constant SQL fragments are combined; all values are parameters.
/// </summary>
/// <remarks>
/// <para>
/// Pages: language variants (<c>CMS_ContentItemLanguageMetadata</c>) of website items (<see cref="StatsContentSql"/> items with
/// the website kind, so page folders are left out) whose content type has a URL (<c>ClassWebPageHasUrl</c>; pages without a URL,
/// for example navigation items, cannot be visited), joined to their <c>CMS_WebPageItem</c>. The channel filter is the item's channel.
/// </para>
/// <para>
/// Published: the variant has a <c>CMS_ContentItemCommonData</c> row with the published <c>VersionStatus</c> (a newer draft keeps it).
/// First publish: that row's <c>ContentItemCommonDataFirstPublishedWhen</c>, or its last publish when the first is not stored.
/// Last change: <c>ContentItemLanguageMetadataModifiedWhen</c>, as in the content inventory (age buckets and stale threshold from
/// <see cref="ContentInventorySql"/>).
/// </para>
/// <para>
/// Visits: page visit activities in the range, matched by <c>ActivityWebPageItemGUID</c> and <c>ActivityLanguageID</c> (as the web page "Stats (Labs)" tab),
/// aggregated once per page and language into the <c>@Pages</c> table, which the result sets read.
/// </para>
/// </remarks>
internal static class PageFreshnessSql
{
    public const string PublishedStatusParameter = "@PublishedStatus";
    public const string PageVisitTypeParameter = "@PageVisitType";
    public const string FromParameter = "@From";
    public const string ToExclusiveParameter = "@ToExclusive";

    /// <summary>Maximum number of rows of each list.</summary>
    public const string LimitParameter = "@Limit";

    // Stale: not changed since the content inventory's stale threshold (the start of its "Over 12 months" bucket).
    private const string Stale = "X.[ModifiedWhen] < " + ContentInventorySql.Age12Parameter;

    // No visits in the range, first published before it (or unknown, which only old imported data has).
    private const string NoVisits = "X.[Visits] = 0 AND (X.[FirstPublishedWhen] IS NULL OR X.[FirstPublishedWhen] < " + FromParameter + ")";

    // {0} = items WHERE clause.
    private const string PagesTable = """
        DECLARE @Pages TABLE (
            [VariantID] int NOT NULL,
            [WebPageItemID] int NOT NULL,
            [ModifiedWhen] datetime2(7) NOT NULL,
            [FirstPublishedWhen] datetime2(7) NULL,
            [Visits] int NOT NULL,
            [Visitors] int NOT NULL,
            PRIMARY KEY ([VariantID], [WebPageItemID])
        );

        WITH [Visits] AS (
            SELECT
                A.[ActivityWebPageItemGUID] AS [PageGUID],
                A.[ActivityLanguageID] AS [LanguageID],
                COUNT(*) AS [Visits],
                COUNT(DISTINCT A.[ActivityContactID]) AS [Visitors]
            FROM [OM_Activity] A
            WHERE A.[ActivityType] = @PageVisitType
                AND A.[ActivityCreated] >= @From
                AND A.[ActivityCreated] < @ToExclusive
                AND A.[ActivityWebPageItemGUID] IS NOT NULL
            GROUP BY A.[ActivityWebPageItemGUID], A.[ActivityLanguageID]
        )
        INSERT INTO @Pages ([VariantID], [WebPageItemID], [ModifiedWhen], [FirstPublishedWhen], [Visits], [Visitors])
        SELECT
            M.[ContentItemLanguageMetadataID],
            P.[WebPageItemID],
            M.[ContentItemLanguageMetadataModifiedWhen],
            D.[FirstPublishedWhen],
            ISNULL(V.[Visits], 0),
            ISNULL(V.[Visitors], 0)
        {1}
        INNER JOIN [CMS_WebPageItem] P ON P.[WebPageItemContentItemID] = I.[ContentItemID]
        CROSS APPLY (
            SELECT TOP (1)
                COALESCE(D1.[ContentItemCommonDataFirstPublishedWhen], D1.[ContentItemCommonDataLastPublishedWhen]) AS [FirstPublishedWhen]
            FROM [CMS_ContentItemCommonData] D1
            WHERE D1.[ContentItemCommonDataContentItemID] = M.[ContentItemLanguageMetadataContentItemID]
                AND D1.[ContentItemCommonDataContentLanguageID] = M.[ContentItemLanguageMetadataContentLanguageID]
                AND D1.[ContentItemCommonDataVersionStatus] = @PublishedStatus
            ORDER BY D1.[ContentItemCommonDataID]
        ) D
        LEFT JOIN [Visits] V
            ON V.[PageGUID] = P.[WebPageItemGUID]
            AND V.[LanguageID] = M.[ContentItemLanguageMetadataContentLanguageID]
        {0}
            AND C.[ClassWebPageHasUrl] = 1;
        """;

    // 1. Totals (one row), not limited by @Limit.
    private const string TotalsQuery = $$"""
        SELECT
            COUNT(*) AS [PublishedPages],
            ISNULL(SUM(CASE WHEN {{Stale}} THEN 1 ELSE 0 END), 0) AS [StalePages],
            ISNULL(SUM(X.[Visits]), 0) AS [Visits],
            ISNULL(SUM(CASE WHEN {{Stale}} THEN X.[Visits] ELSE 0 END), 0) AS [StaleVisits],
            ISNULL(SUM(CASE WHEN {{Stale}} AND X.[Visits] > 0 THEN 1 ELSE 0 END), 0) AS [StalePopularPages],
            ISNULL(SUM(CASE WHEN {{NoVisits}} THEN 1 ELSE 0 END), 0) AS [NoVisitPages]
        FROM @Pages X;
        """;

    // 2. Variants and visits per age of the last change (one row). {0} = variant columns, {1} = visit columns.
    private const string AgeQuery = """
        SELECT
            {0},
            {1}
        FROM @Pages X;
        """;

    // Columns and joins of the lists.
    private const string ListColumns = """
        X.[VariantID],
                X.[WebPageItemID],
                P.[WebPageItemWebsiteChannelID] AS [WebsiteChannelID],
                M.[ContentItemLanguageMetadataDisplayName] AS [DisplayName],
                CH.[ChannelDisplayName],
                L.[ContentLanguageName],
                L.[ContentLanguageDisplayName],
                P.[WebPageItemTreePath] AS [TreePath],
                X.[ModifiedWhen],
                X.[FirstPublishedWhen],
                X.[Visits],
                X.[Visitors]
            FROM @Pages X
            INNER JOIN [CMS_ContentItemLanguageMetadata] M ON M.[ContentItemLanguageMetadataID] = X.[VariantID]
            INNER JOIN [CMS_ContentItem] I ON I.[ContentItemID] = M.[ContentItemLanguageMetadataContentItemID]
            INNER JOIN [CMS_ContentLanguage] L ON L.[ContentLanguageID] = M.[ContentItemLanguageMetadataContentLanguageID]
            INNER JOIN [CMS_WebPageItem] P ON P.[WebPageItemID] = X.[WebPageItemID]
            LEFT JOIN [CMS_Channel] CH ON CH.[ChannelID] = I.[ContentItemChannelID]
        """;

    // 3. Stale variants with visits, most visits first.
    private const string StalePopularQuery = $$"""
        SELECT TOP (@Limit)
            {{ListColumns}}
        WHERE {{Stale}} AND X.[Visits] > 0
        ORDER BY X.[Visits] DESC, X.[Visitors] DESC, X.[VariantID];
        """;

    // 4. Variants without visits, first published longest ago first (unknown first).
    private const string NoVisitsQuery = $$"""
        SELECT TOP (@Limit)
            {{ListColumns}}
        WHERE {{NoVisits}}
        ORDER BY X.[FirstPublishedWhen], X.[VariantID];
        """;

    /// <summary>
    /// Returns the batch. Add <see cref="StatsContentSql.ChannelParameter"/> when <paramref name="hasChannel"/>; all other parameters
    /// (<see cref="StatsContentSql.ClassTypeParameter"/>, <see cref="StatsContentSql.KindParameter"/> with the website kind,
    /// <see cref="ContentInventorySql.GetAgeParameters"/> and the ones of this class) are always used.
    /// </summary>
    /// <remarks>Result sets, in order: totals (one row), age buckets (one row), stale pages with visits, pages without visits.</remarks>
    /// <param name="hasChannel">Filter by website channel.</param>
    public static string Build(bool hasChannel) =>
        string.Join(
            Environment.NewLine,
            "SET NOCOUNT ON;",
            string.Format(null, PagesTable, StatsContentSql.ItemsWhere(hasKind: true, hasChannel), StatsContentSql.VariantsFrom),
            TotalsQuery,
            string.Format(
                null,
                AgeQuery,
                ContentInventorySql.AgeColumns("X.[ModifiedWhen]"),
                ContentInventorySql.AgeColumns("X.[ModifiedWhen]", "X.[Visits]", VisitsSuffix)),
            StalePopularQuery,
            NoVisitsQuery);

    /// <summary>Suffix of the age bucket visit columns (see <see cref="ContentInventorySql.AgeColumns"/>).</summary>
    public const string VisitsSuffix = "Visits";
}
