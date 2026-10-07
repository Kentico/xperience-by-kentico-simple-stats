using System.Text.Json.Serialization;

using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PublishingCalendar;

/// <summary>
/// Scheduled publishes and unpublishes of language variants and scheduled sends of regular emails upcoming in the window,
/// and variants published recently.
/// Publish and unpublish counts are per language variant; send counts are per email.
/// </summary>
/// <param name="Window">Applied window in days (upcoming events are scheduled from now to now + window).</param>
/// <param name="Kind">Applied content type type filter (<c>Website</c>, <c>Reusable</c>, <c>Email</c>, <c>Headless</c>), or <c>null</c> for all.</param>
/// <param name="ChannelId">Applied channel filter, or <c>null</c> for all.</param>
/// <param name="UpcomingPublish">Scheduled publishes in the window.</param>
/// <param name="UpcomingUnpublish">Scheduled unpublishes in the window.</param>
/// <param name="UpcomingSend">Scheduled sends of regular emails in the window.</param>
/// <param name="RecentlyPublished">Variants published in the last <paramref name="RecentDays"/> days.</param>
/// <param name="RecentDays">Days that count as recent for <paramref name="RecentlyPublished"/>.</param>
/// <param name="Days">
/// Upcoming events per day from today to the window end (0 included), series <see cref="PublishingCalendarReportBuilder.PublishKey"/>,
/// <see cref="PublishingCalendarReportBuilder.UnpublishKey"/> and <see cref="PublishingCalendarReportBuilder.SendKey"/>.
/// </param>
/// <param name="Upcoming">Upcoming events, soonest first (up to <see cref="PublishingCalendarReportBuilder.ListLimit"/>).</param>
/// <param name="Recent">Recently published variants, newest first (up to <see cref="PublishingCalendarReportBuilder.ListLimit"/>). <see cref="PublishingCalendarItem.Action"/> is <c>null</c>.</param>
public sealed record PublishingCalendarResult(
    int Window,
    string? Kind,
    int? ChannelId,
    int UpcomingPublish,
    int UpcomingUnpublish,
    int UpcomingSend,
    int RecentlyPublished,
    int RecentDays,
    StatsTimeSeriesResult Days,
    IReadOnlyList<PublishingCalendarItem> Upcoming,
    IReadOnlyList<PublishingCalendarItem> Recent)
{
    /// <summary>
    /// When the data was read from the database. Can be older than the request when served from cache.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// Scheduled action. Sent to the client as its name (<c>Publish</c>, <c>Unpublish</c>, <c>Send</c>).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PublishingAction>))]
public enum PublishingAction
{
    /// <summary>Scheduled publish of a language variant.</summary>
    Publish,

    /// <summary>Scheduled unpublish of a language variant.</summary>
    Unpublish,

    /// <summary>Scheduled send of a regular email.</summary>
    Send,
}

/// <summary>
/// One row of a publishing calendar list.
/// </summary>
/// <param name="Key">Unique in the list: the variant ID plus the action for publishes and unpublishes, the send configuration ID for sends.</param>
/// <param name="When">Scheduled time (events) or last publish time (recently published), server time.</param>
/// <param name="Action">Scheduled action, or <c>null</c> for recently published variants.</param>
/// <param name="Label">Variant display name (for sends, the email's display name).</param>
/// <param name="ContentType">Content type display name.</param>
/// <param name="Language">Language display name.</param>
/// <param name="Channel">Channel display name; for reusable items "Content hub - " plus the workspace display name (see <see cref="PublishingCalendarReportBuilder.ContentHubLabel"/>); <c>null</c> when unknown.</param>
/// <param name="ModifiedBy">Name of the user who last modified the variant, or <c>null</c>.</param>
public sealed record PublishingCalendarItem(
    string Key,
    DateTime When,
    PublishingAction? Action,
    string Label,
    string ContentType,
    string Language,
    string? Channel,
    string? ModifiedBy)
{
    /// <inheritdoc cref="StatsRankedItem.AdminPath"/>
    public string? AdminPath { get; init; }
}

/// <summary>
/// Counts read by <see cref="IPublishingCalendarRepository"/> (not limited by the list size).
/// </summary>
internal sealed record PublishingCountsRow(int UpcomingPublish, int UpcomingUnpublish, int UpcomingSend, int RecentlyPublished)
{
    public static PublishingCountsRow Empty { get; } = new(0, 0, 0, 0);

    /// <summary>Adds the counts of <paramref name="other"/> (for example the sends to the content counts).</summary>
    public PublishingCountsRow Add(PublishingCountsRow other) =>
        new(
            UpcomingPublish + other.UpcomingPublish,
            UpcomingUnpublish + other.UpcomingUnpublish,
            UpcomingSend + other.UpcomingSend,
            RecentlyPublished + other.RecentlyPublished);
}

/// <summary>
/// Upcoming events on one day. Days can repeat (content and sends are read separately); the builder adds them up.
/// </summary>
internal sealed record PublishingDayRow(DateOnly Day, int Publish, int Unpublish, int Send);

/// <summary>
/// One event or recently published variant of a list, with its time.
/// </summary>
/// <param name="Id">Language metadata ID, or the send configuration ID for <see cref="PublishingAction.Send"/>.</param>
/// <param name="When">Scheduled time, or last publish time (server time).</param>
/// <param name="Action">Scheduled action, or <c>null</c> for recently published variants.</param>
/// <param name="DisplayName">Variant display name.</param>
/// <param name="ContentType">Content type display name.</param>
/// <param name="Language">Language display name.</param>
/// <param name="Channel">Channel display name, or <c>null</c>.</param>
/// <param name="ModifiedBy">User who last modified the variant, or <c>null</c>.</param>
internal sealed record PublishingRow(
    int Id,
    DateTime When,
    PublishingAction? Action,
    string DisplayName,
    string ContentType,
    string Language,
    string? Channel,
    string? ModifiedBy)
{
    /// <inheritdoc cref="ContentItemLink"/>
    public ContentItemLink? Link { get; init; }

    /// <summary>Whether the item is a reusable item (it has no channel, but a workspace in the Content hub).</summary>
    public bool IsReusable { get; init; }

    /// <summary>Display name of the item's workspace, or <c>null</c>.</summary>
    public string? Workspace { get; init; }
}

/// <summary>
/// Data read by <see cref="IPublishingCalendarRepository"/>. Lists can hold more than <see cref="PublishingCalendarReportBuilder.ListLimit"/>
/// rows in any order (content and sends are read separately); the builder orders and limits them.
/// </summary>
internal sealed record PublishingCalendarData(
    PublishingCountsRow Counts,
    IReadOnlyList<PublishingDayRow> Days,
    IReadOnlyList<PublishingRow> Upcoming,
    IReadOnlyList<PublishingRow> Recent)
{
    public static PublishingCalendarData Empty { get; } = new(PublishingCountsRow.Empty, [], [], []);

    /// <summary>
    /// Server time the window and the recent limit are counted from (the read time).
    /// </summary>
    public DateTime Now { get; init; }
}

/// <summary>
/// Normalized filter of the publishing calendar.
/// </summary>
/// <param name="Filter">Kind and channel.</param>
/// <param name="Window">Days ahead (one of <see cref="PublishingCalendarReportBuilder.Windows"/>).</param>
public sealed record PublishingCalendarQuery(StatsSnapshotQuery Filter, int Window);

/// <summary>
/// Publishing calendar data with the time it was read. This is the cached value.
/// </summary>
internal sealed record PublishingCalendarSnapshot(PublishingCalendarData Data, DateTimeOffset ReadAt);
