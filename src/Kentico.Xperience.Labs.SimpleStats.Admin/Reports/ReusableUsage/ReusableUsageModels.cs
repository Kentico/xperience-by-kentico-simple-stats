using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ReusableUsage;

/// <summary>
/// Usage of reusable items: which items other content items reference the most, and where (pages, emails, reusable and headless items).
/// A usage is one distinct referencing content item (see <see cref="StatsContentUsageSql"/>), the same definition as the
/// content inventory's unused reusable items, so <paramref name="UsedItems"/> + <paramref name="Unused"/> = <paramref name="ReusableItems"/>.
/// </summary>
/// <param name="ContentTypeId">Applied content type filter (class ID of a reusable content type), or <c>null</c> for all.</param>
/// <param name="ReusableItems">Reusable items of the selected content type.</param>
/// <param name="UsedItems">Of <paramref name="ReusableItems"/>, items with at least one usage.</param>
/// <param name="UsedShare"><paramref name="UsedItems"/> / <paramref name="ReusableItems"/> (0–1), 0 without items.</param>
/// <param name="UsedOnce">Of <paramref name="UsedItems"/>, items with exactly one usage.</param>
/// <param name="Unused">Of <paramref name="ReusableItems"/>, items without usages.</param>
/// <param name="Usages">Usages of all items (sum over items; a content item that references two items counts twice).</param>
/// <param name="MostUsed">Most used items, most usages first (up to <see cref="ReusableUsageReportBuilder.ListLimit"/>).</param>
/// <param name="Distribution">
/// Items per number of usages, in a fixed order (unused, 1, 2–5, 6–20, over 20), 0 included. Shares are of <paramref name="ReusableItems"/>.
/// </param>
/// <param name="ByContentType">
/// Usages per content type, most first (types without usages last). <see cref="StatsRankedItem.SecondaryValue"/> is the type's items,
/// <see cref="StatsRankedItem.TertiaryValue"/> its used items, <see cref="StatsRankedItem.AdminPath"/> the content type.
/// </param>
/// <param name="ContentTypeOptions">Reusable content types with items, by display name (the content type filter).</param>
public sealed record ReusableUsageResult(
    int? ContentTypeId,
    int ReusableItems,
    int UsedItems,
    double UsedShare,
    int UsedOnce,
    int Unused,
    int Usages,
    IReadOnlyList<ReusableUsageItem> MostUsed,
    StatsRankedResult Distribution,
    StatsRankedResult ByContentType,
    IReadOnlyList<ReusableUsageContentTypeOption> ContentTypeOptions)
{
    /// <summary>
    /// When the data was read from the database. Can be older than the request when served from cache.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// One used reusable item with its usages per kind of referencing item.
/// </summary>
/// <param name="Key">Content item ID. Unique in the list.</param>
/// <param name="Name">Display name of the most recently changed language variant (or the item name).</param>
/// <param name="ContentType">Content type display name, or <c>null</c>.</param>
/// <param name="Channel">"Content hub - " and the workspace (see <see cref="StatsContentChannels"/>).</param>
/// <param name="Pages">Pages that reference the item.</param>
/// <param name="Emails">Emails that reference the item.</param>
/// <param name="ReusableItems">Reusable items that reference the item.</param>
/// <param name="HeadlessItems">Headless items that reference the item.</param>
/// <param name="Usages">Content items that reference the item (all kinds).</param>
/// <param name="LastModified">Last change of any language variant (server date), or <c>null</c> when the item has no variant.</param>
public sealed record ReusableUsageItem(
    string Key,
    string Name,
    string? ContentType,
    string? Channel,
    int Pages,
    int Emails,
    int ReusableItems,
    int HeadlessItems,
    int Usages,
    DateOnly? LastModified)
{
    /// <summary>
    /// The item in the Content hub (relative to the admin root, see <see cref="StatsContentItemPaths"/>), or <c>null</c>.
    /// </summary>
    public string? AdminPath { get; init; }
}

/// <summary>
/// Reusable content type of the content type filter.
/// </summary>
/// <param name="Id">Content type (class) ID.</param>
/// <param name="DisplayName">Content type display name.</param>
/// <param name="ItemCount">Reusable items of the type.</param>
public sealed record ReusableUsageContentTypeOption(int Id, string DisplayName, int ItemCount);

/// <summary>
/// Reusable items by number of usages (one row).
/// </summary>
/// <param name="ReusableItems">Reusable items.</param>
/// <param name="UsedItems">Items with at least one usage.</param>
/// <param name="Unused">Items without usages.</param>
/// <param name="UsedOnce">Items with 1 usage.</param>
/// <param name="UsedFew">Items with 2 to <see cref="ReusableUsageReportBuilder.FewUsagesMax"/> usages.</param>
/// <param name="UsedSome">Items with more, up to <see cref="ReusableUsageReportBuilder.SomeUsagesMax"/> usages.</param>
/// <param name="UsedMany">Items with more than <see cref="ReusableUsageReportBuilder.SomeUsagesMax"/> usages.</param>
/// <param name="Usages">Usages of all items.</param>
internal sealed record ReusableUsageTotals(
    int ReusableItems,
    int UsedItems,
    int Unused,
    int UsedOnce,
    int UsedFew,
    int UsedSome,
    int UsedMany,
    int Usages)
{
    public static ReusableUsageTotals Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0);
}

