using CMS.DataEngine;

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

    // 4. Language variants per age of the last change (one row). {7} = age columns.
    private const string AgeQuery = """
        SELECT
            {7}
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
    /// Returns the age bucket parameters (<see cref="Age3Parameter"/>, <see cref="Age6Parameter"/>, <see cref="Age12Parameter"/>):
    /// 3, 6 and <see cref="ContentInventoryReportBuilder.StaleMonths"/> months before <paramref name="now"/> (server time).
    /// </summary>
    public static IEnumerable<DataParameter> GetAgeParameters(DateTime now) =>
    [
        new DataParameter(Age3Parameter, now.AddMonths(-3)),
        new DataParameter(Age6Parameter, now.AddMonths(-6)),
        new DataParameter(Age12Parameter, GetStaleBefore(now)),
    ];

    /// <summary>
    /// Variants last changed before this time are stale (not changed in <see cref="ContentInventoryReportBuilder.StaleMonths"/> months).
    /// </summary>
    public static DateTime GetStaleBefore(DateTime now) => now.AddMonths(-ContentInventoryReportBuilder.StaleMonths);

    /// <summary>
    /// Select columns (no trailing comma) that sum <paramref name="value"/> per age bucket of <paramref name="modifiedColumn"/>
    /// (needs the <see cref="GetAgeParameters"/> parameters): <c>[Under3Months]</c>, <c>[Months3To6]</c>, <c>[Months6To12]</c> and
    /// <c>[Over12Months]</c>, each followed by <paramref name="suffix"/>. Read with <see cref="ContentAgeRow"/>. Constant arguments only.
    /// </summary>
    /// <param name="modifiedColumn">Column with the last change, for example <c>M.[ContentItemLanguageMetadataModifiedWhen]</c>.</param>
    /// <param name="value">Summed per row: <c>1</c> counts rows, a column sums it (for example visits).</param>
    /// <param name="suffix">Suffix of the column names, for example <c>Visits</c>.</param>
    public static string AgeColumns(string modifiedColumn, string value = "1", string suffix = "") =>
        $"""
        ISNULL(SUM(CASE WHEN {modifiedColumn} >= @Age3 THEN {value} ELSE 0 END), 0) AS [Under3Months{suffix}],
                    ISNULL(SUM(CASE WHEN {modifiedColumn} < @Age3 AND {modifiedColumn} >= @Age6 THEN {value} ELSE 0 END), 0) AS [Months3To6{suffix}],
                    ISNULL(SUM(CASE WHEN {modifiedColumn} < @Age6 AND {modifiedColumn} >= @Age12 THEN {value} ELSE 0 END), 0) AS [Months6To12{suffix}],
                    ISNULL(SUM(CASE WHEN {modifiedColumn} < @Age12 THEN {value} ELSE 0 END), 0) AS [Over12Months{suffix}]
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
            AgeColumns("M.[ContentItemLanguageMetadataModifiedWhen]"),
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
