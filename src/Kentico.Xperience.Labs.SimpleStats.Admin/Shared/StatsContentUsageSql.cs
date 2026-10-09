namespace Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

/// <summary>
/// SQL fragments of the usage of reusable items, shared by the content inventory ("Unused reusable items") and the reusable content usage
/// report, so both use one definition and used + unused = all reusable items. Only constant fragments; all values are parameters.
/// </summary>
/// <remarks>
/// <para>
/// A reusable item is used when a content item references it: a <c>CMS_ContentItemReference</c> row whose
/// <c>ContentItemReferenceTargetItemID</c> is the item. The product stores one row per language variant and version of the
/// referencing item (<c>ContentItemReferenceSourceCommonDataID</c> → <c>CMS_ContentItemCommonData</c>), for references from the
/// content item selector, rich text, Page and Email Builder component properties and components with a reference extractor.
/// </para>
/// <para>
/// A usage is one distinct referencing content item (<c>ContentItemCommonDataContentItemID</c>), not a reference row or a language variant.
/// The source column has a foreign key to <c>CMS_ContentItemCommonData</c>, so every reference row has a referencing item:
/// an item has a row in <see cref="UsageQuery"/> exactly when <see cref="UnusedCondition"/> is false.
/// An item that references itself counts as used (one usage), as in the Content hub.
/// </para>
/// Aliases: <c>I</c> item, <c>C</c> content type.
/// </remarks>
internal static class StatsContentUsageSql
{
    /// <summary>Content type type of reusable items (<c>ClassContentTypeType.REUSABLE</c>).</summary>
    public const string ReusableKindParameter = "@ReusableKind";

    /// <summary>Content type type of pages (<c>ClassContentTypeType.WEBSITE</c>).</summary>
    public const string WebsiteKindParameter = "@WebsiteKind";

    /// <summary>Content type type of emails (<c>ClassContentTypeType.EMAIL</c>).</summary>
    public const string EmailKindParameter = "@EmailKind";

    /// <summary>Content type type of headless items (<c>ClassContentTypeType.HEADLESS</c>).</summary>
    public const string HeadlessKindParameter = "@HeadlessKind";

    /// <summary>
    /// Reusable items: the content type (<c>C</c>) is a content type (<see cref="StatsContentSql.ClassTypeParameter"/>) of the reusable type type.
    /// No leading <c>AND</c>.
    /// </summary>
    public const string ReusableCondition = """
        C.[ClassType] = @ClassType
                    AND C.[ClassContentTypeType] = @ReusableKind
        """;

    /// <summary>
    /// The item (<c>I</c>) is not referenced by any content item. No leading <c>AND</c>.
    /// </summary>
    public const string UnusedCondition = """
        NOT EXISTS (
                        SELECT 1 FROM [CMS_ContentItemReference] R
                        WHERE R.[ContentItemReferenceTargetItemID] = I.[ContentItemID]
                    )
        """;

    /// <summary>
    /// Usages per referenced item: <c>[TargetItemID]</c>, distinct referencing items in total (<c>[Usages]</c>) and per content type type of
    /// the referencing item (<c>[Pages]</c>, <c>[Emails]</c>, <c>[ReusableItems]</c>, <c>[HeadlessItems]</c>). Referencing items of another
    /// type type count only in the total. The inner select has one row per referenced and referencing item (an item has one content type),
    /// whatever the number of its variants and versions. One row per referenced item; items without references have none. A select without a semicolon
    /// (use it as a CTE or derived table). Needs the kind parameters of this class.
    /// </summary>
    public const string UsageQuery = """
        SELECT
                S.[TargetItemID],
                COUNT(*) AS [Usages],
                SUM(CASE WHEN S.[SourceKind] = @WebsiteKind THEN 1 ELSE 0 END) AS [Pages],
                SUM(CASE WHEN S.[SourceKind] = @EmailKind THEN 1 ELSE 0 END) AS [Emails],
                SUM(CASE WHEN S.[SourceKind] = @ReusableKind THEN 1 ELSE 0 END) AS [ReusableItems],
                SUM(CASE WHEN S.[SourceKind] = @HeadlessKind THEN 1 ELSE 0 END) AS [HeadlessItems]
            FROM (
                SELECT DISTINCT
                    R.[ContentItemReferenceTargetItemID] AS [TargetItemID],
                    D.[ContentItemCommonDataContentItemID] AS [SourceItemID],
                    SC.[ClassContentTypeType] AS [SourceKind]
                FROM [CMS_ContentItemReference] R
                INNER JOIN [CMS_ContentItemCommonData] D ON D.[ContentItemCommonDataID] = R.[ContentItemReferenceSourceCommonDataID]
                LEFT JOIN [CMS_ContentItem] SI ON SI.[ContentItemID] = D.[ContentItemCommonDataContentItemID]
                LEFT JOIN [CMS_Class] SC ON SC.[ClassID] = SI.[ContentItemContentTypeID]
            ) S
            GROUP BY S.[TargetItemID]
        """;

    /// <summary>
    /// The item's (<c>I</c>) most recently modified language variant (<c>N</c>): <c>N.[ContentLanguageName]</c>, <c>N.[DisplayName]</c> and
    /// <c>N.[ModifiedWhen]</c>, all <c>NULL</c> when the item has no variant. Lists of whole items (not variants) take name and date from it.
    /// </summary>
    public const string LatestVariantApply = """
        OUTER APPLY (
                    SELECT TOP (1)
                        L.[ContentLanguageName],
                        M.[ContentItemLanguageMetadataDisplayName] AS [DisplayName],
                        M.[ContentItemLanguageMetadataModifiedWhen] AS [ModifiedWhen]
                    FROM [CMS_ContentItemLanguageMetadata] M
                    INNER JOIN [CMS_ContentLanguage] L ON L.[ContentLanguageID] = M.[ContentItemLanguageMetadataContentLanguageID]
                    WHERE M.[ContentItemLanguageMetadataContentItemID] = I.[ContentItemID]
                    ORDER BY M.[ContentItemLanguageMetadataModifiedWhen] DESC
                ) N
        """;
}
