using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContentInventory;

/// <summary>
/// Builds the content inventory batch: all aggregates and lists in one round trip (see <see cref="Build"/> for the result sets).
/// Only constant SQL fragments are combined; all values are parameters.
/// </summary>
/// <remarks>
/// Items, variants, filters and link columns come from <see cref="StatsContentSql"/>.
/// Items are <c>CMS_ContentItem</c> rows whose class is a content type (<c>ClassType</c> = <see cref="ClassTypeParameter"/>).
/// Items without a content type (page folders in website channels) are not counted.
/// The optional filters are the content type type (<c>CMS_Class.ClassContentTypeType</c>) and
/// the item's channel (<c>CMS_ContentItem.ContentItemChannelID</c>; reusable items have no channel).
/// A language variant is one <c>CMS_ContentItemLanguageMetadata</c> row (unique per item and language); its
/// <c>ModifiedWhen</c> is the last change of the variant's latest version.
/// </remarks>
internal static class ContentInventorySql
{
    public const string ClassTypeParameter = StatsContentSql.ClassTypeParameter;
    public const string KindParameter = StatsContentSql.KindParameter;
    public const string ChannelParameter = StatsContentSql.ChannelParameter;

    /// <summary>Modified on or after this time: under 3 months old.</summary>
    public const string Age3Parameter = "@Age3";

    /// <summary>Modified on or after this time (and before <see cref="Age3Parameter"/>): 3–6 months old.</summary>
    public const string Age6Parameter = "@Age6";

    /// <summary>Modified on or after this time (and before <see cref="Age6Parameter"/>): 6–12 months old. Before it: over 12 months.</summary>
    public const string Age12Parameter = "@Age12";

    /// <summary>Variants in a workflow step modified before this time count as waiting too long.</summary>
    public const string OverdueBeforeParameter = "@OverdueBefore";

    /// <summary>Maximum number of rows of each list.</summary>
    public const string LimitParameter = "@Limit";

    /// <summary>1 to read unused reusable items, 0 to return them empty (the kind or channel filter excludes reusable items).</summary>
    public const string IncludeUnusedParameter = "@IncludeUnused";

    /// <summary>Content type type of reusable items (<c>ClassContentTypeType.REUSABLE</c>).</summary>
    public const string ReusableKindParameter = "@ReusableKind";

    // 1. Every content type of the kind with its item count (0 included).
    // The channel condition is part of the LEFT JOIN, so types without items in the channel stay listed with 0.
    private const string ContentTypesQuery = """
        SELECT
            C.[ClassID],
            C.[ClassName],
            C.[ClassDisplayName],
            C.[ClassContentTypeType],
            COUNT(I.[ContentItemID]) AS [ItemCount]
        FROM [CMS_Class] C
        LEFT JOIN [CMS_ContentItem] I
            ON I.[ContentItemContentTypeID] = C.[ClassID]{0}
        WHERE C.[ClassType] = @ClassType
            AND C.[ClassContentTypeType] IS NOT NULL{1}
        GROUP BY C.[ClassID], C.[ClassName], C.[ClassDisplayName], C.[ClassContentTypeType];
        """;

    // 2. Every language with the number of filtered items that have a variant in it.
    private const string LanguagesQuery = """
        SELECT
            L.[ContentLanguageID],
            L.[ContentLanguageName],
            L.[ContentLanguageDisplayName],
            L.[ContentLanguageIsDefault],
            COUNT(V.[ContentItemLanguageMetadataID]) AS [ItemCount]
        FROM [CMS_ContentLanguage] L
        LEFT JOIN (
            SELECT M.[ContentItemLanguageMetadataID], M.[ContentItemLanguageMetadataContentLanguageID]
            {2}
            {3}
        ) V ON V.[ContentItemLanguageMetadataContentLanguageID] = L.[ContentLanguageID]
        GROUP BY L.[ContentLanguageID], L.[ContentLanguageName], L.[ContentLanguageDisplayName], L.[ContentLanguageIsDefault];
        """;

