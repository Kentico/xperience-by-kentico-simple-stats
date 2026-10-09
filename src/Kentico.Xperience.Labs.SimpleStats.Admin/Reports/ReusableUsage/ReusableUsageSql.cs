using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ReusableUsage;

/// <summary>
/// Builds the reusable content usage batch: totals, most used items and usage per content type in one round trip (see <see cref="Build"/>).
/// Only constant SQL fragments are combined; all values are parameters.
/// </summary>
/// <remarks>
/// Reusable items, the usage definition and the item name come from <see cref="StatsContentUsageSql"/> (shared with the content inventory).
/// The usages of the filtered reusable items are computed once into the table variable <c>@Items</c>; the result sets read from it.
/// </remarks>
internal static class ReusableUsageSql
{
    /// <summary>Optional content type (class ID) of the reusable items.</summary>
    public const string ContentTypeParameter = "@ContentTypeID";

    /// <summary>Maximum number of listed items.</summary>
    public const string LimitParameter = "@Limit";

    /// <summary>Highest usage count of the "few usages" bucket (from 2).</summary>
    public const string FewMaxParameter = "@FewMax";

    /// <summary>Highest usage count of the "some usages" bucket (from <see cref="FewMaxParameter"/> + 1). More is "many".</summary>
    public const string SomeMaxParameter = "@SomeMax";

    // Usages of the filtered reusable items (0 without references). {0} = content type condition.
    private const string ItemsStatement = $$"""
        SET NOCOUNT ON;
        DECLARE @Items TABLE (
            [ContentItemID] int NOT NULL PRIMARY KEY,
            [ClassID] int NOT NULL,
            [Usages] int NOT NULL,
            [Pages] int NOT NULL,
            [Emails] int NOT NULL,
            [ReusableItems] int NOT NULL,
            [HeadlessItems] int NOT NULL
        );
        WITH [Usage] AS (
            {{StatsContentUsageSql.UsageQuery}}
        )
        INSERT INTO @Items ([ContentItemID], [ClassID], [Usages], [Pages], [Emails], [ReusableItems], [HeadlessItems])
        SELECT
            I.[ContentItemID],
            C.[ClassID],
            ISNULL(U.[Usages], 0),
            ISNULL(U.[Pages], 0),
            ISNULL(U.[Emails], 0),
            ISNULL(U.[ReusableItems], 0),
            ISNULL(U.[HeadlessItems], 0)
        FROM [CMS_ContentItem] I
        INNER JOIN [CMS_Class] C ON C.[ClassID] = I.[ContentItemContentTypeID]
        LEFT JOIN [Usage] U ON U.[TargetItemID] = I.[ContentItemID]
        WHERE {{StatsContentUsageSql.ReusableCondition}}{0};
        """;

    // 1. Items by number of usages (one row).
    private const string TotalsQuery = """
        SELECT
            COUNT(*) AS [ReusableItems],
            ISNULL(SUM(CASE WHEN X.[Usages] > 0 THEN 1 ELSE 0 END), 0) AS [UsedItems],
            ISNULL(SUM(CASE WHEN X.[Usages] = 0 THEN 1 ELSE 0 END), 0) AS [Unused],
            ISNULL(SUM(CASE WHEN X.[Usages] = 1 THEN 1 ELSE 0 END), 0) AS [UsedOnce],
            ISNULL(SUM(CASE WHEN X.[Usages] > 1 AND X.[Usages] <= @FewMax THEN 1 ELSE 0 END), 0) AS [UsedFew],
            ISNULL(SUM(CASE WHEN X.[Usages] > @FewMax AND X.[Usages] <= @SomeMax THEN 1 ELSE 0 END), 0) AS [UsedSome],
            ISNULL(SUM(CASE WHEN X.[Usages] > @SomeMax THEN 1 ELSE 0 END), 0) AS [UsedMany],
            ISNULL(SUM(X.[Usages]), 0) AS [Usages]
        FROM @Items X;
        """;

    // 2. Most used items. Name and date come from the most recently modified variant, as in the content inventory.
    private const string MostUsedQuery = $$"""
        SELECT TOP (@Limit)
            I.[ContentItemID],
            I.[ContentItemWorkspaceID] AS [WorkspaceID],
            WS.[WorkspaceDisplayName],
            N.[ContentLanguageName],
            ISNULL(N.[DisplayName], I.[ContentItemName]) AS [DisplayName],
            C.[ClassDisplayName],
            N.[ModifiedWhen],
            X.[Usages],
            X.[Pages],
            X.[Emails],
            X.[ReusableItems],
            X.[HeadlessItems]
        FROM @Items X
        INNER JOIN [CMS_ContentItem] I ON I.[ContentItemID] = X.[ContentItemID]
        INNER JOIN [CMS_Class] C ON C.[ClassID] = X.[ClassID]
        LEFT JOIN [CMS_Workspace] WS ON WS.[WorkspaceID] = I.[ContentItemWorkspaceID]
        {{StatsContentUsageSql.LatestVariantApply}}
        WHERE X.[Usages] > 0
        ORDER BY X.[Usages] DESC, I.[ContentItemID];
        """;

    // 3. Usage per content type (types with items only).
    private const string ContentTypesQuery = """
        SELECT
            C.[ClassID],
            C.[ClassName],
            C.[ClassDisplayName],
            COUNT(*) AS [ItemCount],
            SUM(CASE WHEN X.[Usages] > 0 THEN 1 ELSE 0 END) AS [UsedItems],
            SUM(X.[Usages]) AS [Usages]
        FROM @Items X
        INNER JOIN [CMS_Class] C ON C.[ClassID] = X.[ClassID]
        GROUP BY C.[ClassID], C.[ClassName], C.[ClassDisplayName];
        """;

    private const string ContentTypeCondition = """

            AND I.[ContentItemContentTypeID] = @ContentTypeID
        """;

    /// <summary>
    /// Returns the batch. Add <see cref="ContentTypeParameter"/> when <paramref name="hasContentType"/>; all other parameters
    /// (<see cref="StatsContentSql.ClassTypeParameter"/>, the kind parameters of <see cref="StatsContentUsageSql"/> and the ones of this class)
    /// are always used.
    /// </summary>
    /// <remarks>Result sets, in order: totals (one row), most used items, usage per content type.</remarks>
    /// <param name="hasContentType">Filter by content type.</param>
    public static string Build(bool hasContentType) =>
        string.Join(
            Environment.NewLine,
            string.Format(null, ItemsStatement, hasContentType ? ContentTypeCondition : string.Empty),
            TotalsQuery,
            MostUsedQuery,
            ContentTypesQuery);
}
