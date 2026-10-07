using System.Text.Json.Serialization;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

/// <summary>
/// One row of an "oldest first" list (for example content not modified for a long time).
/// </summary>
/// <param name="Key">Stable identifier of the row. Unique in the list.</param>
/// <param name="Label">Primary label (for example the item's display name).</param>
/// <param name="Category">Optional grouping text (for example the content type).</param>
/// <param name="Language">Optional language display name.</param>
/// <param name="Detail">Optional extra text (for example the workflow step).</param>
/// <param name="Since">Date the age is counted from (for example the last modification), server date.</param>
/// <param name="Days">Whole days from <paramref name="Since"/> to when the data was read.</param>
public sealed record StatsAgedItem(
    string Key,
    string Label,
    string? Category,
    string? Language,
    string? Detail,
    DateOnly Since,
    int Days)
{
    /// <inheritdoc cref="StatsRankedItem.AdminPath"/>
    public string? AdminPath { get; init; }

    /// <summary>
    /// Optional channel text (for example "Content hub - Marketing" for reusable items, see <see cref="StatsContentChannels"/>).
    /// <c>null</c> (left out of the JSON) for lists without a channel column.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Channel { get; init; }

    /// <summary>
    /// Optional date of the last change when the age counts from something else (for example a lock), server date.
    /// <c>null</c> (left out of the JSON) for lists without it.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateOnly? LastModified { get; init; }

    /// <summary>
    /// Returns whole days from <paramref name="since"/> to <paramref name="now"/>, never below 0.
    /// </summary>
    public static int GetDays(DateTime since, DateTime now) => Math.Max((int)(now - since).TotalDays, 0);
}