    // 3. Language variants per latest version status and workflow step, with scheduled publish/unpublish counts.
    private const string StatusesQuery = """
        SELECT
            M.[ContentItemLanguageMetadataLatestVersionStatus] AS [VersionStatus],
            M.[ContentItemLanguageMetadataContentWorkflowStepID] AS [StepID],
            S.[ContentWorkflowStepDisplayName] AS [StepDisplayName],
            W.[ContentWorkflowID] AS [WorkflowID],
            W.[ContentWorkflowDisplayName] AS [WorkflowDisplayName],
            COUNT(*) AS [VariantCount],
            SUM(CASE WHEN M.[ContentItemLanguageMetadataScheduledPublishWhen] IS NULL THEN 0 ELSE 1 END) AS [ScheduledPublish],
            SUM(CASE WHEN M.[ContentItemLanguageMetadataScheduledUnpublishWhen] IS NULL THEN 0 ELSE 1 END) AS [ScheduledUnpublish]
        {2}
        LEFT JOIN [CMS_ContentWorkflowStep] S ON S.[ContentWorkflowStepID] = M.[ContentItemLanguageMetadataContentWorkflowStepID]
        LEFT JOIN [CMS_ContentWorkflow] W ON W.[ContentWorkflowID] = S.[ContentWorkflowStepWorkflowID]
        {3}
        GROUP BY
            M.[ContentItemLanguageMetadataLatestVersionStatus],
            M.[ContentItemLanguageMetadataContentWorkflowStepID],
            S.[ContentWorkflowStepDisplayName],
            W.[ContentWorkflowID],
            W.[ContentWorkflowDisplayName];
        """;

    // 4. Language variants per age of the last change (one row).
    private const string AgeQuery = """
        SELECT
            ISNULL(SUM(CASE WHEN M.[ContentItemLanguageMetadataModifiedWhen] >= @Age3 THEN 1 ELSE 0 END), 0) AS [Under3Months],
            ISNULL(SUM(CASE WHEN M.[ContentItemLanguageMetadataModifiedWhen] < @Age3 AND M.[ContentItemLanguageMetadataModifiedWhen] >= @Age6 THEN 1 ELSE 0 END), 0) AS [Months3To6],
            ISNULL(SUM(CASE WHEN M.[ContentItemLanguageMetadataModifiedWhen] < @Age6 AND M.[ContentItemLanguageMetadataModifiedWhen] >= @Age12 THEN 1 ELSE 0 END), 0) AS [Months6To12],
            ISNULL(SUM(CASE WHEN M.[ContentItemLanguageMetadataModifiedWhen] < @Age12 THEN 1 ELSE 0 END), 0) AS [Over12Months]
        {2}
        {3};
        """;

    // 5. Least recently modified language variants.
    private const string OldestQuery = """
        SELECT TOP (@Limit)
            M.[ContentItemLanguageMetadataID] AS [VariantID],
            I.[ContentItemID],
            {5}
            L.[ContentLanguageName],
            M.[ContentItemLanguageMetadataDisplayName] AS [DisplayName],
            C.[ClassDisplayName],
            L.[ContentLanguageDisplayName],
            M.[ContentItemLanguageMetadataModifiedWhen] AS [ModifiedWhen]
        {2}
        INNER JOIN [CMS_ContentLanguage] L ON L.[ContentLanguageID] = M.[ContentItemLanguageMetadataContentLanguageID]
        {6}
        {3}
        ORDER BY M.[ContentItemLanguageMetadataModifiedWhen], M.[ContentItemLanguageMetadataID];
        """;

