using System.Globalization;

using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PublishingActivity;

/// <summary>
/// Turns daily counts, content types and the slowest list into the publishing activity report.
/// </summary>
internal static class PublishingActivityReportBuilder
{
    /// <summary>
    /// Rows of the slowest to publish list.
    /// </summary>
    public const int ListLimit = 25;

    /// <summary>
    /// A variant's earliest publish version row created within this many seconds of its first publish date is that first publish
    /// (the product writes both in the same publish, milliseconds apart), so it is not an update.
    /// </summary>
    public const int FirstPublishToleranceSeconds = 60;

    /// <summary>
    /// Decimals of the days to publish.
    /// </summary>
    public const int DaysDecimals = 1;

    public static StatsSeriesDefinition CreatedSeries { get; } = new(PublishingActivitySeriesKeys.Created, "Created");

    public static StatsSeriesDefinition FirstPublishedSeries { get; } = new(PublishingActivitySeriesKeys.FirstPublished, "First published");

    public static StatsSeriesDefinition UpdatesSeries { get; } = new(PublishingActivitySeriesKeys.Updates, "Updates");

    /// <summary>
    /// Builds the report.
    /// </summary>
    /// <param name="query">Normalized filter.</param>
    /// <param name="data">Data for the filter, daily counts from the start of the previous period (see <see cref="StatsComparison.GetPreviousRange"/>).</param>
    /// <param name="versionHistoryEnabled">Whether content version history is enabled. Without it, updates are left out.</param>
    /// <param name="versionHistoryLength">Versions kept per variant (negative values are treated as 0).</param>
    /// <param name="getContentItemPath">Returns the admin path of an item where it is edited, or <c>null</c>.</param>
    /// <param name="getContentTypePath">Returns the admin path of a content type by ID, or <c>null</c>.</param>
    public static PublishingActivityResult Build(
        PublishingActivityQuery query,
        PublishingActivityData data,
        bool versionHistoryEnabled,
        int versionHistoryLength,
        Func<ContentItemLink, string?>? getContentItemPath = null,
        Func<int, string?>? getContentTypePath = null)
    {
        var range = query.Range;
        var (previousFrom, previousTo) = StatsComparison.GetPreviousRange(range);

        IReadOnlyList<StatsSeriesDefinition> definitions = versionHistoryEnabled
            ? [CreatedSeries, FirstPublishedSeries, UpdatesSeries]
            : [CreatedSeries, FirstPublishedSeries];
        var series = StatsTimeSeriesBuilder.BuildFixed(range, data.Daily, definitions);

        StatsComparison Compare(StatsSeriesDefinition definition) =>
            StatsComparison.Create(range, Sum(data.Daily, definition, range.From, range.To), Sum(data.Daily, definition, previousFrom, previousTo));

        var types = data.ContentTypes
            .Where(row => row.Created > 0 || row.FirstPublished > 0 || (versionHistoryEnabled && row.Updates > 0))
            .Select(row => new PublishingActivityContentType(
                string.Create(CultureInfo.InvariantCulture, $"type:{row.ClassId}"),
                string.IsNullOrWhiteSpace(row.DisplayName) ? string.Create(CultureInfo.InvariantCulture, $"Content type #{row.ClassId}") : row.DisplayName,
                Math.Max(row.Created, 0),
                Math.Max(row.FirstPublished, 0),
                versionHistoryEnabled ? Math.Max(row.Updates, 0) : null,
                RoundDays(row.MedianDays))
            {
                AdminPath = getContentTypePath?.Invoke(row.ClassId),
            })
            .OrderByDescending(type => type.Created + type.FirstPublished + (type.Updates ?? 0))
            .ThenBy(type => type.ContentType, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var slowest = data.Slowest
            .Where(row => row.FirstPublishedWhen >= row.CreatedWhen)
            .Take(ListLimit)
            .Select(row => new StatsAgedItem(
                row.VariantId.ToString(CultureInfo.InvariantCulture),
                row.DisplayName,
                string.IsNullOrWhiteSpace(row.ContentType) ? null : row.ContentType,
                row.Language,
                null,
                DateOnly.FromDateTime(row.CreatedWhen),
                StatsAgedItem.GetDays(row.CreatedWhen, row.FirstPublishedWhen))
            {
                AdminPath = row.Link is { } link && getContentItemPath is not null ? getContentItemPath(link) : null,
                Channel = StatsContentChannels.GetLabel(row.Channel, row.IsReusable, row.Workspace),
                Until = DateOnly.FromDateTime(row.FirstPublishedWhen),
            })
            .ToList();

        return new(
            range.From,
            range.To,
            range.Grouping,
            query.Kind,
            range.ChannelId,
            Compare(CreatedSeries),
            Compare(FirstPublishedSeries),
            versionHistoryEnabled ? Compare(UpdatesSeries) : null,
            versionHistoryEnabled,
            Math.Max(versionHistoryLength, 0),
            RoundDays(data.Totals.MedianDays),
            RoundDays(data.Totals.Percentile90Days),
            Math.Max(data.Totals.PublishedDateUnknown, 0),
            series,
            types,
            slowest);
    }

    private static int Sum(IEnumerable<StatsDailyCount> daily, StatsSeriesDefinition definition, DateOnly from, DateOnly to) =>
        daily
            .Where(row => string.Equals(row.SeriesKey, definition.Key, StringComparison.OrdinalIgnoreCase)
                && row.Date >= from
                && row.Date <= to
                && row.Count > 0)
            .Sum(row => row.Count);

    /// <summary>
    /// Days rounded to <see cref="DaysDecimals"/> decimals; negative values (inconsistent data) are treated as 0.
    /// </summary>
    private static decimal? RoundDays(double? days) =>
        days is double value && double.IsFinite(value)
            ? Math.Round((decimal)Math.Max(value, 0), DaysDecimals, MidpointRounding.AwayFromZero)
            : null;
}
