using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PublishingActivity;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.EditorContributions;

/// <summary>
/// Input of the editor contributions <c>LOAD</c> page command. The filter is the same as the publishing activity report's
/// (range, grouping, channel and content kind).
/// </summary>
public sealed record EditorContributionsLoadRequest
{
    /// <summary>
    /// Report filter. <c>null</c> means defaults.
    /// </summary>
    public PublishingActivityFilter? Filter { get; init; }

    /// <summary>
    /// When <c>true</c>, cached data for the filter is dropped and read again from the database.
    /// </summary>
    public bool Refresh { get; init; }
}

/// <summary>
/// Editor contributions report: language variants created and last modified per administration user in the range, and (with content
/// version history) publishes per user. Counts are per language variant.
/// </summary>
/// <param name="From">Applied range start (inclusive).</param>
/// <param name="To">Applied range end (inclusive).</param>
/// <param name="Grouping">Applied grouping.</param>
/// <param name="Kind">Applied content type type filter, or <c>null</c> for all.</param>
/// <param name="ChannelId">Applied channel filter, or <c>null</c> for all.</param>
/// <param name="ActiveEditors">
/// Users with any created or last modified variant in the range vs the previous period. Users that no longer exist are not counted.
/// </param>
/// <param name="Created">Variants created in the range vs the previous period.</param>
/// <param name="LastModified">Variants whose latest change is in the range vs the previous period.</param>
/// <param name="Published">
/// Publishes from content version history (first publishes and updates) vs the previous period. <c>null</c> when version history is disabled.
/// </param>
/// <param name="VersionHistoryEnabled">Whether content version history is enabled (Settings → Content).</param>
/// <param name="VersionHistoryLength">Versions kept per language variant (older ones are deleted), 0 when not limited.</param>
/// <param name="UserCount">Users with any contribution in the range, not only <paramref name="ByUser"/>.</param>
/// <param name="ByUser">
/// Users with contributions in the range, most created + last modified first (up to <see cref="EditorContributionsReportBuilder.UserLimit"/>).
/// </param>
/// <param name="Series">
/// Created per period for the users with the most created variants (up to <see cref="EditorContributionsReportBuilder.SeriesLimit"/>),
/// the others summed into "Other users".
/// </param>
public sealed record EditorContributionsResult(
    DateOnly From,
    DateOnly To,
    StatsGrouping Grouping,
    string? Kind,
    int? ChannelId,
    StatsComparison ActiveEditors,
    StatsComparison Created,
    StatsComparison LastModified,
    StatsComparison? Published,
    bool VersionHistoryEnabled,
    int VersionHistoryLength,
    int UserCount,
    IReadOnlyList<EditorContribution> ByUser,
    StatsTimeSeriesResult Series)
{
    /// <summary>
    /// When the data was read from the database. Can be older than the request when served from cache.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// Contributions of one administration user in the range.
/// </summary>
/// <param name="Key">Stable key (<c>user:{ID}</c>, or <see cref="EditorContributionsReportBuilder.UnknownUserKey"/>).</param>
/// <param name="User">User display name, or "Unknown user" for users that no longer exist.</param>
/// <param name="IsSystemUser">Whether the user is a system user (the product's service user used by imports and automation, or the public user).</param>
/// <param name="Created">Variants created by the user in the range.</param>
/// <param name="LastModified">Variants whose latest change in the range was made by the user.</param>
/// <param name="Published">Publishes by the user in the range, or <c>null</c> when version history is disabled.</param>
/// <param name="ContentTypes">Distinct content types of these contributions.</param>
public sealed record EditorContribution(
    string Key,
    string User,
    bool IsSystemUser,
    int Created,
    int LastModified,
    int? Published,
    int ContentTypes)
{
    /// <summary>
    /// The user in the Users application, relative to the admin root. <c>null</c> for unknown users.
    /// </summary>
    public string? AdminPath { get; init; }
}

/// <summary>
/// Created variants of one user on one day, as read from the database.
/// </summary>
/// <param name="UserId">User ID, or <c>null</c> when the user no longer exists or is not set.</param>
/// <param name="UserName">User display name, or <c>null</c>.</param>
/// <param name="IsSystemUser">Whether the user is a system user.</param>
/// <param name="Date">Day.</param>
/// <param name="Count">Created variants.</param>
internal sealed record EditorDailyRow(int? UserId, string? UserName, bool IsSystemUser, DateOnly Date, int Count);

/// <summary>
/// Contributions of one user in the range, as read from the database.
/// </summary>
/// <param name="UserId">User ID, or <c>null</c> when the user no longer exists or is not set.</param>
/// <param name="UserName">User display name, or <c>null</c>.</param>
/// <param name="IsSystemUser">Whether the user is a system user.</param>
/// <param name="Created">Variants created.</param>
/// <param name="LastModified">Variants last modified.</param>
/// <param name="Published">Publishes (0 when not read).</param>
/// <param name="ContentTypes">Distinct content types.</param>
internal sealed record EditorUserRow(int? UserId, string? UserName, bool IsSystemUser, int Created, int LastModified, int Published, int ContentTypes);

/// <summary>
/// Totals (one row) of the range and the previous period.
/// </summary>
internal sealed record EditorTotalsRow(
    int ActiveEditors,
    int PreviousActiveEditors,
    int Created,
    int PreviousCreated,
    int LastModified,
    int PreviousLastModified,
    int Published,
    int PreviousPublished)
{
    public static EditorTotalsRow Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0);
}

/// <summary>
/// Data read by <see cref="IEditorContributionsRepository"/>.
/// </summary>
/// <param name="Daily">Created per user and day in the range.</param>
/// <param name="Users">Users with contributions in the range (top N).</param>
/// <param name="UserCount">Users with contributions in the range (all).</param>
/// <param name="Totals">Totals.</param>
internal sealed record EditorContributionsData(
    IReadOnlyList<EditorDailyRow> Daily,
    IReadOnlyList<EditorUserRow> Users,
    int UserCount,
    EditorTotalsRow Totals)
{
    public static EditorContributionsData Empty { get; } = new([], [], 0, EditorTotalsRow.Empty);
}

/// <summary>
/// Editor contributions data with the time it was read. This is the cached value.
/// </summary>
internal sealed record EditorContributionsSnapshot(EditorContributionsData Data, DateTimeOffset ReadAt);
