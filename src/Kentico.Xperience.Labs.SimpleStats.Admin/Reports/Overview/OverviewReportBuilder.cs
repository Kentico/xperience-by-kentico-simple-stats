using System.Globalization;

using CMS.EventLog;

using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ActivityCounts;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContentInventory;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContentLocks;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.EmailSummary;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.EventLog;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.FormSubmissions;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.Members;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.NewContacts;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.OrdersRevenue;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PageFreshness;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PublishingCalendar;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.Overview;

/// <summary>
/// Maps the results of the full reports to small Overview tiles. No queries of its own: thresholds come from the reports' results
/// (for example <see cref="ContentWorkflowSummary.OverdueDays"/>), so the Overview never disagrees with the report it links to.
/// Texts are built from templates and counts.
/// </summary>
internal static class OverviewReportBuilder
{
    /// <summary>
    /// Ranges of the Overview in days, ending today. The first is the default.
    /// </summary>
    public static IReadOnlyList<int> RangeDays { get; } = [7, 14, 30];

    public const int DefaultRangeDays = 7;

    /// <summary>
    /// Days ahead of the "Coming up" list (a window of the publishing calendar).
    /// </summary>
    public const int CalendarWindow = 7;

    /// <summary>
    /// Most rows of an Overview list. The full report has the rest.
    /// </summary>
    public const int ListLimit = 5;

    /// <summary>
    /// Most forms in the "top forms" list.
    /// </summary>
    public const int FormLimit = 3;

    /// <summary>
    /// Fewest errors in the range for the error increase attention item, so a change from 1 to 2 errors is not flagged.
    /// </summary>
    public const int ErrorIncreaseMinimum = 5;

    public const string WorkflowOverdueKey = "workflow-overdue";
    public const string ForgottenEditsKey = "forgotten-edits";
    public const string OldLocksKey = "old-locks";
    public const string StalePopularKey = "stale-popular";
    public const string ErrorIncreaseKey = "error-increase";

    /// <summary>
    /// Returns the Overview range: the last <c>n</c> days ending <paramref name="today"/>, where <c>n</c> is the length of the client's
    /// range when it is one of <see cref="RangeDays"/>, else <see cref="DefaultRangeDays"/>. Grouping is always by day; no channel.
    /// </summary>
    public static StatsQuery NormalizeRange(StatsFilter? filter, DateOnly today)
    {
        int days = filter?.From is DateOnly from && filter.To is DateOnly to
            ? Math.Abs(to.DayNumber - from.DayNumber) + 1
            : DefaultRangeDays;

        if (!RangeDays.Contains(days))
        {
            days = DefaultRangeDays;
        }

        return new(today.AddDays(-(days - 1)), today, StatsGrouping.Day, null);
    }

    /// <summary>
    /// Returns <c>true</c> when errors went up enough to need attention: more than in the previous period and at least
    /// <see cref="ErrorIncreaseMinimum"/>.
    /// </summary>
    public static bool IsErrorIncrease(StatsComparison errors) =>
        errors.Current > errors.Previous && errors.Current >= ErrorIncreaseMinimum;

    public static OverviewTileResult BuildContent(ContentInventoryResult report, string? reportPath)
    {
        var workflow = report.Workflow;
        var forgotten = report.ForgottenEdits;
        var attention = new List<OverviewAttentionItem>();

        if (workflow.Overdue > 0)
        {
            attention.Add(new(
                WorkflowOverdueKey,
                $"{Count(workflow.Overdue, "item", "items")} in a workflow step for more than {Days(workflow.OverdueDays)}",
                workflow.Overdue,
                StatsTone.Caution)
            { AdminPath = reportPath });
        }

        if (forgotten.Forgotten > 0)
        {
            attention.Add(new(
                ForgottenEditsKey,
                $"{Count(forgotten.Forgotten, "published item has", "published items have")} unpublished changes older than {Days(forgotten.OverdueDays)}",
                forgotten.Forgotten,
                StatsTone.Caution)
            { AdminPath = reportPath });
        }

        OverviewKpi[] kpis =
        [
            Plain("in-workflow", "In workflow", "Language variants in a workflow step now.", "items", workflow.InWorkflow),
            Plain("pending-drafts", "Unpublished changes", "Published language variants with a newer, unpublished draft.", "items", forgotten.PendingDrafts),
        ];

        return new OverviewTileResult(OverviewTiles.Content, true, kpis, [], attention)
        {
            ReportPath = reportPath,
            UpdatedAt = report.UpdatedAt,
        };
    }