/// <summary>
/// One used reusable item, as read from the database.
/// </summary>
/// <param name="ItemId">Content item ID.</param>
/// <param name="DisplayName">Display name of the most recently changed variant (or the item name).</param>
/// <param name="ContentType">Content type display name.</param>
/// <param name="Usages">Referencing content items (all kinds).</param>
/// <param name="Pages">Referencing pages.</param>
/// <param name="Emails">Referencing emails.</param>
/// <param name="ReusableItems">Referencing reusable items.</param>
/// <param name="HeadlessItems">Referencing headless items.</param>
/// <param name="ModifiedWhen">Last change of any variant (server time), or <c>null</c> when the item has no variant.</param>
internal sealed record ReusableUsageRow(
    int ItemId,
    string DisplayName,
    string ContentType,
    int Usages,
    int Pages,
    int Emails,
    int ReusableItems,
    int HeadlessItems,
    DateTime? ModifiedWhen)
{
    /// <inheritdoc cref="ContentItemLink"/>
    public ContentItemLink? Link { get; init; }

    /// <summary>Display name of the item's workspace, or <c>null</c>.</summary>
    public string? Workspace { get; init; }
}

/// <summary>
/// Usage of the reusable items of one content type.
/// </summary>
/// <param name="ClassId">Content type (class) ID.</param>
/// <param name="CodeName">Content type code name.</param>
/// <param name="DisplayName">Content type display name.</param>
/// <param name="ItemCount">Reusable items of the type.</param>
/// <param name="UsedItems">Of <paramref name="ItemCount"/>, items with at least one usage.</param>
/// <param name="Usages">Usages of the type's items.</param>
internal sealed record ReusableUsageTypeRow(int ClassId, string CodeName, string DisplayName, int ItemCount, int UsedItems, int Usages);

/// <summary>
/// Data read by <see cref="IReusableUsageRepository"/>.
/// </summary>
/// <param name="Totals">Items by number of usages.</param>
/// <param name="MostUsed">Most used items, most usages first.</param>
/// <param name="ContentTypes">Usage per content type (types with items only).</param>
internal sealed record ReusableUsageData(
    ReusableUsageTotals Totals,
    IReadOnlyList<ReusableUsageRow> MostUsed,
    IReadOnlyList<ReusableUsageTypeRow> ContentTypes)
{
    public static ReusableUsageData Empty { get; } = new(ReusableUsageTotals.Empty, [], []);
}

/// <summary>
/// Reusable content usage data with the time it was read. This is the cached value.
/// </summary>
internal sealed record ReusableUsageSnapshot(ReusableUsageData Data, DateTimeOffset ReadAt);
