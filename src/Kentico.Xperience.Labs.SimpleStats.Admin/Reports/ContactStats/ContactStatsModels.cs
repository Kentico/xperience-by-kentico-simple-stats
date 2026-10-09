using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.TagUsage;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContactStats;

/// <summary>
/// Filter of the contact "Stats (Labs)" tab, sent by the admin client: the shared range and grouping (<see cref="StatsFilter"/>, channel ignored),
/// "All time", and activity types. The shared filter is wrapped, not changed, so other reports keep their filter and cache keys.
/// </summary>
public sealed record ContactStatsFilter
{
    /// <summary>
    /// Range and grouping. Used when <see cref="AllTime"/> is <c>false</c> (grouping always). <c>null</c> means defaults.
    /// </summary>
    public StatsFilter? Range { get; init; }

    /// <summary>
    /// Range from the contact's creation or first activity (whichever is earlier) to today. Default <c>true</c>.
    /// </summary>
    public bool AllTime { get; init; } = true;

    /// <summary>Most activity types taken from a request.</summary>
    public const int MaxTypes = 20;

    /// <summary>Longest activity type taken from a request.</summary>
    public const int MaxTypeLength = 100;

    /// <summary>
    /// Activity type code names. <c>null</c> or empty means all types. Types the contact does not have are ignored,
    /// unless none is known: then they are kept (the result is empty), not replaced by all types.
    /// </summary>
    public IReadOnlyList<string>? ActivityTypes { get; init; }

    /// <summary>
    /// Taxonomy of the tag interests. <c>null</c> or a value &lt;= 0 means all taxonomies.
    /// </summary>
    public int? TaxonomyId { get; init; }

    /// <summary>
    /// Applies defaults and limits and returns a query that is safe to run.
    /// </summary>
    /// <param name="today">Current server date.</param>
    /// <param name="info">Contact creation date and its activity types.</param>
    public ContactStatsQuery Normalize(DateOnly today, ContactStatsInfo info)
    {
        var grouping = Range?.Grouping is { } g && Enum.IsDefined(g) ? g : StatsGrouping.Week;
        StatsQuery range;
        if (AllTime)
        {
            var start = info.GetStart() ?? today;
            range = new StatsFilter { From = start, To = today, Grouping = grouping }.Normalize(today);
        }
        else
        {
            range = (Range ?? new StatsFilter()).Normalize(today) with { Grouping = grouping };
        }

        // Bounded so the cache key stays bounded. "|" is the delimiter of the type list parameter, so it is replaced and cannot match another type
        var requested = (ActivityTypes ?? [])
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim().Replace('|', '_'))
            .Select(t => t.Length > MaxTypeLength ? t[..MaxTypeLength] : t)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Take(MaxTypes)
            .ToList();

        var types = requested
            .Select(t => info.Types.FirstOrDefault(k => string.Equals(k.ActivityType, t, StringComparison.OrdinalIgnoreCase))?.ActivityType)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // None of the requested types is known (stale cache or old client): keep them, so the query returns no rows, not all types
        if (types.Count == 0)
        {
            types = requested;
        }

        return new(range with { ChannelId = null }, AllTime, types)
        {
            TaxonomyId = TaxonomyId is > 0 ? TaxonomyId : null,
        };
    }
}

/// <summary>
/// Normalized contact stats filter.
/// </summary>
/// <param name="Range">Range and grouping (no channel).</param>
/// <param name="AllTime">The range is "All time": no previous period comparison.</param>
/// <param name="ActivityTypes">Selected activity types, sorted (case-insensitive), types of the contact (or the requested ones when none is known). Empty means all.</param>
public sealed record ContactStatsQuery(StatsQuery Range, bool AllTime, IReadOnlyList<string> ActivityTypes)
{
    /// <summary>
    /// Taxonomy of the tag interests, or <c>null</c> for all.
    /// </summary>
    public int? TaxonomyId { get; init; }

