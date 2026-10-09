using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContentLocks;

/// <summary>
/// Language variants locked for editing (content locking), per user and oldest lock first. Counts are per language variant.
/// </summary>
/// <param name="LockingEnabled">
/// Whether content locking is enabled (Settings → Content). Locks that exist while it is disabled are still reported.
/// </param>
/// <param name="Kind">Applied content type type filter (<c>Website</c>, <c>Reusable</c>, <c>Email</c>, <c>Headless</c>), or <c>null</c> for all.</param>
/// <param name="ChannelId">Applied channel filter, or <c>null</c> for all.</param>
/// <param name="LockedCount">Locked language variants.</param>
/// <param name="UserCount">Users holding locks (users that no longer exist count as one).</param>
/// <param name="OldLockCount">Locks older than <paramref name="OldLockDays"/> days.</param>
/// <param name="OldLockDays">Days after which a lock counts as old (see <see cref="ContentLocksReportBuilder.OldLockDays"/>).</param>
/// <param name="OldestLockedSince">Date of the oldest lock (server date), or <c>null</c> without locks.</param>
/// <param name="OldestLockDays">Whole days the oldest lock is held, or <c>null</c> without locks.</param>
/// <param name="ByUser">
/// Locks per user, most first. <see cref="StatsRankedItem.SecondaryValue"/> is the age of the user's oldest lock in whole days;
/// <see cref="StatsRankedItem.AdminPath"/> opens the user in the Users application.
/// </param>
/// <param name="Items">
/// Locked variants, oldest lock first (up to <see cref="ContentLocksReportBuilder.ListLimit"/>).
/// <see cref="StatsAgedItem.Detail"/> is the user holding the lock, <see cref="StatsAgedItem.Since"/> the lock date,
/// <see cref="StatsAgedItem.Channel"/> the channel text and <see cref="StatsAgedItem.LastModified"/> the variant's last change.
/// </param>
public sealed record ContentLocksResult(
    bool LockingEnabled,
    string? Kind,
    int? ChannelId,
    int LockedCount,
    int UserCount,
    int OldLockCount,
    int OldLockDays,
    DateOnly? OldestLockedSince,
    int? OldestLockDays,
    StatsRankedResult ByUser,
    IReadOnlyList<StatsAgedItem> Items)
{
    /// <summary>
    /// When the data was read from the database. Can be older than the request when served from cache.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// Locks of one user.
/// </summary>
/// <param name="UserId">User ID, or <c>null</c> for locks of users that no longer exist (one row).</param>
/// <param name="UserName">User display name, or <c>null</c>.</param>
/// <param name="LockCount">Locked variants.</param>
/// <param name="OldestLockedWhen">Time of the user's oldest lock (server time).</param>
internal sealed record ContentLockUserRow(int? UserId, string? UserName, int LockCount, DateTime OldestLockedWhen);

/// <summary>
/// One locked language variant.
/// </summary>
/// <param name="Id">Language metadata ID.</param>
/// <param name="DisplayName">Variant display name.</param>
/// <param name="ContentType">Content type display name.</param>
/// <param name="Language">Language display name.</param>
/// <param name="UserId">ID of the user holding the lock, or <c>null</c> when the user no longer exists.</param>
/// <param name="UserName">Display name of the user holding the lock, or <c>null</c>.</param>
/// <param name="LockedWhen">Lock time (server time).</param>
/// <param name="ModifiedWhen">Last change of the variant's latest version (server time).</param>
internal sealed record ContentLockRow(
    int Id,
    string DisplayName,
    string ContentType,
    string Language,
    int? UserId,
    string? UserName,
    DateTime LockedWhen,
    DateTime ModifiedWhen)
{
    /// <inheritdoc cref="ContentItemLink"/>
    public ContentItemLink? Link { get; init; }

    /// <summary>Channel display name, or <c>null</c>.</summary>
    public string? Channel { get; init; }

    /// <summary>Whether the item is a reusable item (it has no channel, but a workspace in the Content hub).</summary>
    public bool IsReusable { get; init; }

    /// <summary>Display name of the item's workspace, or <c>null</c>.</summary>
    public string? Workspace { get; init; }
}

/// <summary>
/// Data read by <see cref="IContentLocksRepository"/>.
/// </summary>
/// <param name="OldLockCount">Locks older than <see cref="ContentLocksReportBuilder.OldLockDays"/> days (all, not only <paramref name="Items"/>).</param>
/// <param name="Users">Locks per user (all users).</param>
/// <param name="Items">Locked variants, oldest lock first (up to <see cref="ContentLocksReportBuilder.ListLimit"/>).</param>
internal sealed record ContentLocksData(
    int OldLockCount,
    IReadOnlyList<ContentLockUserRow> Users,
    IReadOnlyList<ContentLockRow> Items)
{
    public static ContentLocksData Empty { get; } = new(0, [], []);

    /// <summary>
    /// Server time lock ages are counted from (the read time).
    /// </summary>
    public DateTime Now { get; init; }
}

/// <summary>
/// Content locks data with the time it was read. This is the cached value.
/// </summary>
internal sealed record ContentLocksSnapshot(ContentLocksData Data, DateTimeOffset ReadAt);
