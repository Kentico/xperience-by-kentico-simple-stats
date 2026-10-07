using System.Globalization;

using CMS.ContentEngine;
using CMS.DataEngine;

using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PublishingCalendar;

/// <summary>
/// Turns scheduled publishes, unpublishes and email sends into the publishing calendar report.
/// </summary>
internal static class PublishingCalendarReportBuilder
{
    /// <summary>
    /// Windows (days ahead) the client can pick.
    /// </summary>
    public static IReadOnlyList<int> Windows { get; } = [7, 30, 90];

    /// <summary>
    /// Window used when the client sends none or an unknown one.
    /// </summary>
    public const int DefaultWindow = 30;

    /// <summary>
    /// Days that count as recent for "recently published".
    /// </summary>
    public const int RecentDays = 7;

    /// <summary>
    /// Rows of each list (upcoming, recently published).
    /// </summary>
    public const int ListLimit = 50;

    /// <summary>
    /// Channel column text of reusable items, which have no channel and are edited in the Content hub:
    /// "Content hub - " plus the workspace display name, or only this text when the item has no workspace.
    /// </summary>
    public const string ContentHubLabel = "Content hub";

    // Series keys of the events per day, also used by the client.
    public const string PublishKey = "publish";
    public const string UnpublishKey = "unpublish";
    public const string SendKey = "send";

    private static readonly IReadOnlyList<StatsSeriesDefinition> daySeries =
    [
        new(PublishKey, "Publish"),
        new(UnpublishKey, "Unpublish"),
        new(SendKey, "Send"),
    ];

    /// <summary>
    /// Kinds of the kind filter: the content kinds without headless items, which cannot be scheduled to publish or unpublish.
    /// </summary>
    public static IReadOnlyList<string> Kinds { get; } =
        [.. StatsContentKinds.Kinds.Where(kind => kind != ClassContentTypeType.HEADLESS)];

    /// <summary>
    /// Channel types of the channel filter: website and email channels (no headless channels, see <see cref="Kinds"/>).
    /// </summary>
    public static IReadOnlyList<ChannelType> ChannelTypes { get; } =
        [.. StatsContentKinds.ChannelTypes.Where(type => type != ChannelType.Headless)];

    /// <summary>
    /// Returns the normalized query: kind and channel as in the content inventory, but a headless kind is all kinds and a
    /// headless channel is dropped (see <see cref="Kinds"/>); the window one of <see cref="Windows"/>.
    /// </summary>
    /// <param name="filter">Filter sent by the client, or <c>null</c>.</param>
    /// <param name="channels">Channels of the channel filter. Channels of other types than <see cref="ChannelTypes"/> are ignored.</param>
    public static PublishingCalendarQuery Normalize(StatsSnapshotFilter? filter, IEnumerable<StatsChannelOption> channels)
    {
        filter ??= new StatsSnapshotFilter();
        var allowed = channels.Where(c => ChannelTypes.Any(type => string.Equals(c.Type, type.ToString(), StringComparison.Ordinal))).ToList();
        var snapshot = filter.Normalize(Kinds, (kind, channelId) => StatsContentKinds.IsChannelAllowed(kind, channelId, allowed));

        return new(snapshot, filter.NormalizeWindow([.. Windows], DefaultWindow));
    }

    /// <summary>
    /// Returns <c>true</c> when the kind filter can match emails (all kinds or emails), so scheduled sends are read.
    /// </summary>
    public static bool IncludesSends(string? kind) => kind is null or ClassContentTypeType.EMAIL;

    /// <summary>Last time of the window (inclusive).</summary>
    public static DateTime GetUpcomingTo(DateTime now, int window) => now.AddDays(window);

    /// <summary>Variants last published on or after this time count as recently published.</summary>
    public static DateTime GetRecentFrom(DateTime now) => now.AddDays(-RecentDays);