    // 6. Language variants in a workflow step, longest unchanged first. The window count runs before TOP, so it covers all of them.
    private const string WorkflowQuery = """
        SELECT TOP (@Limit)
            M.[ContentItemLanguageMetadataID] AS [VariantID],
            I.[ContentItemID],
            {5}
            L.[ContentLanguageName],
            M.[ContentItemLanguageMetadataDisplayName] AS [DisplayName],
            C.[ClassDisplayName],
            L.[ContentLanguageDisplayName],
            M.[ContentItemLanguageMetadataModifiedWhen] AS [ModifiedWhen],
            S.[ContentWorkflowStepDisplayName] AS [StepDisplayName],
            W.[ContentWorkflowID] AS [WorkflowID],
            W.[ContentWorkflowDisplayName] AS [WorkflowDisplayName],
            SUM(CASE WHEN M.[ContentItemLanguageMetadataModifiedWhen] < @OverdueBefore THEN 1 ELSE 0 END) OVER () AS [OverdueCount]
        {2}
        INNER JOIN [CMS_ContentLanguage] L ON L.[ContentLanguageID] = M.[ContentItemLanguageMetadataContentLanguageID]
        {6}
        LEFT JOIN [CMS_ContentWorkflowStep] S ON S.[ContentWorkflowStepID] = M.[ContentItemLanguageMetadataContentWorkflowStepID]
        LEFT JOIN [CMS_ContentWorkflow] W ON W.[ContentWorkflowID] = S.[ContentWorkflowStepWorkflowID]
        {3}
            AND M.[ContentItemLanguageMetadataContentWorkflowStepID] IS NOT NULL
        ORDER BY M.[ContentItemLanguageMetadataModifiedWhen], M.[ContentItemLanguageMetadataID];
        """;

    // Reusable items that no content item references (content item selector fields and Page Builder widget properties
    // are stored in CMS_ContentItemReference, from any language and version of the referencing item).
    private const string UnusedWhere = """
        WHERE @IncludeUnused = 1
            AND C.[ClassType] = @ClassType
            AND C.[ClassContentTypeType] = @ReusableKind
            AND NOT EXISTS (
                SELECT 1 FROM [CMS_ContentItemReference] R
                WHERE R.[ContentItemReferenceTargetItemID] = I.[ContentItemID]
            )
        """;

    // 7. Unused reusable items per content type.
    private const string UnusedByTypeQuery = """
        SELECT C.[ClassID], C.[ClassName], C.[ClassDisplayName], COUNT(*) AS [ItemCount]
        FROM [CMS_ContentItem] I
        INNER JOIN [CMS_Class] C ON C.[ClassID] = I.[ContentItemContentTypeID]
        {4}
        GROUP BY C.[ClassID], C.[ClassName], C.[ClassDisplayName];
        """;

    // 8. Unused reusable items, least recently modified first. Name and date come from the most recently modified variant.
    private const string UnusedItemsQuery = """
        SELECT TOP (@Limit)
            I.[ContentItemID],
            I.[ContentItemWorkspaceID] AS [WorkspaceID],
            N.[ContentLanguageName],
            ISNULL(N.[DisplayName], I.[ContentItemName]) AS [DisplayName],
            C.[ClassDisplayName],
            N.[ModifiedWhen]
        FROM [CMS_ContentItem] I
        INNER JOIN [CMS_Class] C ON C.[ClassID] = I.[ContentItemContentTypeID]
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
        {4}
        ORDER BY N.[ModifiedWhen], I.[ContentItemID];
        """;

    /// <summary>
    /// Returns the batch. Add <see cref="KindParameter"/> when <paramref name="hasKind"/> and
    /// <see cref="ChannelParameter"/> when <paramref name="hasChannel"/>; all other parameters are always used.
    /// </summary>
    /// <remarks>
    /// Result sets, in order: content types, languages, statuses, age buckets (one row), oldest variants,
    /// variants in a workflow step, unused reusable items per content type, unused reusable items.
    /// </remarks>
    public static string Build(bool hasKind, bool hasChannel)
    {
        object[] args =
        [
            StatsContentSql.ChannelCondition(hasChannel),
            StatsContentSql.KindCondition(hasKind),
            StatsContentSql.VariantsFrom,
            StatsContentSql.ItemsWhere(hasKind, hasChannel),
            UnusedWhere,
            StatsContentSql.LinkColumns,
            StatsContentSql.LinkApply,
        ];

        return string.Join(
            Environment.NewLine,
            new[]
            {
                ContentTypesQuery,
                LanguagesQuery,
                StatusesQuery,
                AgeQuery,
                OldestQuery,
                WorkflowQuery,
                UnusedByTypeQuery,
                UnusedItemsQuery,
            }.Select(query => string.Format(null, query, args)));
    }
}
