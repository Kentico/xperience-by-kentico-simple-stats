using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PublishingActivity;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.EditorContributions;

/// <summary>
/// Turns per-user contributions into the editor contributions report.
/// </summary>
internal static class EditorContributionsReportBuilder
{
    /// <summary>
    /// Rows of the per-user list.
    /// </summary>
    public const int UserLimit = 25;

    /// <summary>
    /// Users with their own series in the created over time chart; the others are summed into <see cref="OtherSeries"/>.
    /// </summary>
    public const int SeriesLimit = 5;

    /// <inheritdoc cref="StatsUserLabels.UnknownUserKey"/>
    public const string UnknownUserKey = StatsUserLabels.UnknownUserKey;

    /// <summary>
    /// Series of the users after the first <see cref="SeriesLimit"/>.
    /// </summary>
    public static StatsSeriesDefinition OtherSeries { get; } = new(StatsTimeSeriesBuilder.OtherSeries.Key, "Other users");

    /// <summary>
    /// Builds the report.
    /// </summary>
    /// <param name="query">Normalized filter.</param>
    /// <param name="data">Data for the filter.</param>
    /// <param name="versionHistoryEnabled">Whether content version history is enabled. Without it, publishes are left out.</param>
    /// <param name="versionHistoryLength">Versions kept per variant (negative values are treated as 0).</param>
    /// <param name="getUserPath">Returns the admin path of a user by ID, or <c>null</c>.</param>
    public static EditorContributionsResult Build(
        PublishingActivityQuery query,
        EditorContributionsData data,
        bool versionHistoryEnabled,
        int versionHistoryLength,
        Func<int, string?>? getUserPath = null)
    {
        var range = query.Range;
        var totals = data.Totals;

        // Users that no longer exist are one row, also if the data has several.
        var users = data.Users
            .GroupBy(row => row.UserId)
            .Select(group => new EditorUserRow(
                group.Key,
                group.Select(row => row.UserName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)),
                group.Any(row => row.IsSystemUser),
                group.Sum(row => Math.Max(row.Created, 0)),
                group.Sum(row => Math.Max(row.LastModified, 0)),
                versionHistoryEnabled ? group.Sum(row => Math.Max(row.Published, 0)) : 0,
                group.Max(row => Math.Max(row.ContentTypes, 0))))
            .Where(row => row.Created > 0 || row.LastModified > 0 || row.Published > 0)
            .OrderByDescending(row => row.Created + row.LastModified)
            .ThenByDescending(row => row.Published)
            .ThenBy(row => StatsUserLabels.GetLabel(row.UserId, row.UserName), StringComparer.OrdinalIgnoreCase)
            .ToList();

        var byUser = users
            .Take(UserLimit)
            .Select(row => new EditorContribution(
                StatsUserLabels.GetKey(row.UserId),
                StatsUserLabels.GetLabel(row.UserId, row.UserName),
                row.UserId is not null && row.IsSystemUser,
                row.Created,
                row.LastModified,
                versionHistoryEnabled ? row.Published : null,
                row.ContentTypes)
            {
                AdminPath = row.UserId is int userId ? getUserPath?.Invoke(userId) : null,
            })
            .ToList();

        var names = data.Daily
            .Select(row => (Key: StatsUserLabels.GetKey(row.UserId), Label: StatsUserLabels.GetLabel(row.UserId, row.UserName)))
            .Concat(byUser.Select(user => (user.Key, Label: user.User)))
            .GroupBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Label, StringComparer.OrdinalIgnoreCase);

        var series = StatsTimeSeriesBuilder.BuildDynamic(
            range,
            data.Daily.Select(row => new StatsDailyCount(StatsUserLabels.GetKey(row.UserId), row.Date, row.Count)),
            key => names.TryGetValue(key, out string? label) ? label : key,
            SeriesLimit,
            OtherSeries);

        return new(
            range.From,
            range.To,
            range.Grouping,
            query.Kind,
            range.ChannelId,
            StatsComparison.Create(range, Math.Max(totals.ActiveEditors, 0), Math.Max(totals.PreviousActiveEditors, 0)),
            StatsComparison.Create(range, Math.Max(totals.Created, 0), Math.Max(totals.PreviousCreated, 0)),
            StatsComparison.Create(range, Math.Max(totals.LastModified, 0), Math.Max(totals.PreviousLastModified, 0)),
            versionHistoryEnabled ? StatsComparison.Create(range, Math.Max(totals.Published, 0), Math.Max(totals.PreviousPublished, 0)) : null,
            versionHistoryEnabled,
            Math.Max(versionHistoryLength, 0),
            Math.Max(data.UserCount, users.Count),
            byUser,
            series);
    }
}