    public static OverviewTileResult BuildLocks(ContentLocksResult report, string? reportPath)
    {
        if (!report.LockingEnabled)
        {
            return Unavailable(OverviewTiles.Locks, reportPath, report.UpdatedAt);
        }

        var attention = new List<OverviewAttentionItem>();
        if (report.OldLockCount > 0)
        {
            attention.Add(new(
                OldLocksKey,
                $"{Count(report.OldLockCount, "item", "items")} locked for more than {Days(report.OldLockDays)}",
                report.OldLockCount,
                StatsTone.Caution)
            { AdminPath = reportPath });
        }

        OverviewKpi[] kpis =
        [
            Plain("locked", "Locked items", "Language variants locked for editing now.", "items", report.LockedCount),
            Plain("old-locks", "Old locks", $"Locks older than {Days(report.OldLockDays)}.", "items", report.OldLockCount),
        ];

        return new OverviewTileResult(OverviewTiles.Locks, true, kpis, [], attention)
        {
            ReportPath = reportPath,
            UpdatedAt = report.UpdatedAt,
        };
    }

    public static OverviewTileResult BuildFreshness(PageFreshnessResult report, string? reportPath)
    {
        int days = RangeLength(report.From, report.To);
        var attention = new List<OverviewAttentionItem>();

        if (report.StalePopularPages > 0)
        {
            attention.Add(new(
                StalePopularKey,
                $"{Count(report.StalePopularPages, "page", "pages")} not changed in {report.StaleMonths} months had visits in the last {Days(days)}",
                report.StalePopularPages,
                StatsTone.Caution)
            { AdminPath = reportPath });
        }

        OverviewKpi[] kpis =
        [
            Plain(
                "stale-popular",
                "Stale pages with visits",
                $"Published page variants not changed in {report.StaleMonths} months with at least one visit in the last {Days(days)}.",
                "pages",
                report.StalePopularPages),
        ];

        return new OverviewTileResult(OverviewTiles.Freshness, true, kpis, [], attention)
        {
            ReportPath = reportPath,
            UpdatedAt = report.UpdatedAt,
        };
    }

    public static OverviewTileResult BuildCalendar(PublishingCalendarResult report, string? reportPath)
    {
        int upcoming = report.UpcomingPublish + report.UpcomingUnpublish + report.UpcomingSend;

        OverviewKpi[] kpis =
        [
            Plain(
                "published",
                "Published",
                $"Language variants published in the last {Days(report.RecentDays)} (a fixed window of the Publishing calendar, not the Overview range).",
                "published items",
                report.RecentlyPublished),
            Plain("upcoming", "Coming up", $"Scheduled publishes, unpublishes and sends in the next {Days(report.Window)}.", "schedules", upcoming),
        ];

        OverviewList[] lists =
        [
            new(
                "upcoming",
                upcoming,
                [.. report.Upcoming.Take(ListLimit).Select(item => new OverviewRow(
                    item.Key,
                    item.Label,
                    JoinDetail(item.Action?.ToString(), item.ContentType, item.Channel),
                    item.When)
                { AdminPath = item.AdminPath })]),
            new(
                "recent",
                report.RecentlyPublished,
                [.. report.Recent.Take(ListLimit).Select(item => new OverviewRow(
                    item.Key,
                    item.Label,
                    JoinDetail(item.ContentType, item.Language, item.Channel),
                    item.When)
                { AdminPath = item.AdminPath })]),
        ];

        return new OverviewTileResult(OverviewTiles.Calendar, true, kpis, lists, [])
        {
            ReportPath = reportPath,
            UpdatedAt = report.UpdatedAt,
        };
    }

