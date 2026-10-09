using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PublishingActivity;

/// <summary>
/// Builds the publishing activity batch: daily counts, content types, totals and the slowest to publish list in one round trip
/// (see <see cref="Build"/>). Only constant SQL fragments are combined; all values are parameters.
/// </summary>
/// <remarks>
/// <para>
/// Variants: language variants (<c>CMS_ContentItemLanguageMetadata</c>) of the filtered items (<see cref="StatsContentSql"/>: content types only,
/// so page folders are left out; kind and channel filter). Created: <c>ContentItemLanguageMetadataCreatedWhen</c>.
/// First published: the earliest <c>ContentItemCommonDataFirstPublishedWhen</c> of the variant's <c>CMS_ContentItemCommonData</c> rows
/// (a published variant with a newer draft has several rows). Published with no first publish date (migrated content): a row with the
/// published or unpublished <c>VersionStatus</c> and no first publish date in any row. Collected once into the <c>@Variants</c> table.
/// </para>
/// <para>
/// Updates: <c>CMS_ContentItemVersion</c> rows with the publish action (<c>ContentItemVersionAction.Publish</c>). The product writes one on every
/// publish while version history is enabled, also the first one. So the variant's earliest publish row is the first publish when it was
/// created within <see cref="FirstPublishToleranceParameter"/> seconds of the first publish date; all other publish rows are updates.
/// Version history is trimmed to a number of versions per variant, so old updates are missing. Collected into the <c>@Updates</c> table.
/// </para>
/// <para>
/// Time to publish: days from created to first published, for variants first published in the range; a first publish before the creation
/// (inconsistent data) is left out. Median and 90th percentile are computed in SQL (<c>PERCENTILE_CONT</c>, interpolated).
/// Times are compared as stored (server local time).
/// </para>
/// </remarks>
internal static class PublishingActivitySql
{
    public const string PublishedStatusParameter = "@PublishedStatus";
    public const string UnpublishedStatusParameter = "@UnpublishedStatus";
    public const string PublishActionParameter = "@PublishAction";

    /// <summary>Seconds between the first publish date and its version row (both written in the same publish).</summary>
    public const string FirstPublishToleranceParameter = "@FirstPublishTolerance";

    /// <summary>Start of the previous period (inclusive).</summary>
    public const string PreviousFromParameter = "@PreviousFrom";

    /// <summary>Start of the range (inclusive).</summary>
    public const string FromParameter = "@From";

    /// <summary>Day after the end of the range (exclusive).</summary>
    public const string ToExclusiveParameter = "@ToExclusive";

    /// <summary>Maximum number of rows of the slowest to publish list.</summary>
    public const string LimitParameter = "@Limit";

    public const string SeriesKeyColumn = "SeriesKey";
    public const string DateColumn = "ActivityDate";
    public const string CountColumn = "ActivityCount";

    // Days from created to first published (fractional, minute precision).
    private const string Days = "DATEDIFF(minute, X.[CreatedWhen], X.[FirstPublishedWhen]) / 1440.0";

    // First published in the range, not before created.
    private const string PublishedInRange = """
        X.[FirstPublishedWhen] >= @From
                AND X.[FirstPublishedWhen] < @ToExclusive
                AND X.[FirstPublishedWhen] >= X.[CreatedWhen]
        """;

    // {0} = variants FROM, {1} = items WHERE.
    private const string VariantsTable = """
        DECLARE @Variants TABLE (
            [VariantID] int NOT NULL PRIMARY KEY,
            [ContentItemID] int NOT NULL,
            [LanguageID] int NOT NULL,
            [ClassID] int NOT NULL,
            [CreatedWhen] datetime2(7) NOT NULL,
            [FirstPublishedWhen] datetime2(7) NULL,
            [WasPublished] bit NOT NULL,
            UNIQUE ([ContentItemID], [LanguageID])
        );

        INSERT INTO @Variants ([VariantID], [ContentItemID], [LanguageID], [ClassID], [CreatedWhen], [FirstPublishedWhen], [WasPublished])
        SELECT
            M.[ContentItemLanguageMetadataID],
            I.[ContentItemID],
            M.[ContentItemLanguageMetadataContentLanguageID],
            C.[ClassID],
            M.[ContentItemLanguageMetadataCreatedWhen],
            D.[FirstPublishedWhen],
            ISNULL(D.[WasPublished], 0)
        {0}
        OUTER APPLY (
            SELECT
                MIN(D1.[ContentItemCommonDataFirstPublishedWhen]) AS [FirstPublishedWhen],
                MAX(CASE WHEN D1.[ContentItemCommonDataVersionStatus] IN (@PublishedStatus, @UnpublishedStatus) THEN 1 ELSE 0 END) AS [WasPublished]
            FROM [CMS_ContentItemCommonData] D1
            WHERE D1.[ContentItemCommonDataContentItemID] = M.[ContentItemLanguageMetadataContentItemID]
                AND D1.[ContentItemCommonDataContentLanguageID] = M.[ContentItemLanguageMetadataContentLanguageID]
        ) D
        {1};
        """;

