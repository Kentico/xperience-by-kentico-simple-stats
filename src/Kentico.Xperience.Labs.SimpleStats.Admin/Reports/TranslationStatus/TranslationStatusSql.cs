using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.TranslationStatus;

/// <summary>
/// Builds the translation status batch: languages, counts per language and content type, and the outdated variants in one round trip
/// (see <see cref="Build"/>). Only constant SQL fragments are combined; all values are parameters.
/// </summary>
/// <remarks>
/// <para>
/// Items, the kind and channel filter, the link and channel columns and user names come from <see cref="StatsContentSql"/>.
/// A language variant is one <c>CMS_ContentItemLanguageMetadata</c> row (unique per item and language). The default variant (<c>D</c>)
/// is the item's variant in the default language (<c>ContentLanguageIsDefault</c>).
/// </para>
/// <para>
/// Counts cover every filtered item in every non-default language, the same items and the same "has a variant" rule as the
/// content inventory's language coverage, so missing = items - translated matches it. Items without a default variant are counted
/// there too, but are never outdated (nothing to compare with).
/// </para>
/// <para>
/// A variant is outdated when its last change (<c>ModifiedWhen</c>) is more than <see cref="ToleranceParameter"/> minutes older than the
/// default variant's. Times are compared as stored (server local time). The language filter is applied by the report builder, so one
/// cached read serves every language: the outdated list has the top <see cref="LimitParameter"/> rows of each language.
/// </para>
/// </remarks>
internal static class TranslationStatusSql
{
    /// <summary>Minutes a variant can be older than the default variant and still be up to date.</summary>
    public const string ToleranceParameter = "@ToleranceMinutes";

    /// <summary>Maximum number of listed outdated variants per language.</summary>
    public const string LimitParameter = "@Limit";

    /// <summary>
    /// Outdated condition of a variant (<c>M</c>) against its default variant (<c>D</c>). False when there is no default variant.
    /// </summary>
    public const string OutdatedCondition =
        "M.[ContentItemLanguageMetadataModifiedWhen] < DATEADD(minute, -@ToleranceMinutes, D.[ContentItemLanguageMetadataModifiedWhen])";

    // Default language. The lowest ID if data has several; NULL without one (then nothing is outdated and all languages are listed).
    private const string DefaultLanguageDeclare = """
        DECLARE @DefaultLanguageID int = (
            SELECT TOP (1) [ContentLanguageID]
            FROM [CMS_ContentLanguage]
            WHERE [ContentLanguageIsDefault] = 1
            ORDER BY [ContentLanguageID]);
        """;

    // The item's default variant (D).
    private const string DefaultVariantJoin = """
        [CMS_ContentItemLanguageMetadata] D
                ON D.[ContentItemLanguageMetadataContentItemID] = I.[ContentItemID]
                AND D.[ContentItemLanguageMetadataContentLanguageID] = @DefaultLanguageID
        """;

    // Not the default language (all languages without a default one).
    private const string NonDefaultCondition = "(@DefaultLanguageID IS NULL OR L.[ContentLanguageID] <> @DefaultLanguageID)";

    // 1. All languages, default first.
    private const string LanguagesQuery = """
        SELECT
            L.[ContentLanguageID],
            L.[ContentLanguageName],
            L.[ContentLanguageDisplayName],
            CAST(CASE WHEN L.[ContentLanguageID] = @DefaultLanguageID THEN 1 ELSE 0 END AS bit) AS [IsDefault]
        FROM [CMS_ContentLanguage] L
        ORDER BY [IsDefault] DESC, L.[ContentLanguageDisplayName], L.[ContentLanguageID];
        """;

