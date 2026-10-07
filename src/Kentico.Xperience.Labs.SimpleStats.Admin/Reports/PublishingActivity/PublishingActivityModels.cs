using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PublishingActivity;

/// <summary>
/// Filter of the publishing activity report, sent by the admin client: the shared range, grouping and channel (<see cref="StatsFilter"/>)
/// plus the content kind (as the content inventory). The shared filter is wrapped, not changed, so other reports keep their filter and cache keys.
/// </summary>
public sealed record PublishingActivityFilter
{
    /// <summary>
    /// Range, grouping and channel. A channel that does not fit the kind is dropped. <c>null</c> means defaults.
    /// </summary>
    public StatsFilter? Range { get; init; }

    /// <summary>
    /// Optional content type type (<c>Website</c>, <c>Reusable</c>, <c>Email</c>, <c>Headless</c>). <c>null</c> or an unknown value means all.
    /// </summary>
    public string? Kind { get; init; }

    /// <summary>
    /// Applies defaults and limits and returns a query that is safe to run.
    /// </summary>
    /// <param name="today">Current date used for the default range.</param>
    /// <param name="channels">Channels of <see cref="StatsContentKinds.ChannelTypes"/>.</param>
    public PublishingActivityQuery Normalize(DateOnly today, IEnumerable<StatsChannelOption> channels)
    {
        var range = (Range ?? new StatsFilter()).Normalize(today);
        var content = StatsContentKinds.Normalize(new StatsSnapshotFilter { Kind = Kind, ChannelId = range.ChannelId }, channels);

        return new(range with { ChannelId = content.ChannelId }, content.Kind);
    }
}

/// <summary>
/// Normalized publishing activity filter.
/// </summary>
/// <param name="Range">Range, grouping and channel (only with a kind that has channels).</param>
/// <param name="Kind">One of <see cref="StatsContentKinds.Kinds"/>, or <c>null</c> for all.</param>
public sealed record PublishingActivityQuery(StatsQuery Range, string? Kind);

