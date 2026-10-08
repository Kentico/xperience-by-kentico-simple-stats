using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.TagUsage;

/// <summary>
/// Builds the tag usage batch: taxonomies, most used tags, unused tags, tagged variants per field and variants per content type in one
/// round trip (see <see cref="Build"/>). Only constant SQL fragments are combined; all values are parameters.
/// </summary>
/// <remarks>
/// <para>
/// Tags are stored per language variant and field: one <c>CMS_ContentItemTag</c> row per variant (<c>ContentItemLanguageMetadataID</c>),
/// field (<c>ContentItemTagFieldGUID</c>) and tag (<c>ContentItemTagTagGUID</c>). The tag uses of the filtered variants (content type type,
/// items from <see cref="StatsContentSql"/>) and the optional taxonomy are read once into the table variable <c>@Uses</c>.
/// A use of a tag is a language variant with the tag in any field; a parent tag counts only its own uses, not its children's.
/// </para>
/// <para>
/// A tag is unused when no <c>CMS_ContentItemTag</c> row has it, in any content (the content type type filter does not apply).
/// The tags of the taxonomy filter and whether they are used are read once into <c>@Tags</c>.
/// </para>
/// </remarks>
internal static class TagUsageSql
{
    /// <summary>Optional taxonomy ID.</summary>
    public const string TaxonomyParameter = "@TaxonomyID";

    /// <summary>Maximum number of listed most used tags.</summary>
    public const string LimitParameter = "@Limit";

    /// <summary>Maximum number of listed unused tags.</summary>
    public const string UnusedLimitParameter = "@UnusedLimit";

    // Tag uses of the filtered variants. {0} = items WHERE, {1} = taxonomy condition on G.
    private const string UsesStatement = """
        SET NOCOUNT ON;
        DECLARE @Uses TABLE (
            [VariantID] int NOT NULL,
            [ClassID] int NOT NULL,
            [FieldGUID] uniqueidentifier NOT NULL,
            [TagID] int NOT NULL,
            [TaxonomyID] int NOT NULL
        );
        INSERT INTO @Uses ([VariantID], [ClassID], [FieldGUID], [TagID], [TaxonomyID])
        SELECT
            M.[ContentItemLanguageMetadataID],
            C.[ClassID],
            T.[ContentItemTagFieldGUID],
            G.[TagID],
            G.[TagTaxonomyID]
        FROM [CMS_ContentItemTag] T
        INNER JOIN [CMS_Tag] G ON G.[TagGUID] = T.[ContentItemTagTagGUID]
        INNER JOIN [CMS_ContentItemLanguageMetadata] M ON M.[ContentItemLanguageMetadataID] = T.[ContentItemTagContentItemLanguageMetadataID]
        INNER JOIN [CMS_ContentItem] I ON I.[ContentItemID] = M.[ContentItemLanguageMetadataContentItemID]
        INNER JOIN [CMS_Class] C ON C.[ClassID] = I.[ContentItemContentTypeID]
        {0}{1};
        """;

    // Tags of the taxonomy filter and whether any content item has them. {0} = taxonomy WHERE on G.
    private const string TagsStatement = """
        DECLARE @Tags TABLE (
            [TagID] int NOT NULL PRIMARY KEY,
            [TaxonomyID] int NOT NULL,
            [IsUsed] bit NOT NULL
        );
        INSERT INTO @Tags ([TagID], [TaxonomyID], [IsUsed])
        SELECT
            G.[TagID],
            G.[TagTaxonomyID],
            CASE WHEN EXISTS (
                SELECT 1 FROM [CMS_ContentItemTag] T WHERE T.[ContentItemTagTagGUID] = G.[TagGUID]) THEN 1 ELSE 0 END
        FROM [CMS_Tag] G{0};
        """;

    // 1. All taxonomies with their tags, unused tags (all content) and tags used in the filtered content.
    private const string TaxonomiesQuery = """
        SELECT
            X.[TaxonomyID],
            X.[TaxonomyGUID],
            X.[TaxonomyName],
            X.[TaxonomyTitle],
            ISNULL(S.[TagCount], 0) AS [TagCount],
            ISNULL(S.[UnusedTagCount], 0) AS [UnusedTagCount],
            ISNULL(U.[UsedTagCount], 0) AS [UsedTagCount]
        FROM [CMS_Taxonomy] X
        LEFT JOIN (
            SELECT
                S1.[TaxonomyID],
                COUNT(*) AS [TagCount],
                SUM(CASE WHEN S1.[IsUsed] = 0 THEN 1 ELSE 0 END) AS [UnusedTagCount]
            FROM @Tags S1
            GROUP BY S1.[TaxonomyID]
        ) S ON S.[TaxonomyID] = X.[TaxonomyID]
        LEFT JOIN (
            SELECT U1.[TaxonomyID], COUNT(DISTINCT U1.[TagID]) AS [UsedTagCount]
            FROM @Uses U1
            GROUP BY U1.[TaxonomyID]
        ) U ON U.[TaxonomyID] = X.[TaxonomyID]
        ORDER BY X.[TaxonomyTitle], X.[TaxonomyID];
        """;