    /// <summary>
    /// Builds the report.
    /// </summary>
    /// <param name="query">Normalized filter.</param>
    /// <param name="data">Data for the filter, read at <see cref="PublishingCalendarData.Now"/>.</param>
    /// <param name="getContentItemPath">Returns the admin path of an item where it is edited, or <c>null</c>. Items without link data get no link.</param>
    public static PublishingCalendarResult Build(
        PublishingCalendarQuery query,
        PublishingCalendarData data,
        Func<ContentItemLink, string?>? getContentItemPath = null)
    {
        string? itemPath(ContentItemLink? link) => link is null || getContentItemPath is null ? null : getContentItemPath(link);

        var counts = data.Counts;

        return new(
            query.Window,
            query.Filter.Kind,
            query.Filter.ChannelId,
            Math.Max(counts.UpcomingPublish, 0),
            Math.Max(counts.UpcomingUnpublish, 0),
            Math.Max(counts.UpcomingSend, 0),
            Math.Max(counts.RecentlyPublished, 0),
            RecentDays,
            BuildDays(query, data),
            ToItems(OrderEvents(data.Upcoming), itemPath),
            ToItems(data.Recent.OrderByDescending(row => row.When).ThenBy(row => row.Id).Take(ListLimit), itemPath));
    }

    /// <summary>
    /// Content events and sends together, soonest first (publish, unpublish, then send at the same time), up to <see cref="ListLimit"/>.
    /// </summary>
    private static IEnumerable<PublishingRow> OrderEvents(IEnumerable<PublishingRow> rows) =>
        rows
            .OrderBy(row => row.When)
            .ThenBy(row => row.Action)
            .ThenBy(row => row.Id)
            .Take(ListLimit);

    /// <summary>
    /// One period per day from today to the day of the window end, all series, days without events as 0.
    /// </summary>
    private static StatsTimeSeriesResult BuildDays(PublishingCalendarQuery query, PublishingCalendarData data)
    {
        var range = new StatsQuery(
            DateOnly.FromDateTime(data.Now),
            DateOnly.FromDateTime(GetUpcomingTo(data.Now, query.Window)),
            StatsGrouping.Day,
            query.Filter.ChannelId);

        // Content and sends can have a row for the same day; the series add them up.
        var counts = data.Days
            .GroupBy(day => day.Day)
            .SelectMany(group => new[]
            {
                new StatsDailyCount(PublishKey, group.Key, group.Sum(day => day.Publish)),
                new StatsDailyCount(UnpublishKey, group.Key, group.Sum(day => day.Unpublish)),
                new StatsDailyCount(SendKey, group.Key, group.Sum(day => day.Send)),
            });

        return StatsTimeSeriesBuilder.BuildFixed(range, counts, daySeries);
    }

    private static List<PublishingCalendarItem> ToItems(IEnumerable<PublishingRow> rows, Func<ContentItemLink?, string?> itemPath) =>
        rows
            .Select(row => new PublishingCalendarItem(
                GetKey(row),
                row.When,
                row.Action,
                row.DisplayName,
                row.ContentType,
                row.Language,
                GetChannel(row),
                string.IsNullOrWhiteSpace(row.ModifiedBy) ? null : row.ModifiedBy)
            {
                AdminPath = itemPath(row.Link),
            })
            .ToList();

    /// <summary>
    /// The channel, or for reusable items the Content hub with the workspace (see <see cref="ContentHubLabel"/>).
    /// </summary>
    private static string? GetChannel(PublishingRow row)
    {
        if (!string.IsNullOrWhiteSpace(row.Channel))
        {
            return row.Channel;
        }
        if (!row.IsReusable)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(row.Workspace) ? ContentHubLabel : $"{ContentHubLabel} - {row.Workspace}";
    }

    private static string GetKey(PublishingRow row)
    {
        string id = row.Id.ToString(CultureInfo.InvariantCulture);

        return row.Action switch
        {
            null => id,
            PublishingAction.Publish => $"{id}-{PublishKey}",
            PublishingAction.Unpublish => $"{id}-{UnpublishKey}",
            PublishingAction.Send => $"{SendKey}-{id}",
            _ => throw new ArgumentOutOfRangeException(nameof(row), row.Action, "Unknown publishing action."),
        };
    }
}