/// <summary>
/// Input of the publishing activity <c>LOAD</c> page command.
/// </summary>
public sealed record PublishingActivityLoadRequest
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
/// Publishing activity report: language variants created, first published and updated (republished) per period, and how long
/// variants took from created to first published. Counts are per language variant.
/// </summary>
/// <param name="From">Applied range start (inclusive).</param>
/// <param name="To">Applied range end (inclusive).</param>
/// <param name="Grouping">Applied grouping.</param>
/// <param name="Kind">Applied content type type filter, or <c>null</c> for all.</param>
/// <param name="ChannelId">Applied channel filter, or <c>null</c> for all.</param>
/// <param name="Created">Variants created in the range vs the previous period.</param>
/// <param name="FirstPublished">Variants first published in the range vs the previous period.</param>
/// <param name="Updates">
/// Publishes from content version history that are not first publishes, vs the previous period. <c>null</c> when version history is disabled.
/// </param>
/// <param name="VersionHistoryEnabled">Whether content version history is enabled (Settings → Content).</param>
/// <param name="VersionHistoryLength">Versions kept per language variant (older ones are deleted), 0 when not limited.</param>
/// <param name="MedianDaysToPublish">
/// Median days from created to first published of the variants first published in the range, or <c>null</c> without any.
/// </param>
/// <param name="Percentile90DaysToPublish">90th percentile of the same days, or <c>null</c> without any.</param>
/// <param name="PublishedDateUnknown">
/// Variants that are or were published but have no first publish date (for example migrated content). Not limited to the range,
/// not in the series.
/// </param>
/// <param name="Series">Created, first published and updates (only with version history) per period.</param>
/// <param name="ByContentType">Content types with activity in the range, most activity first.</param>
/// <param name="Slowest">
/// Variants first published in the range that took longest from created, longest first (up to <see cref="PublishingActivityReportBuilder.ListLimit"/>).
/// <see cref="StatsAgedItem.Since"/> is the creation date, <see cref="StatsAgedItem.Until"/> the first publish and
/// <see cref="StatsAgedItem.Days"/> the whole days between them.
/// </param>
public sealed record PublishingActivityResult(
    DateOnly From,
    DateOnly To,
    StatsGrouping Grouping,
    string? Kind,
    int? ChannelId,
    StatsComparison Created,
    StatsComparison FirstPublished,
    StatsComparison? Updates,
    bool VersionHistoryEnabled,
    int VersionHistoryLength,
    decimal? MedianDaysToPublish,
    decimal? Percentile90DaysToPublish,
    int PublishedDateUnknown,
    StatsTimeSeriesResult Series,
    IReadOnlyList<PublishingActivityContentType> ByContentType,
    IReadOnlyList<StatsAgedItem> Slowest)
{
    /// <summary>
    /// When the data was read from the database. Can be older than the request when served from cache.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// Activity of one content type in the range.
/// </summary>
/// <param name="Key">Stable key of the content type.</param>
/// <param name="ContentType">Content type display name.</param>
/// <param name="Created">Variants created in the range.</param>
/// <param name="FirstPublished">Variants first published in the range.</param>
/// <param name="Updates">Updates in the range, or <c>null</c> when version history is disabled.</param>
/// <param name="MedianDaysToPublish">Median days from created to first published, or <c>null</c> without first publishes.</param>
public sealed record PublishingActivityContentType(
    string Key,
    string ContentType,
    int Created,
    int FirstPublished,
    int? Updates,
    decimal? MedianDaysToPublish)
{
    /// <inheritdoc cref="StatsRankedItem.AdminPath"/>
    public string? AdminPath { get; init; }
}

/// <summary>
/// Activity of one content type in the range, as read from the database.
/// </summary>
/// <param name="ClassId">Content type (class) ID.</param>
/// <param name="DisplayName">Content type display name.</param>
/// <param name="Created">Variants created in the range.</param>
/// <param name="FirstPublished">Variants first published in the range.</param>
/// <param name="Updates">Updates in the range.</param>
/// <param name="MedianDays">Median days to publish, or <c>null</c>.</param>
internal sealed record PublishingActivityTypeRow(int ClassId, string DisplayName, int Created, int FirstPublished, int Updates, double? MedianDays);

/// <summary>
/// Totals (one row).
/// </summary>
/// <param name="PublishedDateUnknown">Variants that are or were published without a first publish date.</param>
/// <param name="MedianDays">Median days to publish in the range, or <c>null</c>.</param>
/// <param name="Percentile90Days">90th percentile of days to publish in the range, or <c>null</c>.</param>
internal sealed record PublishingActivityTotalsRow(int PublishedDateUnknown, double? MedianDays, double? Percentile90Days)
{
    public static PublishingActivityTotalsRow Empty { get; } = new(0, null, null);
}

/// <summary>
/// One variant of the slowest to publish list, as read from the database.
/// </summary>
/// <param name="VariantId">Language metadata ID.</param>
/// <param name="DisplayName">Variant display name.</param>
/// <param name="ContentType">Content type display name.</param>
/// <param name="Language">Language display name.</param>
/// <param name="CreatedWhen">When the variant was created (server time).</param>
/// <param name="FirstPublishedWhen">When the variant was first published (server time).</param>
internal sealed record PublishingActivitySlowRow(
    int VariantId,
    string DisplayName,
    string ContentType,
    string Language,
    DateTime CreatedWhen,
    DateTime FirstPublishedWhen)
{
    /// <inheritdoc cref="ContentItemLink"/>
    public ContentItemLink? Link { get; init; }

    /// <summary>Channel display name, or <c>null</c>.</summary>
    public string? Channel { get; init; }

    /// <summary>Whether the item is a reusable item.</summary>
    public bool IsReusable { get; init; }

    /// <summary>Display name of the item's workspace, or <c>null</c>.</summary>
    public string? Workspace { get; init; }
}

/// <summary>
/// Data read by <see cref="IPublishingActivityRepository"/>.
/// </summary>
/// <param name="Daily">
/// Counts per day and series (<see cref="PublishingActivityReportBuilder.CreatedSeries"/>, <see cref="PublishingActivityReportBuilder.FirstPublishedSeries"/>,
/// <see cref="PublishingActivityReportBuilder.UpdatesSeries"/>) from the start of the previous period to the end of the range.
/// </param>
/// <param name="ContentTypes">Content types with activity in the range.</param>
/// <param name="Totals">Totals.</param>
/// <param name="Slowest">Slowest to publish in the range, longest first.</param>
internal sealed record PublishingActivityData(
    IReadOnlyList<StatsDailyCount> Daily,
    IReadOnlyList<PublishingActivityTypeRow> ContentTypes,
    PublishingActivityTotalsRow Totals,
    IReadOnlyList<PublishingActivitySlowRow> Slowest)
{
    public static PublishingActivityData Empty { get; } = new([], [], PublishingActivityTotalsRow.Empty, []);
}

/// <summary>
/// Publishing activity data with the time it was read. This is the cached value.
/// </summary>
internal sealed record PublishingActivitySnapshot(PublishingActivityData Data, DateTimeOffset ReadAt);
