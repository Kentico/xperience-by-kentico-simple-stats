namespace Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

/// <summary>
/// Filter of a current-state (snapshot) report, sent by the admin client. Snapshot reports have no date range or grouping,
/// so they use this filter instead of <see cref="StatsFilter"/>: nothing time-based splits their cache key.
/// Missing or invalid values fall back to "all" in <see cref="Normalize"/>.
/// </summary>
public sealed record StatsSnapshotFilter
{
    /// <summary>
    /// Optional report-specific kind (for example a content type type such as <c>Website</c>).
    /// <c>null</c> or a value the report does not know means all kinds.
    /// </summary>
    public string? Kind { get; init; }

    /// <summary>
    /// Optional channel ID. <c>null</c> or a value &lt;= 0 means all channels.
    /// </summary>
    public int? ChannelId { get; init; }

    /// <summary>
    /// Optional number of days a report looks ahead or back from now (for example the publishing calendar).
    /// Only reports with a window read it (see <see cref="NormalizeWindow"/>); it is not part of <see cref="StatsSnapshotQuery"/>.
    /// </summary>
    public int? Window { get; init; }

    /// <summary>
    /// Optional content type (class) ID (for example the reusable content usage report). Only reports with a content type filter read it
    /// and check it against their own options; it is not part of <see cref="StatsSnapshotQuery"/>. <c>null</c> or an unknown ID means all.
    /// </summary>
    public int? ContentTypeId { get; init; }

    /// <summary>
    /// Optional content language ID (for example the translation status report). Only reports with a language filter read it
    /// and check it against their own options; it is not part of <see cref="StatsSnapshotQuery"/>. <c>null</c> or an unknown ID means all.
    /// </summary>
    public int? LanguageId { get; init; }

    /// <summary>
    /// Optional taxonomy ID (for example the tag usage report). Only reports with a taxonomy filter read it
    /// and check it against their own options; it is not part of <see cref="StatsSnapshotQuery"/>. <c>null</c> or an unknown ID means all.
    /// </summary>
    public int? TaxonomyId { get; init; }

    /// <summary>
    /// Returns <see cref="Window"/> when it is one of <paramref name="windows"/>, else <paramref name="defaultWindow"/>.
    /// </summary>
    public int NormalizeWindow(IReadOnlyCollection<int> windows, int defaultWindow) =>
        Window is int window && windows.Contains(window) ? window : defaultWindow;

    /// <summary>
    /// Applies defaults and returns a query that is safe to run.
    /// </summary>
    /// <param name="kinds">Kinds the report supports. <see cref="Kind"/> is matched case-insensitively and returned in this casing.</param>
    /// <param name="isChannelAllowed">
    /// Optional check of the channel against the normalized kind (<c>null</c> for all kinds).
    /// A channel it rejects is dropped (all channels), so the cache key has no channel that cannot apply.
    /// </param>
    public StatsSnapshotQuery Normalize(IEnumerable<string> kinds, Func<string?, int, bool>? isChannelAllowed = null)
    {
        string? kind = string.IsNullOrWhiteSpace(Kind)
            ? null
            : kinds.FirstOrDefault(k => string.Equals(k, Kind.Trim(), StringComparison.OrdinalIgnoreCase));
        int? channelId = ChannelId is int id && id > 0 && (isChannelAllowed?.Invoke(kind, id) ?? true) ? id : null;

        return new(kind, channelId);
    }
}

/// <summary>
/// Normalized filter used by snapshot report queries.
/// </summary>
/// <param name="Kind">One of the kinds the report supports, or <c>null</c> for all.</param>
/// <param name="ChannelId">Optional channel ID.</param>
public sealed record StatsSnapshotQuery(string? Kind, int? ChannelId);

/// <summary>
/// Input of a snapshot report <c>LOAD</c> page command.
/// </summary>
public sealed record StatsSnapshotLoadRequest
{
    /// <summary>
    /// Report filter. <c>null</c> means all.
    /// </summary>
    public StatsSnapshotFilter? Filter { get; init; }

    /// <summary>
    /// When <c>true</c>, cached data for the filter is dropped and read again from the database.
    /// </summary>
    public bool Refresh { get; init; }
}
