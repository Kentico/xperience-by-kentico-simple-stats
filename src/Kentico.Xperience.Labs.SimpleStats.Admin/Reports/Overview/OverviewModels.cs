using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;
using Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.Overview;

/// <summary>
/// One tile group of the Overview page: its key (sent to the client), its page command and the permission of its source report.
/// </summary>
/// <param name="Key">Tile key, for example <c>content</c>.</param>
/// <param name="CommandName">Page command that loads the tile, for example <c>LOAD_CONTENT</c>.</param>
/// <param name="Permission">Permission of the source report (<see cref="StatsPermissions"/>).</param>
internal sealed record OverviewTile(string Key, string CommandName, string Permission);

/// <summary>
/// Tiles of the Overview page, in page order. Command names and permissions must match the page commands of <see cref="OverviewPage"/>.
/// </summary>
internal static class OverviewTiles
{
    public const string Content = "content";
    public const string Locks = "locks";
    public const string Freshness = "freshness";
    public const string Calendar = "calendar";
    public const string Emails = "emails";
    public const string Forms = "forms";
    public const string Contacts = "contacts";
    public const string Members = "members";
    public const string Orders = "orders";
    public const string Events = "events";
    public const string Traffic = "traffic";

    public const string LoadContent = "LOAD_CONTENT";
    public const string LoadLocks = "LOAD_LOCKS";
    public const string LoadFreshness = "LOAD_FRESHNESS";
    public const string LoadCalendar = "LOAD_CALENDAR";
    public const string LoadEmails = "LOAD_EMAILS";
    public const string LoadForms = "LOAD_FORMS";
    public const string LoadContacts = "LOAD_CONTACTS";
    public const string LoadMembers = "LOAD_MEMBERS";
    public const string LoadOrders = "LOAD_ORDERS";
    public const string LoadEvents = "LOAD_EVENTS";
    public const string LoadTraffic = "LOAD_TRAFFIC";

    public static IReadOnlyList<OverviewTile> All { get; } =
    [
        new(Content, LoadContent, StatsPermissions.CONTENT_INVENTORY),
        new(Locks, LoadLocks, StatsPermissions.CONTENT_LOCKS),
        new(Freshness, LoadFreshness, StatsPermissions.PAGE_FRESHNESS),
        new(Calendar, LoadCalendar, StatsPermissions.PUBLISHING_CALENDAR),
        new(Emails, LoadEmails, StatsPermissions.EMAIL_SUMMARY),
        new(Forms, LoadForms, StatsPermissions.FORM_SUBMISSIONS),
        new(Contacts, LoadContacts, StatsPermissions.NEW_CONTACTS),
        new(Members, LoadMembers, StatsPermissions.MEMBERS),
        new(Orders, LoadOrders, StatsPermissions.ORDERS_REVENUE),
        new(Events, LoadEvents, StatsPermissions.EVENT_LOG),
        new(Traffic, LoadTraffic, StatsPermissions.ACTIVITY_COUNTS),
    ];
}

/// <summary>
/// One KPI of an Overview tile. The client shows it as a card, with the change when <see cref="Comparison"/> is set.
/// </summary>
/// <param name="Key">Stable key in the tile, for example <c>submissions</c>.</param>
/// <param name="Caption">Card caption.</param>
/// <param name="Tooltip">What the value counts.</param>
/// <param name="Noun">Plural noun of the value, for example "submissions" (used in "No submissions in previous 7 days").</param>
/// <param name="Kind">What the value measures.</param>
/// <param name="Value">Value in the range (or the KPI's own window). <c>null</c> when it cannot be computed.</param>
public sealed record OverviewKpi(string Key, string Caption, string Tooltip, string Noun, StatsValueKind Kind, decimal? Value)
{
    /// <summary>
    /// <see cref="Value"/> compared with the previous period of the same length. <c>null</c> for KPIs without a comparison
    /// (for example a fixed window that does not follow the Overview range).
    /// </summary>
    public StatsValueComparison? Comparison { get; init; }
}

/// <summary>
/// One row of an Overview list (for example an upcoming schedule or a top form).
/// </summary>
/// <param name="Key">Unique in the list.</param>
/// <param name="Label">Main text, for example the item name.</param>
/// <param name="Detail">Secondary text, for example the action and content type. <c>null</c> when none.</param>
/// <param name="Date">Date and time of the row (server time). <c>null</c> for rows without a date.</param>
public sealed record OverviewRow(string Key, string Label, string? Detail, DateTime? Date)
{
    /// <inheritdoc cref="StatsRankedItem.AdminPath"/>
    public string? AdminPath { get; init; }
}

/// <summary>
/// A short list of an Overview tile.
/// </summary>
/// <param name="Key">Stable key in the tile, for example <c>upcoming</c>.</param>
/// <param name="Total">Rows in the full report (not only <paramref name="Rows"/>).</param>
/// <param name="Rows">Up to <see cref="OverviewReportBuilder.ListLimit"/> rows.</param>
public sealed record OverviewList(string Key, int Total, IReadOnlyList<OverviewRow> Rows);

/// <summary>
/// Something that needs attention: a count above 0 for a condition someone should act on.
/// </summary>
/// <param name="Key">Stable key, for example <c>workflow-overdue</c>.</param>
/// <param name="Text">Short text built from a template and the count.</param>
/// <param name="Count">Number of items the text counts.</param>
/// <param name="Tone"><see cref="StatsTone.Problem"/> or <see cref="StatsTone.Caution"/>.</param>
public sealed record OverviewAttentionItem(string Key, string Text, int Count, StatsTone Tone)
{
    /// <summary>
    /// Full report with the details, relative to the admin root. <c>null</c> when it is not available.
    /// </summary>
    public string? AdminPath { get; init; }
}

/// <summary>
/// Result of one Overview tile command: a small summary of one report's result.
/// </summary>
/// <param name="Tile">Tile key (<see cref="OverviewTiles"/>).</param>
/// <param name="Available">
/// <c>false</c> when the source is not set up (for example no commerce tables or content locking disabled).
/// The client then shows a short note instead of zeros, and the tile has no KPIs, lists or attention items.
/// </param>
/// <param name="Kpis">KPIs of the tile.</param>
/// <param name="Lists">Short lists of the tile.</param>
/// <param name="Attention">Items that need attention, empty when none.</param>
public sealed record OverviewTileResult(
    string Tile,
    bool Available,
    IReadOnlyList<OverviewKpi> Kpis,
    IReadOnlyList<OverviewList> Lists,
    IReadOnlyList<OverviewAttentionItem> Attention)
{
    /// <summary>
    /// Path of the full report page, relative to the admin root. <c>null</c> when it is not available.
    /// </summary>
    public string? ReportPath { get; init; }

    /// <summary>
    /// Optional note built from a template (for example the event log size limit). <c>null</c> when none.
    /// </summary>
    public string? Note { get; init; }

    /// <summary>
    /// When the source report's data was read from the database. Can be older than the request when served from cache.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }
}