    public static OverviewTileResult BuildEmails(EmailSummaryResult report, string? reportPath)
    {
        if (!report.Available)
        {
            return Unavailable(OverviewTiles.Emails, reportPath, report.UpdatedAt);
        }

        var totals = report.Totals;
        OverviewKpi[] kpis =
        [
            Compared("emails", "Emails sent", "Regular emails with a send date in the range.", "emails", totals.Emails),
            Compared("sent", "Sent", "Sent emails (recipients) of the regular emails sent in the range.", "sent emails", totals.Sent),
            Compared("open-rate", "Open rate", "Unique opens divided by delivered, for the regular emails sent in the range. The change is in percentage points.", "delivered emails", totals.OpenRate),
            Compared("click-rate", "Click rate", "Unique clicks divided by delivered, for the regular emails sent in the range. The change is in percentage points.", "delivered emails", totals.ClickRate),
        ];

        OverviewList[] lists =
        [
            new("best", report.ByClickRate.Top.ItemCount, [.. report.ByClickRate.Top.Items.Take(1).Select(ToEmailRow)]),
            new("worst", report.ByClickRate.Bottom.ItemCount, [.. report.ByClickRate.Bottom.Items.Take(1).Select(ToEmailRow)]),
        ];

        return new OverviewTileResult(OverviewTiles.Emails, true, kpis, lists, [])
        {
            ReportPath = reportPath,
            UpdatedAt = report.UpdatedAt,
        };
    }

    public static OverviewTileResult BuildForms(FormSubmissionsResult report, string? reportPath)
    {
        OverviewKpi[] kpis =
        [
            Compared("submissions", "Form submissions", "Submissions of all forms in the range.", "submissions", report.TotalComparison),
        ];

        OverviewList[] lists =
        [
            new(
                "top",
                report.FormsWithSubmissions,
                [.. report.Forms.Items
                    .Where(item => item.Value > 0)
                    .Take(FormLimit)
                    .Select(item => new OverviewRow(item.Key, item.Label, Count((int)item.Value, "submission", "submissions"), null) { AdminPath = item.AdminPath })]),
        ];

        return new OverviewTileResult(OverviewTiles.Forms, true, kpis, lists, [])
        {
            ReportPath = reportPath,
            UpdatedAt = report.UpdatedAt,
        };
    }

    public static OverviewTileResult BuildContacts(NewContactsResult report, string? reportPath)
    {
        OverviewKpi[] kpis =
        [
            Compared("new-contacts", "New contacts", "Contacts created in the range that still exist.", "new contacts", report.TotalComparison),
            Compared("identified", "Identified", "New contacts with an email address.", "identified contacts", report.IdentifiedComparison),
            Compared("anonymous", "Anonymous", "New contacts without an email address.", "anonymous contacts", report.AnonymousComparison),
        ];

        return new OverviewTileResult(OverviewTiles.Contacts, true, kpis, [], [])
        {
            ReportPath = reportPath,
            UpdatedAt = report.Trend.UpdatedAt,
        };
    }

    public static OverviewTileResult BuildMembers(MembersResult report, string? reportPath)
    {
        if (!report.Available)
        {
            return Unavailable(OverviewTiles.Members, reportPath, report.UpdatedAt);
        }

        OverviewKpi[] kpis =
        [
            Compared("new-members", "New members", "Members created in the range that still exist.", "new members", report.Totals.NewMembers),
        ];

        return new OverviewTileResult(OverviewTiles.Members, true, kpis, [], [])
        {
            ReportPath = reportPath,
            UpdatedAt = report.UpdatedAt,
        };
    }

    public static OverviewTileResult BuildOrders(OrdersRevenueResult report, string? reportPath)
    {
        if (!report.CommerceAvailable)
        {
            return Unavailable(OverviewTiles.Orders, reportPath, report.UpdatedAt);
        }

        OverviewKpi[] kpis =
        [
            Compared("revenue", "Revenue", "Sum of order grand totals in the range (all statuses).", "revenue", report.Totals.Revenue),
            Compared("orders", "Orders", "Orders created in the range (all statuses).", "orders", report.Totals.Orders),
        ];

        return new OverviewTileResult(OverviewTiles.Orders, true, kpis, [], [])
        {
            ReportPath = reportPath,
            UpdatedAt = report.UpdatedAt,
        };
    }

