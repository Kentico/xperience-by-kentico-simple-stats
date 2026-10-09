using System.Globalization;

using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ReusableUsage;

/// <summary>
/// Turns the usage of reusable items into the reusable content usage report.
/// </summary>
internal static class ReusableUsageReportBuilder
{
    /// <summary>
    /// Rows of the most used items list.
    /// </summary>
    public const int ListLimit = 25;

    /// <summary>
    /// Highest usage count of the "2–5 usages" bucket.
    /// </summary>
    public const int FewUsagesMax = 5;

    /// <summary>
    /// Highest usage count of the "6–20 usages" bucket. More is "Over 20 usages".
    /// </summary>
    public const int SomeUsagesMax = 20;

    // Distribution bucket keys, also used by the client.
    public const string UnusedKey = "unused";
    public const string OnceKey = "1";
    public const string FewKey = "2-5";
    public const string SomeKey = "6-20";
    public const string ManyKey = "over-20";

    /// <summary>
    /// Content type filter options: reusable content types with items, by display name.
    /// </summary>
    /// <param name="contentTypes">Usage per content type of all reusable items (no content type filter).</param>
    public static IReadOnlyList<ReusableUsageContentTypeOption> GetContentTypeOptions(IEnumerable<ReusableUsageTypeRow> contentTypes) =>
    [
        .. contentTypes
            .Where(t => t.ItemCount > 0)
            .DistinctBy(t => t.ClassId)
            .Select(t => new ReusableUsageContentTypeOption(t.ClassId, GetTypeLabel(t), t.ItemCount))
            .OrderBy(o => o.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(o => o.Id),
    ];

    /// <summary>
    /// Returns <paramref name="contentTypeId"/> when it is one of <paramref name="options"/>, else <c>null</c> (all content types).
    /// </summary>
    public static int? NormalizeContentType(int? contentTypeId, IEnumerable<ReusableUsageContentTypeOption> options) =>
        contentTypeId is int id && id > 0 && options.Any(o => o.Id == id) ? id : null;

    /// <summary>
    /// Builds the report.
    /// </summary>
    /// <param name="contentTypeId">Normalized content type filter, or <c>null</c> for all.</param>
    /// <param name="data">Data for the filter.</param>
    /// <param name="options">Content type filter options (see <see cref="GetContentTypeOptions"/>).</param>
    /// <param name="getContentItemPath">Returns the admin path of an item in the Content hub, or <c>null</c>. Items without link data get no link.</param>
    /// <param name="getContentTypePath">Returns the admin path of a content type by class ID, or <c>null</c>.</param>
    public static ReusableUsageResult Build(
        int? contentTypeId,
        ReusableUsageData data,
        IReadOnlyList<ReusableUsageContentTypeOption> options,
        Func<ContentItemLink, string?>? getContentItemPath = null,
        Func<int, string?>? getContentTypePath = null)
    {
        var totals = data.Totals;
        int reusableItems = Math.Max(totals.ReusableItems, 0);
        int usedItems = Math.Clamp(totals.UsedItems, 0, reusableItems);
        int unused = Math.Clamp(totals.Unused, 0, reusableItems - usedItems);

        var distribution = StatsRankedBuilder.BuildSnapshot(
            null,
            GetDistributionEntries(totals),
            reusableItems,
            0,
            limit: int.MaxValue,
            includeZero: true,
            keepOrder: true);

        var contentTypes = data.ContentTypes.DistinctBy(t => t.ClassId).ToList();

        var byContentType = StatsRankedBuilder.BuildSnapshot(
            null,
            contentTypes.Select(t => new StatsRankedEntry(t.CodeName, GetTypeLabel(t), null, Math.Max(t.Usages, 0), t.ItemCount, null)
            {
                TertiaryValue = t.UsedItems,
                AdminPath = getContentTypePath?.Invoke(t.ClassId),
            }),
            Math.Max(totals.Usages, 0),
            contentTypes.Count,
            limit: contentTypes.Count,
            includeZero: true);

        var mostUsed = data.MostUsed
            .Where(row => row.Usages > 0)
            .Take(ListLimit)
            .Select(row => new ReusableUsageItem(
                row.ItemId.ToString(CultureInfo.InvariantCulture),
                row.DisplayName,
                string.IsNullOrWhiteSpace(row.ContentType) ? null : row.ContentType,
                StatsContentChannels.GetLabel(null, isReusable: true, row.Workspace),
                row.Pages,
                row.Emails,
                row.ReusableItems,
                row.HeadlessItems,
                row.Usages,
                row.ModifiedWhen is DateTime modified ? DateOnly.FromDateTime(modified) : null)
            {
                AdminPath = row.Link is null || getContentItemPath is null ? null : getContentItemPath(row.Link),
            })
            .ToList();

        return new(
            contentTypeId,
            reusableItems,
            usedItems,
            reusableItems > 0 ? (double)usedItems / reusableItems : 0,
            Math.Clamp(totals.UsedOnce, 0, usedItems),
            unused,
            Math.Max(totals.Usages, 0),
            mostUsed,
            distribution,
            byContentType,
            options);
    }

    /// <summary>
    /// Buckets in a fixed order (unused, 1, 2–<see cref="FewUsagesMax"/>, to <see cref="SomeUsagesMax"/>, more), 0 included.
    /// </summary>
    private static IEnumerable<StatsRankedEntry> GetDistributionEntries(ReusableUsageTotals totals) =>
    [
        new(UnusedKey, "Unused", null, Math.Max(totals.Unused, 0), null, null),
        new(OnceKey, "1 usage", null, Math.Max(totals.UsedOnce, 0), null, null),
        new(FewKey, string.Create(CultureInfo.InvariantCulture, $"2–{FewUsagesMax} usages"), null, Math.Max(totals.UsedFew, 0), null, null),
        new(SomeKey, string.Create(CultureInfo.InvariantCulture, $"{FewUsagesMax + 1}–{SomeUsagesMax} usages"), null, Math.Max(totals.UsedSome, 0), null, null),
        new(ManyKey, string.Create(CultureInfo.InvariantCulture, $"Over {SomeUsagesMax} usages"), null, Math.Max(totals.UsedMany, 0), null, null),
    ];

    private static string GetTypeLabel(ReusableUsageTypeRow type) =>
        string.IsNullOrWhiteSpace(type.DisplayName) ? type.CodeName : type.DisplayName;
}