    /// <summary>
    /// Whether <paramref name="activityType"/> is in the filter (empty filter: every type).
    /// </summary>
    public bool Includes(string activityType) =>
        ActivityTypes.Count == 0 || ActivityTypes.Contains(activityType, StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Input of the contact stats <c>LOAD</c> and <c>LOAD_HEATMAP</c> page commands.
/// </summary>
public sealed record ContactStatsLoadRequest
{
    /// <summary>Filter. <c>null</c> means defaults.</summary>
    public ContactStatsFilter? Filter { get; init; }

    /// <summary>When <c>true</c>, cached data for the filter is dropped and read again from the database.</summary>
    public bool Refresh { get; init; }
}

/// <summary>
/// Contact facts needed to normalize the filter: whether it exists, when it was created, and its activity types (any date).
/// </summary>
public sealed record ContactStatsInfo(bool Exists, DateTime? Created, IReadOnlyList<ContactActivityTypeCount> Types)
{
    public static ContactStatsInfo Missing { get; } = new(false, null, []);

    /// <summary>
    /// First day of "All time": the contact's creation or first activity, whichever is earlier. <c>null</c> when neither is known.
    /// </summary>
    public DateOnly? GetStart()
    {
        var dates = Types.Select(t => (DateTime?)t.FirstActivity).Append(Created).OfType<DateTime>().ToList();

        return dates.Count == 0 ? null : DateOnly.FromDateTime(dates.Min());
    }
}

/// <summary>
/// Activities of one type of the contact (any date).
/// </summary>
public sealed record ContactActivityTypeCount(string ActivityType, int Count, DateTime FirstActivity);

/// <summary>
/// Activity type option of the filter.
/// </summary>
/// <param name="ActivityType">Code name.</param>
/// <param name="DisplayName">Display name from <c>OM_ActivityType</c>.</param>
/// <param name="Count">Activities of the contact with this type (any date).</param>
public sealed record ContactStatsTypeOption(string ActivityType, string DisplayName, int Count);

/// <summary>
/// Data of one contact read by the batch. Aggregates of that contact only.
/// </summary>
internal sealed record ContactStatsData(
    ContactStatsTotalsRow Totals,
    IReadOnlyList<StatsDailyCount> Daily,
    IReadOnlyList<ContactStatsPageRow> Pages,
    int PageCount,
    IReadOnlyList<ContactStatsItemRow> Forms,
    int FormCount,
    IReadOnlyList<ContactStatsItemRow> Emails,
    int EmailCount,
    IReadOnlyList<ContactStatsSourceRow> Sources,
    int SourceCount,
    IReadOnlyList<ContactStatsSourceRow> SourceContents,
    int SourceContentCount,
    IReadOnlyList<ContactStatsInterestRow> Interests,
    int InterestCount,
    int InterestVisits)
{
    public static ContactStatsData Empty { get; } = new(ContactStatsTotalsRow.Empty, [], [], 0, [], 0, [], 0, [], 0, [], 0, [], 0, 0);

    /// <summary>Tags reached by the page visits (of the taxonomy filter), most visits first.</summary>
    public IReadOnlyList<ContactStatsTagRow> Tags { get; init; } = [];

    /// <summary>Distinct tags reached (of the taxonomy filter), not only <see cref="Tags"/>.</summary>
    public int TagCount { get; init; }

    /// <summary>Page visits that reached at least one tag (of the taxonomy filter).</summary>
    public int TagVisits { get; init; }

    /// <summary>Taxonomies with tags reached by the page visits (any taxonomy), plus the selected one.</summary>
    public IReadOnlyList<TagUsageTaxonomyOption> TaxonomyOptions { get; init; } = [];
}

/// <summary>
/// Page visits that reached one tag, through the visited page or an item it links to. A visit counts once per tag.
/// </summary>
internal sealed record ContactStatsTagRow(int TagId, string Title, string Taxonomy, int Visits, int Pages);

/// <summary>
/// Totals of the contact in the range and the previous period, and first / last activity of the selected types (any date).
/// </summary>
internal sealed record ContactStatsTotalsRow(
    int Activities,
    int PreviousActivities,
    int Sessions,
    int PageVisits,
    int FormSubmissions,
    int EmailClicks,
    int ActiveDays,
    int CampaignSessions,
    DateTime? FirstSeen,
    DateTime? LastSeen)
{
    public static ContactStatsTotalsRow Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, null, null);
}

/// <summary>Page visits of one page variant.</summary>
internal sealed record ContactStatsPageRow(
    Guid? PageGuid,
    int? LanguageId,
    int Visits,
    DateTime LastVisited,
    string? Url,
    string? DisplayName,
    string? Language,
    string? Channel);

/// <summary>Activities of one form (submissions) or email (clicks), by <c>ActivityItemID</c>.</summary>
internal sealed record ContactStatsItemRow(int? ItemId, int Count, DateTime Last, string? DisplayName)
{
    /// <summary>Email channel of an email (for the statistics link).</summary>
    public int? EmailChannelId { get; init; }

