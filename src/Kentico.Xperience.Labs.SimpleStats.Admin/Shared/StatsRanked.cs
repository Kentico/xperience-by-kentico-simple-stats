using System.Text.Json.Serialization;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

/// <summary>
/// One row of a ranked list (top pages, top referrers, forms by submissions, ...).
/// </summary>
/// <param name="Rank">1-based position in the list.</param>
/// <param name="Key">Stable identifier of the item (for example the URL). Unique in the list.</param>
/// <param name="Label">Primary label shown in charts and tables.</param>
/// <param name="SecondaryLabel">Optional extra text shown under or next to the label.</param>
/// <param name="Value">
/// Ranked value (for example visits, or revenue). Decimal so amounts fit; counts are whole numbers and serialize as before.
/// See <see cref="StatsRankedResult.ValueKind"/>.
/// </param>
/// <param name="SecondaryValue">Optional second number (for example unique contacts). See <see cref="StatsRankedResult.SecondaryValueKind"/>.</param>
/// <param name="Share">Share of <see cref="StatsRankedResult.Total"/> (0-1).</param>
/// <param name="Url">Optional absolute link opened in a new tab.</param>
public sealed record StatsRankedItem(
    int Rank,
    string Key,
    string Label,
    string? SecondaryLabel,
    decimal Value,
    decimal? SecondaryValue,
    double Share,
    string? Url)
{
    /// <summary>
    /// Optional link to a native admin page of the item (for example a form's submissions).
    /// Path relative to the admin root, see <see cref="StatsAdminLinks"/>. Opened in the same tab.
    /// </summary>
    public string? AdminPath { get; init; }

    /// <summary>
    /// Optional value of the item in the previous period (see <see cref="StatsComparison.GetPreviousRange"/>).
    /// <c>null</c> when the report does not compare periods. Left out of the JSON when <c>null</c>,
    /// so reports without a comparison send the same data as before.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? PreviousValue { get; init; }

    /// <summary>
    /// Relative change of <see cref="Value"/> vs <see cref="PreviousValue"/> as a ratio (0.12 = +12%), see <see cref="StatsComparison.GetChange(decimal, decimal)"/>.
    /// <c>null</c> (left out of the JSON) when the report does not compare periods or the previous value is 0.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Change { get; init; }

    /// <summary>
    /// <see cref="Value"/> formatted by the project's price formatter when it is an amount (see <see cref="StatsAmountTexts"/>).
    /// <c>null</c> (left out of the JSON) when not formatted; the client then formats the number.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ValueText { get; init; }

    /// <summary>
    /// <see cref="SecondaryValue"/> formatted like <see cref="ValueText"/> when it is an amount.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SecondaryValueText { get; init; }

    /// <summary>
    /// <see cref="PreviousValue"/> formatted like <see cref="ValueText"/>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PreviousValueText { get; init; }

    /// <summary>
    /// Optional third number (for example item quantity next to revenue and orders). See <see cref="StatsRankedResult.TertiaryValueKind"/>.
    /// <c>null</c> (left out of the JSON) for lists with up to two values, so they send the same data as before.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? TertiaryValue { get; init; }

    /// <summary>
    /// <see cref="TertiaryValue"/> formatted like <see cref="ValueText"/> when it is an amount.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TertiaryValueText { get; init; }

    /// <summary>
    /// Optional meaning of the item for chart colors (for example an order status that is a problem).
    /// <c>null</c> (left out of the JSON) for lists without it, so they send the same data as before.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public StatsTone? Tone { get; init; }
}