    // Publish version rows of the filtered variants from the previous period to the end of the range that are not the first publish.
    // Only rows in the range are read (seek per variant); the earliest publish row check looks up earlier rows of that variant only.
    private const string UpdatesTable = """
        DECLARE @Updates TABLE (
            [VariantID] int NOT NULL,
            [PublishedWhen] datetime2(7) NOT NULL
        );

        INSERT INTO @Updates ([VariantID], [PublishedWhen])
        SELECT X.[VariantID], V.[ContentItemVersionCreatedWhen]
        FROM @Variants X
        INNER JOIN [CMS_ContentItemVersion] V
            ON V.[ContentItemVersionContentItemID] = X.[ContentItemID]
            AND V.[ContentItemVersionContentLanguageID] = X.[LanguageID]
        WHERE V.[ContentItemVersionAction] = @PublishAction
            AND V.[ContentItemVersionCreatedWhen] >= @PreviousFrom
            AND V.[ContentItemVersionCreatedWhen] < @ToExclusive
            AND NOT (
                X.[FirstPublishedWhen] IS NOT NULL
                AND ABS(DATEDIFF(second, X.[FirstPublishedWhen], V.[ContentItemVersionCreatedWhen])) <= @FirstPublishTolerance
                AND NOT EXISTS (
                    SELECT 1
                    FROM [CMS_ContentItemVersion] P
                    WHERE P.[ContentItemVersionContentItemID] = V.[ContentItemVersionContentItemID]
                        AND P.[ContentItemVersionContentLanguageID] = V.[ContentItemVersionContentLanguageID]
                        AND P.[ContentItemVersionAction] = @PublishAction
                        AND (P.[ContentItemVersionCreatedWhen] < V.[ContentItemVersionCreatedWhen]
                            OR (P.[ContentItemVersionCreatedWhen] = V.[ContentItemVersionCreatedWhen]
                                AND P.[ContentItemVersionID] < V.[ContentItemVersionID]))));
        """;

    // 1. Counts per day and series, previous period + range.
    private const string DailyQuery = $$"""
        SELECT N'{{PublishingActivitySeriesKeys.Created}}' AS [SeriesKey], CAST(X.[CreatedWhen] AS date) AS [ActivityDate], COUNT(*) AS [ActivityCount]
        FROM @Variants X
        WHERE X.[CreatedWhen] >= @PreviousFrom AND X.[CreatedWhen] < @ToExclusive
        GROUP BY CAST(X.[CreatedWhen] AS date)
        UNION ALL
        SELECT N'{{PublishingActivitySeriesKeys.FirstPublished}}', CAST(X.[FirstPublishedWhen] AS date), COUNT(*)
        FROM @Variants X
        WHERE X.[FirstPublishedWhen] >= @PreviousFrom AND X.[FirstPublishedWhen] < @ToExclusive
        GROUP BY CAST(X.[FirstPublishedWhen] AS date)
        UNION ALL
        SELECT N'{{PublishingActivitySeriesKeys.Updates}}', CAST(U.[PublishedWhen] AS date), COUNT(*)
        FROM @Updates U
        GROUP BY CAST(U.[PublishedWhen] AS date);
        """;

