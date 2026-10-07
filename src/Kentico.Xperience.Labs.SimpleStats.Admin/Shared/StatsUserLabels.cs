using System.Globalization;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

/// <summary>
/// Labels of administration users in report lists (for example who holds a lock or last changed an item).
/// Read the name with <see cref="StatsContentSql.UserDisplayName"/>.
/// </summary>
internal static class StatsUserLabels
{
    /// <summary>Label of a user that no longer exists (or is not set).</summary>
    public const string UnknownUserLabel = "Unknown user";

    /// <summary>
    /// The user's display name, "User {ID}" without a name, or <see cref="UnknownUserLabel"/> without a user ID.
    /// </summary>
    /// <param name="userId">User ID, or <c>null</c> when the user no longer exists or is not set.</param>
    /// <param name="userName">User display name, or <c>null</c>.</param>
    public static string GetLabel(int? userId, string? userName)
    {
        if (userId is not int id)
        {
            return UnknownUserLabel;
        }

        return string.IsNullOrWhiteSpace(userName) ? $"User {id.ToString(CultureInfo.InvariantCulture)}" : userName;
    }
}