/// <summary>
/// Ranked list for one range and channel.
/// </summary>
/// <param name="From">Applied range start (inclusive). <see cref="DateOnly.MinValue"/> for snapshot reports (no range).</param>
/// <param name="To">Applied range end (inclusive). <see cref="DateOnly.MinValue"/> for snapshot reports (no range).</param>
/// <param name="ChannelId">Applied channel filter.</param>
/// <param name="Items">Top items, largest value first.</param>
/// <param name="Total">Sum of values over all items in the range, not only <paramref name="Items"/>.</param>
/// <param name="ItemCount">Number of distinct items in the range, not only <paramref name="Items"/>.</param>
public sealed record StatsRankedResult(
    DateOnly From,
    DateOnly To,
    int? ChannelId,
    IReadOnlyList<StatsRankedItem> Items,
    decimal Total,
    int ItemCount)
{
    /// <summary>
    /// What <see cref="StatsRankedItem.Value"/>, <see cref="StatsRankedItem.PreviousValue"/> and <see cref="Total"/> measure.
    /// <c>null</c> (left out of the JSON) means <see cref="StatsValueKind.Count"/>, so count reports send the same data as before.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public StatsValueKind? ValueKind { get; init; }

    /// <summary>
    /// What <see cref="StatsRankedItem.SecondaryValue"/> measures. <c>null</c> (left out of the JSON) means <see cref="StatsValueKind.Count"/>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public StatsValueKind? SecondaryValueKind { get; init; }

    /// <summary>
    /// What <see cref="StatsRankedItem.TertiaryValue"/> measures. <c>null</c> (left out of the JSON) means <see cref="StatsValueKind.Count"/>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public StatsValueKind? TertiaryValueKind { get; init; }

    /// <summary>
    /// <see cref="Total"/> formatted by the project's price formatter when it is an amount. <c>null</c> (left out of the JSON) otherwise.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TotalText { get; init; }

    /// <summary>
    /// When the data was read from the database. Can be older than the request when served from cache.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// Unranked input row for <see cref="StatsRankedBuilder"/>.
/// </summary>
public sealed record StatsRankedEntry(
    string Key,
    string Label,
    string? SecondaryLabel,
    decimal Value,
    decimal? SecondaryValue,
    string? Url)
{
    /// <inheritdoc cref="StatsRankedItem.AdminPath"/>
    public string? AdminPath { get; init; }

    /// <summary>
    /// Optional value in the previous period. When set, the ranked item gets <see cref="StatsRankedItem.PreviousValue"/>
    /// and <see cref="StatsRankedItem.Change"/>. <c>null</c> (default) leaves both empty.
    /// </summary>
    public decimal? PreviousValue { get; init; }

    /// <inheritdoc cref="StatsRankedItem.TertiaryValue"/>
    public decimal? TertiaryValue { get; init; }

    /// <inheritdoc cref="StatsRankedItem.Tone"/>
    public StatsTone? Tone { get; init; }
}

/// <summary>
/// Orders entries, applies the limit and computes shares.
/// </summary>
public static class StatsRankedBuilder
{
    /// <summary>
    /// Builds a ranked result.
    /// </summary>
    /// <param name="query">Normalized filter.</param>
    /// <param name="entries">
    /// Entries (usually already top N from SQL). Entries with a value &lt;= 0 are dropped
    /// (value 0 is kept with <paramref name="includeZero"/>); duplicate keys keep the first.
    /// </param>
    /// <param name="total">Sum of values over all items in the range. Raised to the sum of <paramref name="entries"/> if lower.</param>
    /// <param name="itemCount">Number of distinct items in the range. Raised to the number of entries if lower.</param>
    /// <param name="limit">Maximum number of items.</param>
    /// <param name="includeZero">
    /// When <c>true</c>, entries with value 0 are kept and listed last (for example unused forms).
    /// Negative values are always dropped.
    /// </param>
    /// <param name="keepOrder">
    /// When <c>true</c>, entries keep their order (for example fixed categories such as statuses) instead of being sorted by value.
    /// </param>
    public static StatsRankedResult Build(
        StatsQuery query,
        IEnumerable<StatsRankedEntry> entries,
        decimal total,
        int itemCount,
        int limit,
        bool includeZero = false,
        bool keepOrder = false) =>
        Build(query.From, query.To, query.ChannelId, entries, total, itemCount, limit, includeZero, keepOrder, itemsOverlap: false);

    /// <summary>
    /// Builds a ranked result for a current-state (snapshot) report. Same rules as
    /// <see cref="Build(StatsQuery, IEnumerable{StatsRankedEntry}, decimal, int, int, bool, bool)"/>, but the result has no range:
    /// <see cref="StatsRankedResult.From"/> and <see cref="StatsRankedResult.To"/> are <see cref="DateOnly.MinValue"/> and not used.
    /// </summary>
    /// <param name="channelId">Applied channel filter.</param>
    /// <param name="entries">Entries. See the other overload.</param>
    /// <param name="total">Sum of values over all items. See the other overload.</param>
    /// <param name="itemCount">Number of distinct items. See the other overload.</param>
    /// <param name="limit">Maximum number of items.</param>
    /// <param name="includeZero">When <c>true</c>, entries with value 0 are kept.</param>
    /// <param name="keepOrder">
    /// When <c>true</c>, entries keep their order (for example fixed categories such as statuses, so chart colors stay stable)
    /// instead of being sorted by value.
    /// </param>
    /// <param name="itemsOverlap">
    /// When <c>true</c>, one thing can count in several entries (for example a member in several roles), so values can add up to more
    /// than <paramref name="total"/>. The total is then kept (not raised to the sum), and shares are of <paramref name="total"/>
    /// (for example the share of all members in a role). Shares then do not add up to 100%.
    /// </param>
    public static StatsRankedResult BuildSnapshot(
        int? channelId,
        IEnumerable<StatsRankedEntry> entries,
        decimal total,
        int itemCount,
        int limit,
        bool includeZero = false,
        bool keepOrder = false,
        bool itemsOverlap = false) =>
        Build(DateOnly.MinValue, DateOnly.MinValue, channelId, entries, total, itemCount, limit, includeZero, keepOrder, itemsOverlap);

    private static StatsRankedResult Build(
        DateOnly from,
        DateOnly to,
        int? channelId,
        IEnumerable<StatsRankedEntry> entries,
        decimal total,
        int itemCount,
        int limit,
        bool includeZero,
        bool keepOrder,
        bool itemsOverlap)
    {
        var valid = entries
            .Where(e => e.Value > 0 || (includeZero && e.Value == 0))
            .DistinctBy(e => e.Key, StringComparer.Ordinal)
            .ToList();

        // Overlapping entries: each value is at most the total, so only raise the total to the largest value.
        decimal safeTotal = itemsOverlap
            ? Math.Max(total, valid.Select(e => e.Value).DefaultIfEmpty(0).Max())
            : Math.Max(total, valid.Sum(e => e.Value));
        int safeCount = Math.Max(itemCount, valid.Count);

        IEnumerable<StatsRankedEntry> ordered = keepOrder
            ? valid
            : valid
                .OrderByDescending(e => e.Value)
                .ThenBy(e => e.Key, StringComparer.Ordinal);

        var items = ordered
            .Take(Math.Max(limit, 0))
            .Select((e, index) => new StatsRankedItem(
                index + 1,
                e.Key,
                e.Label,
                e.SecondaryLabel,
                e.Value,
                e.SecondaryValue,
                safeTotal > 0 ? (double)e.Value / (double)safeTotal : 0,
                e.Url)
            {
                AdminPath = e.AdminPath,
                PreviousValue = e.PreviousValue,
                TertiaryValue = e.TertiaryValue,
                Tone = e.Tone,
                Change = e.PreviousValue is decimal previous ? StatsComparison.GetChange(e.Value, previous) : null,
            })
            .ToList();

        return new(from, to, channelId, items, safeTotal, safeCount);
    }
}