    // 2. Content types with activity in the range, with the median days to publish.
    private const string ContentTypesQuery = $$"""
        WITH [Activity] AS (
            SELECT X.[ClassID], 1 AS [Created], 0 AS [FirstPublished], 0 AS [Updates]
            FROM @Variants X
            WHERE X.[CreatedWhen] >= @From AND X.[CreatedWhen] < @ToExclusive
            UNION ALL
            SELECT X.[ClassID], 0, 1, 0
            FROM @Variants X
            WHERE X.[FirstPublishedWhen] >= @From AND X.[FirstPublishedWhen] < @ToExclusive
            UNION ALL
            SELECT X.[ClassID], 0, 0, 1
            FROM @Updates U
            INNER JOIN @Variants X ON X.[VariantID] = U.[VariantID]
            WHERE U.[PublishedWhen] >= @From
        ),
        [Medians] AS (
            SELECT DISTINCT
                X.[ClassID],
                PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY {{Days}}) OVER (PARTITION BY X.[ClassID]) AS [MedianDays]
            FROM @Variants X
            WHERE {{PublishedInRange}}
        )
        SELECT
            A.[ClassID],
            C.[ClassDisplayName],
            SUM(A.[Created]) AS [Created],
            SUM(A.[FirstPublished]) AS [FirstPublished],
            SUM(A.[Updates]) AS [Updates],
            MAX(MD.[MedianDays]) AS [MedianDays]
        FROM [Activity] A
        INNER JOIN [CMS_Class] C ON C.[ClassID] = A.[ClassID]
        LEFT JOIN [Medians] MD ON MD.[ClassID] = A.[ClassID]
        GROUP BY A.[ClassID], C.[ClassDisplayName];
        """;

    // 3. Totals (one row): published without a first publish date (current state), median and 90th percentile days to publish in the range.
    private const string TotalsQuery = $$"""
        SELECT
            (SELECT COUNT(*) FROM @Variants X WHERE X.[WasPublished] = 1 AND X.[FirstPublishedWhen] IS NULL) AS [PublishedDateUnknown],
            P.[MedianDays],
            P.[Percentile90Days]
        FROM (SELECT 1 AS [One]) O
        OUTER APPLY (
            SELECT TOP (1)
                PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY {{Days}}) OVER () AS [MedianDays],
                PERCENTILE_CONT(0.9) WITHIN GROUP (ORDER BY {{Days}}) OVER () AS [Percentile90Days]
            FROM @Variants X
            WHERE {{PublishedInRange}}
        ) P;
        """;

    // 4. Slowest to publish in the range, longest first. {0} = link columns, {1} = link joins.
    private const string SlowestQuery = $$"""
        SELECT TOP (@Limit)
            X.[VariantID],
            I.[ContentItemID],
            {0}
            L.[ContentLanguageName],
            M.[ContentItemLanguageMetadataDisplayName] AS [DisplayName],
            C.[ClassDisplayName],
            L.[ContentLanguageDisplayName],
            {{StatsContentSql.ChannelLabelColumns}}
            X.[CreatedWhen],
            X.[FirstPublishedWhen]
        FROM @Variants X
        INNER JOIN [CMS_ContentItemLanguageMetadata] M ON M.[ContentItemLanguageMetadataID] = X.[VariantID]
        INNER JOIN [CMS_ContentItem] I ON I.[ContentItemID] = X.[ContentItemID]
        INNER JOIN [CMS_Class] C ON C.[ClassID] = X.[ClassID]
        INNER JOIN [CMS_ContentLanguage] L ON L.[ContentLanguageID] = X.[LanguageID]
        {{StatsContentSql.ChannelLabelJoins}}
        {1}
        WHERE {{PublishedInRange}}
        ORDER BY DATEDIFF(minute, X.[CreatedWhen], X.[FirstPublishedWhen]) DESC, X.[VariantID];
        """;

    /// <summary>
    /// Returns the batch. Add <see cref="StatsContentSql.KindParameter"/> when <paramref name="hasKind"/> and
    /// <see cref="StatsContentSql.ChannelParameter"/> when <paramref name="hasChannel"/>; all other parameters
    /// (<see cref="StatsContentSql.ClassTypeParameter"/> and the ones of this class) are always used.
    /// </summary>
    /// <remarks>Result sets, in order: daily counts, content types, totals (one row), slowest to publish.</remarks>
    /// <param name="hasKind">Filter by content type type.</param>
    /// <param name="hasChannel">Filter by channel.</param>
    public static string Build(bool hasKind, bool hasChannel) =>
        string.Join(
            Environment.NewLine,
            "SET NOCOUNT ON;",
            string.Format(null, VariantsTable, StatsContentSql.VariantsFrom, StatsContentSql.ItemsWhere(hasKind, hasChannel)),
            UpdatesTable,
            DailyQuery,
            ContentTypesQuery,
            TotalsQuery,
            string.Format(null, SlowestQuery, StatsContentSql.LinkColumns, StatsContentSql.LinkApply));
}

/// <summary>
/// Series keys of the publishing activity report (also used in SQL, so constant and without quotes).
/// </summary>
internal static class PublishingActivitySeriesKeys
{
    public const string Created = "created";
    public const string FirstPublished = "published";
    public const string Updates = "updates";
}