    public static OverviewTileResult BuildEvents(EventLogResult report, string? reportPath)
    {
        var errors = GetTypeTotal(report, EventType.ERROR);
        var warnings = GetTypeTotal(report, EventType.WARNING);
        var attention = new List<OverviewAttentionItem>();

        if (errors is not null && IsErrorIncrease(errors))
        {
            int days = RangeLength(errors.PreviousFrom, errors.PreviousTo);
            attention.Add(new(
                ErrorIncreaseKey,
                $"{Count(errors.Current, "error", "errors")} in the last {Days(days)}, up from {N(errors.Previous)} in the previous {Days(days)}",
                errors.Current,
                StatsTone.Problem)
            { AdminPath = reportPath });
        }

        var kpis = new List<OverviewKpi>();
        if (errors is not null)
        {
            kpis.Add(Compared("errors", "Errors", "Error events in the event log in the range.", "errors", errors));
        }

        if (warnings is not null)
        {
            kpis.Add(Compared("warnings", "Warnings", "Warning events in the event log in the range.", "warnings", warnings));
        }

        string? note = report.LogSizeLimit switch
        {
            null => null,
            0 => "The event log size is 0, so events are not logged.",
            int limit => $"The event log keeps the newest {N(limit)} events; older events are deleted.",
        };

        return new OverviewTileResult(OverviewTiles.Events, true, kpis, [], attention)
        {
            ReportPath = reportPath,
            Note = note,
            UpdatedAt = report.UpdatedAt,
        };
    }

    public static OverviewTileResult BuildTraffic(ActivityCountsResult report, string? reportPath)
    {
        OverviewKpi[] kpis =
        [
            Compared("activities", "Tracked activities", "Contact activities in the range (page visits, form submissions, email opens and clicks, ...). Only contacts who consented to tracking.", "activities", report.TotalComparison),
        ];

        return new OverviewTileResult(OverviewTiles.Traffic, true, kpis, [], [])
        {
            ReportPath = reportPath,
            UpdatedAt = report.UpdatedAt,
        };
    }

    private static StatsComparison? GetTypeTotal(EventLogResult report, string eventType) =>
        report.Totals.FirstOrDefault(t => string.Equals(t.EventType, eventType, StringComparison.OrdinalIgnoreCase))?.Comparison;

    private static OverviewRow ToEmailRow(StatsRankedItem item) =>
        new(item.Key, item.Label, $"{Percent(item.Value)} click rate", null) { AdminPath = item.AdminPath };

    private static OverviewTileResult Unavailable(string tile, string? reportPath, DateTimeOffset updatedAt) =>
        new(tile, false, [], [], []) { ReportPath = reportPath, UpdatedAt = updatedAt };

    private static OverviewKpi Plain(string key, string caption, string tooltip, string noun, int value) =>
        new(key, caption, tooltip, noun, StatsValueKind.Count, value);

    private static OverviewKpi Compared(string key, string caption, string tooltip, string noun, StatsComparison comparison) =>
        Compared(key, caption, tooltip, noun, new StatsValueComparison(
            StatsValueKind.Count,
            comparison.Current,
            comparison.Previous,
            comparison.Change,
            comparison.PreviousFrom,
            comparison.PreviousTo));

    private static OverviewKpi Compared(string key, string caption, string tooltip, string noun, StatsValueComparison comparison) =>
        new(key, caption, tooltip, noun, comparison.Kind, comparison.Current) { Comparison = comparison };

    private static string? JoinDetail(params string?[] parts)
    {
        string text = string.Join(" · ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        return text.Length == 0 ? null : text;
    }

    private static int RangeLength(DateOnly from, DateOnly to) => to.DayNumber - from.DayNumber + 1;

    private static string N(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

    private static string Count(int value, string singular, string plural) => $"{N(value)} {(value == 1 ? singular : plural)}";

    private static string Days(int days) => Count(days, "day", "days");

    private static string Percent(decimal ratio) =>
        (ratio * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";
}
