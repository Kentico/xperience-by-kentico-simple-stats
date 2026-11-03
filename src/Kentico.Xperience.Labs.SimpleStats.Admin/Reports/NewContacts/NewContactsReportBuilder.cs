using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.NewContacts;

/// <summary>
/// Turns daily contact counts into a period-bucketed, zero-filled report.
/// </summary>
internal static class NewContactsReportBuilder
{
    public const string IdentifiedKey = "identified";
    public const string AnonymousKey = "anonymous";

    /// <summary>
    /// Fixed series order: identified first, then anonymous. Not sorted by total.
    /// </summary>
    private static readonly StatsSeriesDefinition[] series =
    [
        new(IdentifiedKey, "Identified"),
        new(AnonymousKey, "Anonymous"),
    ];

    /// <summary>
    /// Builds the report.
    /// </summary>
    /// <param name="query">Normalized filter.</param>
    /// <param name="dailyCounts">
    /// Daily counts from the start of the previous period (see <see cref="StatsComparison.GetPreviousRange"/>) to the end of the range.
    /// The trend uses only the range; rows before it are the previous period of the comparisons.
    /// </param>
    public static NewContactsResult Build(StatsQuery query, IEnumerable<NewContactsDailyCount> dailyCounts)
    {
        var dailyRows = dailyCounts.ToList();
        var rows = dailyRows.SelectMany(row => new StatsDailyCount[]
        {
            new(IdentifiedKey, row.Date, row.Identified),
            new(AnonymousKey, row.Date, row.Anonymous),
        });

        var trend = StatsTimeSeriesBuilder.BuildFixed(query, rows, series);

        int identified = trend.Series[0].Total;
        int anonymous = trend.Series[1].Total;
        int total = identified + anonymous;

        var (previousFrom, _) = StatsComparison.GetPreviousRange(query);
        var previousRows = dailyRows.Where(row => row.Date >= previousFrom && row.Date < query.From).ToList();
        int previousIdentified = previousRows.Sum(row => row.Identified);
        int previousAnonymous = previousRows.Sum(row => row.Anonymous);

        return new(
            trend,
            identified,
            anonymous,
            total > 0 ? (double)identified / total : 0,
            StatsComparison.Create(query, total, previousIdentified + previousAnonymous),
            StatsComparison.Create(query, identified, previousIdentified),
            StatsComparison.Create(query, anonymous, previousAnonymous));
    }
}