    /// <summary>Language of an email (for the statistics link).</summary>
    public string? LanguageName { get; init; }
}

/// <summary>Landings (sessions) of one UTM source, or source and content pair.</summary>
internal sealed record ContactStatsSourceRow(string Source, string? Content, int Sessions);

/// <summary>Page visits of one content type.</summary>
internal sealed record ContactStatsInterestRow(int ClassId, string DisplayName, int Visits, int Pages);

/// <summary>Cached data with its read time.</summary>
internal sealed record ContactStatsSnapshot(ContactStatsData Data, DateTimeOffset ReadAt);

/// <summary>Cached heatmap counts with their read time.</summary>
internal sealed record ContactHeatmapSnapshot(IReadOnlyList<ContactHeatmapCell> Cells, DateTimeOffset ReadAt);

/// <summary>
/// KPIs of the contact in the range (for the selected types).
/// </summary>
/// <param name="Activities">Activities in the range.</param>
/// <param name="Sessions">Landing page activities (one per browsing session).</param>
/// <param name="PageVisits">Page visits.</param>
/// <param name="FormSubmissions">Form submissions.</param>
/// <param name="EmailClicks">Email clicks.</param>
/// <param name="ActiveDays">Distinct days with any activity.</param>
/// <param name="RangeDays">Days in the range.</param>
/// <param name="FirstSeen">Oldest activity (any date, selected types), server time.</param>
/// <param name="LastSeen">Newest activity (any date, selected types), server time.</param>
/// <param name="DaysSinceLastSeen">Whole days from <paramref name="LastSeen"/> to today.</param>
public sealed record ContactStatsTotals(
    int Activities,
    int Sessions,
    int PageVisits,
    int FormSubmissions,
    int EmailClicks,
    int ActiveDays,
    int RangeDays,
    DateTime? FirstSeen,
    DateTime? LastSeen,
    int? DaysSinceLastSeen);

/// <summary>
/// One insight line built on the server (no AI).
/// </summary>
/// <param name="Kind">Stable kind, see <see cref="ContactStatsInsights"/>.</param>
/// <param name="Text">Line shown to the user.</param>
public sealed record ContactStatsInsight(string Kind, string Text);

/// <summary>
/// Activities of one contact: trend, pages, forms, emails, campaign sources, content interests and insights.
/// </summary>
/// <param name="ContactId">Contact ID.</param>
/// <param name="From">Applied range start (inclusive).</param>
/// <param name="To">Applied range end (inclusive).</param>
/// <param name="Grouping">Applied grouping.</param>
/// <param name="AllTime">The range is "All time".</param>
/// <param name="ActivityTypes">Applied activity types (sorted), empty for all.</param>
/// <param name="TypeOptions">Activity types of the contact (any date), most first.</param>
/// <param name="Totals">KPIs.</param>
/// <param name="Comparison">Activities vs the previous period of the same length; <c>null</c> for All time.</param>
/// <param name="Series">Activities per period by type.</param>
/// <param name="TopPages">Page visits by page variant: secondary label = channel and language, URL = public URL.</param>
/// <param name="Forms">Submissions by form: secondary label = last submitted, admin path = the form's submissions.</param>
/// <param name="Emails">Clicks by email: secondary label = last clicked, admin path = the email's statistics.</param>
/// <param name="Campaigns">Sessions by UTM source (label) and content (secondary label).</param>
/// <param name="Interests">Page visits by content type of the visited page: secondary value = distinct pages.</param>
/// <param name="InterestTags">
/// Page visits by tag (of <see cref="TaxonomyId"/>) of the visited page and the items it links to (one level): secondary label = taxonomy,
/// secondary value = distinct pages. A visit counts once per tag, so values do not add up.
/// </param>
/// <param name="TaxonomyId">Applied taxonomy of <paramref name="InterestTags"/>, or <c>null</c> for all.</param>
/// <param name="TaxonomyOptions">Taxonomies with tags reached in the range (tag count = distinct tags reached).</param>
/// <param name="HasAnyUtmData">Any activity on the site has a UTM source.</param>
/// <param name="Insights">Lines that apply.</param>
/// <param name="ActivitiesPath">Admin path of the contact's product Activities tab, or <c>null</c>.</param>
/// <param name="PagePath">Admin path of this tab (for admin links in the client), or <c>null</c>.</param>
public sealed record ContactStatsResult(
    int ContactId,
    DateOnly From,
    DateOnly To,
    StatsGrouping Grouping,
    bool AllTime,
    IReadOnlyList<string> ActivityTypes,
    IReadOnlyList<ContactStatsTypeOption> TypeOptions,
    ContactStatsTotals Totals,
    StatsComparison? Comparison,
    StatsTimeSeriesResult Series,
    StatsRankedResult TopPages,
    StatsRankedResult Forms,
    StatsRankedResult Emails,
    StatsRankedResult Campaigns,
    StatsRankedResult Interests,
    StatsRankedResult InterestTags,
    int? TaxonomyId,
    IReadOnlyList<TagUsageTaxonomyOption> TaxonomyOptions,
    bool HasAnyUtmData,
    IReadOnlyList<ContactStatsInsight> Insights,
    string? ActivitiesPath,
    string? PagePath)
{
    /// <summary>When the data was read from the database.</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// Activities of one weekday and hour (server time zone).
/// </summary>
/// <param name="Weekday">0 = Monday ... 6 = Sunday.</param>
/// <param name="Hour">0-23.</param>
/// <param name="Count">Activities.</param>
public sealed record ContactHeatmapCell(int Weekday, int Hour, int Count);

/// <summary>
/// "When active" heatmap of one contact: activities by weekday and hour in the range, for the selected types. All 7 × 24 cells.
/// </summary>
public sealed record ContactHeatmapResult(
    int ContactId,
    DateOnly From,
    DateOnly To,
    IReadOnlyList<string> ActivityTypes,
    IReadOnlyList<ContactHeatmapCell> Cells,
    int Max,
    int Total)
{
    /// <summary>When the data was read from the database.</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}
