using System.Data.Common;

using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContentInventory;

/// <summary>
/// Current state of content items: counts by content type, latest version status and language,
/// content age, items waiting in workflow steps and unused reusable items.
/// </summary>
/// <param name="Kind">Applied content type type filter (<c>Website</c>, <c>Reusable</c>, <c>Email</c>, <c>Headless</c>), or <c>null</c> for all.</param>
/// <param name="ChannelId">Applied channel filter, or <c>null</c> for all.</param>
/// <param name="TotalItems">Content items that match the filters.</param>
/// <param name="ByKind">
/// Items per content type type, largest first. Kinds without items are left out. <see cref="StatsRankedItem.SecondaryValue"/> is the number of content types.
/// </param>
/// <param name="ByContentType">
/// Every content type of the selected kind, including types with no items (listed last).
/// <see cref="StatsRankedItem.SecondaryLabel"/> is the kind. <see cref="StatsRankedItem.AdminPath"/> links to the content type.
/// </param>
/// <param name="ByStatus">
/// Language variants per status of their latest version, in a fixed order (published, draft, in workflow, unpublished, then other if any).
/// </param>
/// <param name="LanguageCoverage">Items with a variant in each language vs all items. Default language first.</param>
/// <param name="Age">Language variants by time since their last change.</param>
/// <param name="Workflow">Language variants waiting in a workflow step.</param>
/// <param name="UnusedReusable">Reusable items no content item references. <c>null</c> when the filters exclude reusable items.</param>
/// <param name="TotalVariants">Language variants of the items (one item can have one per language).</param>
/// <param name="ScheduledPublish">Language variants with a scheduled publish.</param>
/// <param name="ScheduledUnpublish">Language variants with a scheduled unpublish.</param>
/// <param name="ContentTypeCount">Content types of the selected kind.</param>
/// <param name="ContentTypesInUse">Content types with at least one item that matches the filters.</param>
/// <param name="DefaultLanguage">Display name of the default language, or <c>null</c>.</param>
public sealed record ContentInventoryResult(
    string? Kind,
    int? ChannelId,
    int TotalItems,
    StatsRankedResult ByKind,
    StatsRankedResult ByContentType,
    StatsRankedResult ByStatus,
    IReadOnlyList<StatsCoverageItem> LanguageCoverage,
    ContentAgeSummary Age,
    ContentWorkflowSummary Workflow,
    UnusedReusableSummary? UnusedReusable,
    int TotalVariants,
    int ScheduledPublish,
    int ScheduledUnpublish,
    int ContentTypeCount,
    int ContentTypesInUse,
    string? DefaultLanguage)
{
    /// <summary>
    /// When the data was read from the database. Can be older than the request when served from cache.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// Language variants by time since their last change (<c>ContentItemLanguageMetadataModifiedWhen</c>).
/// </summary>
/// <param name="Buckets">Variants per age, in a fixed order (under 3 months, 3–6, 6–12, over 12 months), 0 included.</param>
/// <param name="NotModified12Months">Variants not changed in the last 12 months.</param>
/// <param name="Oldest">Least recently changed variants, oldest first (up to <see cref="ContentInventoryReportBuilder.ListLimit"/>).</param>
public sealed record ContentAgeSummary(StatsRankedResult Buckets, int NotModified12Months, IReadOnlyList<StatsAgedItem> Oldest);

/// <summary>
/// Language variants in a workflow step. The time a variant entered its step is not stored (version history
/// only records publish and unpublish actions), so the last change of the variant (<c>ModifiedWhen</c>) is used as a proxy.
/// </summary>
/// <param name="InWorkflow">Variants in a workflow step.</param>
/// <param name="Overdue">Variants unchanged in a workflow step for more than <paramref name="OverdueDays"/> days.</param>
/// <param name="OverdueDays">Days after which a variant counts as waiting too long.</param>
/// <param name="Items">
/// Variants in a workflow step, longest unchanged first (up to <see cref="ContentInventoryReportBuilder.ListLimit"/>).
/// <see cref="StatsAgedItem.Detail"/> is the step; <see cref="StatsAgedItem.AdminPath"/> links to the item, or to its workflow's steps when the item cannot be linked.
/// </param>
public sealed record ContentWorkflowSummary(int InWorkflow, int Overdue, int OverdueDays, IReadOnlyList<StatsAgedItem> Items);

/// <summary>
/// Reusable items that no content item references (content item selector fields and Page Builder widget properties).
/// </summary>
/// <param name="Count">Unused reusable items.</param>
/// <param name="ReusableItems">All reusable items.</param>
/// <param name="ByContentType">Unused items per content type, largest first. <see cref="StatsRankedItem.AdminPath"/> links to the content type.</param>
/// <param name="Items">Unused items, least recently changed first (up to <see cref="ContentInventoryReportBuilder.ListLimit"/>).</param>
public sealed record UnusedReusableSummary(int Count, int ReusableItems, StatsRankedResult ByContentType, IReadOnlyList<StatsAgedItem> Items);

/// <summary>
/// Content type with the number of its items that match the filters.
/// </summary>
/// <param name="ClassId">Content type (class) ID.</param>
/// <param name="CodeName">Content type code name.</param>
/// <param name="DisplayName">Content type display name.</param>
/// <param name="Kind">Content type type (<c>ClassContentTypeType</c>).</param>
/// <param name="ItemCount">Items of the type that match the filters.</param>
internal sealed record ContentTypeRow(int ClassId, string CodeName, string DisplayName, string Kind, int ItemCount);

/// <summary>
/// Content language with the number of filtered items that have a variant in it.
/// </summary>
internal sealed record ContentLanguageRow(int LanguageId, string CodeName, string DisplayName, bool IsDefault, int ItemCount);

/// <summary>
/// Language variants with the same latest version status and workflow step.
/// </summary>
/// <param name="VersionStatus">Raw <c>ContentItemLanguageMetadataLatestVersionStatus</c> (<see cref="CMS.ContentEngine.VersionStatus"/>).</param>
/// <param name="StepId">Workflow step ID, or <c>null</c> when not in a workflow step.</param>
/// <param name="StepDisplayName">Workflow step display name, or <c>null</c>.</param>
/// <param name="WorkflowId">Workflow ID of the step, or <c>null</c>.</param>
/// <param name="WorkflowDisplayName">Workflow display name, or <c>null</c>.</param>
/// <param name="VariantCount">Language variants.</param>
/// <param name="ScheduledPublish">Of <paramref name="VariantCount"/>, variants with a scheduled publish.</param>
/// <param name="ScheduledUnpublish">Of <paramref name="VariantCount"/>, variants with a scheduled unpublish.</param>
internal sealed record ContentStatusRow(
    int VersionStatus,
    int? StepId,
    string? StepDisplayName,
    int? WorkflowId,
    string? WorkflowDisplayName,
    int VariantCount,
    int ScheduledPublish,
    int ScheduledUnpublish);

/// <summary>
/// Language variants per age of the last change.
/// </summary>
internal sealed record ContentAgeRow(int Under3Months, int Months3To6, int Months6To12, int Over12Months)
{
    public static ContentAgeRow Empty { get; } = new(0, 0, 0, 0);

    /// <summary>
    /// Reads the current row's <see cref="ContentInventorySql.AgeColumns"/> with the same <paramref name="suffix"/>.
    /// </summary>
    public static ContentAgeRow Read(DbDataReader reader, string suffix = "") =>
        new(
            reader.GetInt32(reader.GetOrdinal("Under3Months" + suffix)),
            reader.GetInt32(reader.GetOrdinal("Months3To6" + suffix)),
            reader.GetInt32(reader.GetOrdinal("Months6To12" + suffix)),
            reader.GetInt32(reader.GetOrdinal("Over12Months" + suffix)));
}

/// <summary>
/// One language variant of a list (oldest variants, variants in a workflow step).
/// </summary>
/// <param name="VariantId">Language metadata ID.</param>
/// <param name="DisplayName">Variant display name.</param>
/// <param name="ContentType">Content type display name.</param>
/// <param name="Language">Language display name.</param>
/// <param name="ModifiedWhen">Last change of the variant's latest version (server time).</param>
/// <param name="StepDisplayName">Workflow step display name, or <c>null</c>.</param>
/// <param name="WorkflowId">Workflow ID, or <c>null</c>.</param>
/// <param name="WorkflowDisplayName">Workflow display name, or <c>null</c>.</param>
internal sealed record ContentVariantRow(
    int VariantId,
    string DisplayName,
    string ContentType,
    string Language,
    DateTime ModifiedWhen,
    string? StepDisplayName = null,
    int? WorkflowId = null,
    string? WorkflowDisplayName = null)
{
    /// <inheritdoc cref="ContentItemLink"/>
    public ContentItemLink? Link { get; init; }
}

/// <summary>
/// Unused reusable item.
/// </summary>
/// <param name="ItemId">Content item ID.</param>
/// <param name="DisplayName">Display name of the most recently changed variant (or the item name).</param>
/// <param name="ContentType">Content type display name.</param>
/// <param name="ModifiedWhen">Last change of any variant, or <c>null</c> when the item has no variant.</param>
internal sealed record UnusedItemRow(int ItemId, string DisplayName, string ContentType, DateTime? ModifiedWhen)
{
    /// <inheritdoc cref="ContentItemLink"/>
    public ContentItemLink? Link { get; init; }
}

/// <summary>
/// Aggregates read by <see cref="IContentInventoryRepository"/>.
/// </summary>
internal sealed record ContentInventoryData(
    IReadOnlyList<ContentTypeRow> ContentTypes,
    IReadOnlyList<ContentLanguageRow> Languages,
    IReadOnlyList<ContentStatusRow> Statuses)
{
    public static ContentInventoryData Empty { get; } = new([], [], []);

    /// <summary>
    /// Server time the ages are counted to (the read time).
    /// </summary>
    public DateTime Now { get; init; }

    public ContentAgeRow Age { get; init; } = ContentAgeRow.Empty;

    /// <summary>Least recently changed variants, oldest first.</summary>
    public IReadOnlyList<ContentVariantRow> Oldest { get; init; } = [];

    /// <summary>Variants in a workflow step, longest unchanged first.</summary>
    public IReadOnlyList<ContentVariantRow> WorkflowItems { get; init; } = [];

    /// <summary>Variants in a workflow step unchanged for more than <see cref="ContentInventoryReportBuilder.OverdueDays"/> days (all, not only <see cref="WorkflowItems"/>).</summary>
    public int WorkflowOverdue { get; init; }

    /// <summary>Unused reusable items per content type (<see cref="ContentTypeRow.ItemCount"/> = unused items).</summary>
    public IReadOnlyList<ContentTypeRow> UnusedByContentType { get; init; } = [];

    /// <summary>Unused reusable items, least recently changed first.</summary>
    public IReadOnlyList<UnusedItemRow> UnusedItems { get; init; } = [];
}

/// <summary>
/// Content inventory data with the time it was read. This is the cached value.
/// </summary>
internal sealed record ContentInventorySnapshot(ContentInventoryData Data, DateTimeOffset ReadAt);