    // 2. Per non-default language and content type: filtered items, items with a variant in the language, outdated variants.
    // Every item is paired with every non-default language, so items without a variant count as missing. {0} = items WHERE.
    private const string CountsQuery = $$"""
        SELECT
            L.[ContentLanguageID],
            C.[ClassID],
            C.[ClassName],
            C.[ClassDisplayName],
            COUNT(*) AS [ItemCount],
            COUNT(M.[ContentItemLanguageMetadataID]) AS [TranslatedCount],
            SUM(CASE WHEN {{OutdatedCondition}} THEN 1 ELSE 0 END) AS [OutdatedCount]
        FROM [CMS_ContentItem] I
        INNER JOIN [CMS_Class] C ON C.[ClassID] = I.[ContentItemContentTypeID]
        CROSS JOIN [CMS_ContentLanguage] L
        LEFT JOIN [CMS_ContentItemLanguageMetadata] M
            ON M.[ContentItemLanguageMetadataContentItemID] = I.[ContentItemID]
            AND M.[ContentItemLanguageMetadataContentLanguageID] = L.[ContentLanguageID]
        LEFT JOIN {{DefaultVariantJoin}}
        {0}
            AND {{NonDefaultCondition}}
        GROUP BY L.[ContentLanguageID], C.[ClassID], C.[ClassName], C.[ClassDisplayName];
        """;

    // 3. Outdated variants, most minutes behind first, top @Limit per language. The user is the variant's last modifier.
    // {0} = variants FROM, {1} = items WHERE, {2} = link columns, {3} = link joins.
    private const string OutdatedQuery = $$"""
        SELECT X.*
        FROM (
            SELECT
                M.[ContentItemLanguageMetadataID] AS [VariantID],
                I.[ContentItemID],
                {2}
                L.[ContentLanguageID],
                L.[ContentLanguageName],
                M.[ContentItemLanguageMetadataDisplayName] AS [DisplayName],
                C.[ClassDisplayName],
                L.[ContentLanguageDisplayName],
                {{StatsContentSql.ChannelLabelColumns}}
                U.[UserID],
                {{StatsContentSql.UserDisplayName}} AS [UserName],
                D.[ContentItemLanguageMetadataModifiedWhen] AS [DefaultModifiedWhen],
                M.[ContentItemLanguageMetadataModifiedWhen] AS [ModifiedWhen],
                ROW_NUMBER() OVER (
                    PARTITION BY L.[ContentLanguageID]
                    ORDER BY DATEDIFF(minute, M.[ContentItemLanguageMetadataModifiedWhen], D.[ContentItemLanguageMetadataModifiedWhen]) DESC,
                        M.[ContentItemLanguageMetadataID]) AS [RowNumber]
            {0}
            INNER JOIN [CMS_ContentLanguage] L ON L.[ContentLanguageID] = M.[ContentItemLanguageMetadataContentLanguageID]
            INNER JOIN {{DefaultVariantJoin}}
            {{StatsContentSql.ChannelLabelJoins}}
            LEFT JOIN [CMS_User] U ON U.[UserID] = M.[ContentItemLanguageMetadataModifiedByUserID]
            {3}
            {1}
                AND {{NonDefaultCondition}}
                AND {{OutdatedCondition}}
        ) X
        WHERE X.[RowNumber] <= @Limit
        ORDER BY DATEDIFF(minute, X.[ModifiedWhen], X.[DefaultModifiedWhen]) DESC, X.[VariantID];
        """;

    /// <summary>
    /// Returns the batch. Add <see cref="StatsContentSql.KindParameter"/> when <paramref name="hasKind"/> and
    /// <see cref="StatsContentSql.ChannelParameter"/> when <paramref name="hasChannel"/>; all other parameters
    /// (<see cref="StatsContentSql.ClassTypeParameter"/> and the ones of this class) are always used.
    /// </summary>
    /// <remarks>Result sets, in order: languages, counts per language and content type, outdated variants.</remarks>
    /// <param name="hasKind">Filter by content type type.</param>
    /// <param name="hasChannel">Filter by channel.</param>
    public static string Build(bool hasKind, bool hasChannel)
    {
        string itemsWhere = StatsContentSql.ItemsWhere(hasKind, hasChannel);

        return string.Join(
            Environment.NewLine,
            DefaultLanguageDeclare,
            LanguagesQuery,
            string.Format(null, CountsQuery, itemsWhere),
            string.Format(null, OutdatedQuery, StatsContentSql.VariantsFrom, itemsWhere, StatsContentSql.LinkColumns, StatsContentSql.LinkApply));
    }
}