    // 2. Most used tags: distinct variants with the tag in any field.
    private const string TopTagsQuery = """
        SELECT TOP (@Limit)
            G.[TagID],
            G.[TagTaxonomyID] AS [TaxonomyID],
            G.[TagTitle],
            X.[TaxonomyTitle],
            P.[TagTitle] AS [ParentTitle],
            COUNT(DISTINCT U.[VariantID]) AS [Uses]
        FROM @Uses U
        INNER JOIN [CMS_Tag] G ON G.[TagID] = U.[TagID]
        INNER JOIN [CMS_Taxonomy] X ON X.[TaxonomyID] = G.[TagTaxonomyID]
        LEFT JOIN [CMS_Tag] P ON P.[TagID] = G.[TagParentID]
        GROUP BY G.[TagID], G.[TagTaxonomyID], G.[TagTitle], X.[TaxonomyTitle], P.[TagTitle]
        ORDER BY [Uses] DESC, G.[TagTitle], G.[TagID];
        """;

    // 3. Unused tags by taxonomy and title. The count is in the taxonomies result set.
    private const string UnusedTagsQuery = """
        SELECT TOP (@UnusedLimit)
            G.[TagID],
            G.[TagTaxonomyID] AS [TaxonomyID],
            G.[TagTitle],
            X.[TaxonomyTitle],
            P.[TagTitle] AS [ParentTitle],
            0 AS [Uses]
        FROM @Tags S
        INNER JOIN [CMS_Tag] G ON G.[TagID] = S.[TagID]
        INNER JOIN [CMS_Taxonomy] X ON X.[TaxonomyID] = G.[TagTaxonomyID]
        LEFT JOIN [CMS_Tag] P ON P.[TagID] = G.[TagParentID]
        WHERE S.[IsUsed] = 0
        ORDER BY X.[TaxonomyTitle], G.[TagTitle], G.[TagID];
        """;

    // 4. Variants with at least one tag, per field and content type.
    private const string FieldCountsQuery = """
        SELECT U.[FieldGUID], U.[ClassID], COUNT(DISTINCT U.[VariantID]) AS [Tagged]
        FROM @Uses U
        GROUP BY U.[FieldGUID], U.[ClassID];
        """;

    // 5. Filtered variants per content type. {0} = variants FROM, {1} = items WHERE.
    private const string ContentTypesQuery = """
        SELECT C.[ClassID], C.[ClassName], C.[ClassDisplayName], COUNT(*) AS [Variants]
        {0}
        {1}
        GROUP BY C.[ClassID], C.[ClassName], C.[ClassDisplayName];
        """;

    private const string UsesTaxonomyCondition = """

            AND G.[TagTaxonomyID] = @TaxonomyID
        """;

    private const string TagsTaxonomyWhere = """

        WHERE G.[TagTaxonomyID] = @TaxonomyID
        """;

    /// <summary>
    /// Returns the batch. Add <see cref="StatsContentSql.KindParameter"/> when <paramref name="hasKind"/> and
    /// <see cref="TaxonomyParameter"/> when <paramref name="hasTaxonomy"/>; all other parameters
    /// (<see cref="StatsContentSql.ClassTypeParameter"/> and the limits of this class) are always used.
    /// </summary>
    /// <remarks>Result sets, in order: taxonomies, most used tags, unused tags, tagged variants per field and content type, variants per content type.</remarks>
    /// <param name="hasKind">Filter by content type type.</param>
    /// <param name="hasTaxonomy">Filter by taxonomy.</param>
    public static string Build(bool hasKind, bool hasTaxonomy)
    {
        string itemsWhere = StatsContentSql.ItemsWhere(hasKind, hasChannel: false);

        return string.Join(
            Environment.NewLine,
            string.Format(null, UsesStatement, itemsWhere, hasTaxonomy ? UsesTaxonomyCondition : string.Empty),
            string.Format(null, TagsStatement, hasTaxonomy ? TagsTaxonomyWhere : string.Empty),
            TaxonomiesQuery,
            TopTagsQuery,
            UnusedTagsQuery,
            FieldCountsQuery,
            string.Format(null, ContentTypesQuery, StatsContentSql.VariantsFrom, itemsWhere));
    }
}
