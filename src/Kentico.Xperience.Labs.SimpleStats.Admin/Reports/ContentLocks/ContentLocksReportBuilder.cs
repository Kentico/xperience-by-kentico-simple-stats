using System.Globalization;

using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContentLocks;

/// <summary>
/// Turns locked language variants into the content locks report.
/// </summary>
internal static class ContentLocksReportBuilder
{
    /// <summary>
    /// Locks held for more than this many days are old (highlighted, so an administrator can follow up).
    /// </summary>
    public const int OldLockDays = 3;

    /// <summary>
    /// Rows of the locked variants list.
    /// </summary>
    public const int ListLimit = 50;

    /// <summary>
    /// Rows of the locks per user list.
    /// </summary>
    public const int UserLimit = 50;

    /// <summary>Key of the row with the locks of users that no longer exist.</summary>
    public const string UnknownUserKey = "(unknown)";

    /// <summary>Label of the row with the locks of users that no longer exist.</summary>
    public const string UnknownUserLabel = "Unknown user";

    /// <summary>Variants locked before this time hold an old lock.</summary>
    public static DateTime GetOldBefore(DateTime now) => now.AddDays(-OldLockDays);

    /// <summary>
    /// Builds the report.
    /// </summary>
    /// <param name="query">Normalized filter.</param>
    /// <param name="data">Data for the filter, read at <see cref="ContentLocksData.Now"/>.</param>
    /// <param name="lockingEnabled">Whether content locking is enabled.</param>
    /// <param name="getContentItemPath">Returns the admin path of an item where it is edited, or <c>null</c>. Items without link data get no link.</param>
    /// <param name="getUserPath">Returns the admin path of a user by ID, or <c>null</c>.</param>
    public static ContentLocksResult Build(
        StatsSnapshotQuery query,
        ContentLocksData data,
        bool lockingEnabled,
        Func<ContentItemLink, string?>? getContentItemPath = null,
        Func<int, string?>? getUserPath = null)
    {
        string? itemPath(ContentItemLink? link) => link is null || getContentItemPath is null ? null : getContentItemPath(link);

        // Users that no longer exist are one row, also if the data has several.
        var users = data.Users
            .Where(user => user.LockCount > 0)
            .GroupBy(user => user.UserId)
            .Select(group => new ContentLockUserRow(
                group.Key,
                group.Select(user => user.UserName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)),
                group.Sum(user => user.LockCount),
                group.Min(user => user.OldestLockedWhen)))
            .ToList();

        int lockedCount = Math.Max(users.Sum(user => user.LockCount), data.Items.Count);
        var oldest = users
            .Select(user => (DateTime?)user.OldestLockedWhen)
            .Concat(data.Items.Select(item => (DateTime?)item.LockedWhen))
            .Min();

        var byUser = StatsRankedBuilder.BuildSnapshot(
            query.ChannelId,
            users.Select(user => ToEntry(user, data.Now, getUserPath)),
            lockedCount,
            users.Count,
            UserLimit);

        var items = data.Items
            .Take(ListLimit)
            .Select(row => new StatsAgedItem(
                row.Id.ToString(CultureInfo.InvariantCulture),
                row.DisplayName,
                string.IsNullOrWhiteSpace(row.ContentType) ? null : row.ContentType,
                row.Language,
                GetUserLabel(row.UserId, row.UserName),
                DateOnly.FromDateTime(row.LockedWhen),
                StatsAgedItem.GetDays(row.LockedWhen, data.Now))
            {
                AdminPath = itemPath(row.Link),
                Channel = StatsContentChannels.GetLabel(row.Channel, row.IsReusable, row.Workspace),
                LastModified = DateOnly.FromDateTime(row.ModifiedWhen),
            })
            .ToList();

        return new(
            lockingEnabled,
            query.Kind,
            query.ChannelId,
            lockedCount,
            users.Count,
            Math.Max(data.OldLockCount, 0),
            OldLockDays,
            oldest is DateTime since ? DateOnly.FromDateTime(since) : null,
            oldest is DateTime from ? StatsAgedItem.GetDays(from, data.Now) : null,
            byUser,
            items);
    }

    private static StatsRankedEntry ToEntry(ContentLockUserRow user, DateTime now, Func<int, string?>? getUserPath)
    {
        decimal oldestDays = StatsAgedItem.GetDays(user.OldestLockedWhen, now);

        return user.UserId is int userId
            ? new StatsRankedEntry($"user:{userId}", GetUserLabel(userId, user.UserName), null, user.LockCount, oldestDays, null)
            {
                AdminPath = getUserPath?.Invoke(userId),
            }
            : new StatsRankedEntry(UnknownUserKey, UnknownUserLabel, null, user.LockCount, oldestDays, null);
    }

    /// <summary>
    /// The user's display name, "User {ID}" without a name, or <see cref="UnknownUserLabel"/> when the user no longer exists.
    /// </summary>
    private static string GetUserLabel(int? userId, string? userName)
    {
        if (userId is not int id)
        {
            return UnknownUserLabel;
        }

        return string.IsNullOrWhiteSpace(userName) ? $"User {id.ToString(CultureInfo.InvariantCulture)}" : userName;
    }
}
