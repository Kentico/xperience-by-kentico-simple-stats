using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ActivityCounts;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContentInventory;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContentLocks;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.EmailSummary;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.EventLog;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.FormSubmissions;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.Members;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.NewContacts;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.OrdersRevenue;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.Overview;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PageFreshness;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PublishingCalendar;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class OverviewReportBuilderTests
{
    private const string ReportPath = "/simple-stats/report";

    private static readonly DateOnly today = new(2026, 10, 30);

    // Last 7 days; previous period Oct 17–23.
    private static readonly StatsQuery range = new(new(2026, 10, 24), today, StatsGrouping.Day, null);
    private static readonly StatsSnapshotQuery allContent = new(null, null);
    private static readonly DateTimeOffset readAt = new(2026, 10, 30, 8, 0, 0, TimeSpan.Zero);

    [TestCase(7, 7)]
    [TestCase(14, 14)]
    [TestCase(30, 30)]
    [TestCase(90, 7)]
    [TestCase(1, 7)]
    public void NormalizeRange_AcceptsOverviewRangesOnly_EndingToday(int days, int expectedDays)
    {
        var filter = new StatsFilter { From = new DateOnly(2026, 9, 1), To = new DateOnly(2026, 9, 1).AddDays(days - 1), Grouping = StatsGrouping.Month, ChannelId = 3 };

        var query = OverviewReportBuilder.NormalizeRange(filter, today);

        Assert.That(query, Is.EqualTo(new StatsQuery(today.AddDays(-(expectedDays - 1)), today, StatsGrouping.Day, null)));
    }

    [Test]
    public void NormalizeRange_NoFilter_DefaultsTo7Days() =>
        Assert.That(OverviewReportBuilder.NormalizeRange(null, today), Is.EqualTo(new StatsQuery(new(2026, 10, 24), today, StatsGrouping.Day, null)));

    [Test]
    public void Tiles_HaveUniqueKeysAndCommands() => Assert.Multiple(() =>
                                                          {
                                                              Assert.That(OverviewTiles.All.Select(t => t.Key), Is.Unique);
                                                              Assert.That(OverviewTiles.All.Select(t => t.CommandName), Is.Unique);
                                                              Assert.That(OverviewTiles.All.Select(t => t.Permission), Is.Unique);
                                                          });

    // Content: thresholds come from the result (ContentInventoryReportBuilder.OverdueDays).

    [Test]
    public void Content_NothingOverdue_NoAttention()
    {
        var tile = OverviewReportBuilder.BuildContent(Content(inWorkflow: 3, overdue: 0, pending: 2, forgotten: 0), ReportPath);

        Assert.Multiple(() =>
        {
            Assert.That(tile.Available, Is.True);
            Assert.That(tile.Attention, Is.Empty);
            Assert.That(tile.Kpis.Select(k => k.Value), Is.EqualTo(new decimal?[] { 3, 2 }));
            Assert.That(tile.ReportPath, Is.EqualTo(ReportPath));
            Assert.That(tile.UpdatedAt, Is.EqualTo(readAt));
        });
    }

    [Test]
    public void Content_OverdueAndForgotten_AttentionItemsWithReportLink()
    {
        var tile = OverviewReportBuilder.BuildContent(Content(inWorkflow: 5, overdue: 3, pending: 4, forgotten: 1), ReportPath);

        Assert.Multiple(() =>
        {
            Assert.That(tile.Attention.Select(a => a.Key), Is.EqualTo(new[] { OverviewReportBuilder.WorkflowOverdueKey, OverviewReportBuilder.ForgottenEditsKey }));
            Assert.That(tile.Attention.Select(a => a.Count), Is.EqualTo(new[] { 3, 1 }));
            Assert.That(tile.Attention.Select(a => a.Tone), Has.All.EqualTo(StatsTone.Caution));
            Assert.That(tile.Attention.Select(a => a.AdminPath), Has.All.EqualTo(ReportPath));
            Assert.That(tile.Attention[0].Text, Is.EqualTo($"3 items in a workflow step for more than {ContentInventoryReportBuilder.OverdueDays} days"));
            Assert.That(tile.Attention[1].Text, Is.EqualTo($"1 published item has unpublished changes older than {ContentInventoryReportBuilder.OverdueDays} days"));
        });
    }

    [Test]
    public void Content_UsesThresholdFromResult()
    {
        var report = Content(inWorkflow: 1, overdue: 1, pending: 0, forgotten: 0);
        report = report with { Workflow = report.Workflow with { OverdueDays = 21 } };

        var tile = OverviewReportBuilder.BuildContent(report, ReportPath);

        Assert.That(tile.Attention.Single().Text, Is.EqualTo("1 item in a workflow step for more than 21 days"));
    }

    // Locks

    [Test]
    public void Locks_LockingDisabled_NotAvailable()
    {
        var tile = OverviewReportBuilder.BuildLocks(Locks(enabled: false, locked: 4, old: 2), ReportPath);

        Assert.Multiple(() =>
        {
            Assert.That(tile.Available, Is.False);
            Assert.That(tile.Kpis, Is.Empty);
            Assert.That(tile.Attention, Is.Empty);
            Assert.That(tile.ReportPath, Is.EqualTo(ReportPath));
        });
    }

    [TestCase(0, false)]
    [TestCase(1, true)]
    public void Locks_OldLocks_AttentionAboveZero(int oldLocks, bool expected)
    {
        var tile = OverviewReportBuilder.BuildLocks(Locks(enabled: true, locked: 5, old: oldLocks), ReportPath);

        Assert.That(tile.Attention.Any(a => a.Key == OverviewReportBuilder.OldLocksKey), Is.EqualTo(expected));
        if (expected)
        {
            Assert.That(tile.Attention.Single().Text, Is.EqualTo($"1 item locked for more than {ContentLocksReportBuilder.OldLockDays} days"));
        }
    }

    // Freshness

    [TestCase(0, false)]
    [TestCase(2, true)]
    public void Freshness_StalePopular_AttentionAboveZero(int stalePopular, bool expected)
    {
        var report = PageFreshnessReportBuilder.Build(range, PageFreshnessData.Empty) with { StalePopularPages = stalePopular };

        var tile = OverviewReportBuilder.BuildFreshness(report, ReportPath);

        Assert.That(tile.Attention.Any(a => a.Key == OverviewReportBuilder.StalePopularKey), Is.EqualTo(expected));
        if (expected)
        {
            Assert.That(tile.Attention.Single().Text, Is.EqualTo($"2 pages not changed in {ContentInventoryReportBuilder.StaleMonths} months had visits in the last 7 days"));
        }
    }

    // Calendar

    [Test]
    public void Calendar_Empty_ZeroKpisAndEmptyLists()
    {
        var tile = OverviewReportBuilder.BuildCalendar(Calendar(), ReportPath);

        Assert.Multiple(() =>
        {
            Assert.That(tile.Kpis.Select(k => k.Value), Is.EqualTo(new decimal?[] { 0, 0 }));
            Assert.That(tile.Kpis.Select(k => k.Comparison), Has.All.Null);
            Assert.That(tile.Lists.Select(l => l.Key), Is.EqualTo(new[] { "upcoming", "recent" }));
            Assert.That(tile.Lists.SelectMany(l => l.Rows), Is.Empty);
            Assert.That(tile.Attention, Is.Empty);
        });
    }

    [Test]
    public void Calendar_KeepsFirstFiveRows_AndFullTotals()
    {
        var when = new DateTime(2026, 10, 31, 9, 0, 0, DateTimeKind.Unspecified);
        var upcoming = Enumerable.Range(1, 7)
            .Select(i => new PublishingCalendarItem($"{i}-publish", when.AddHours(i), PublishingAction.Publish, $"Item {i}", "Article", "English", "Site", null) { AdminPath = $"/item/{i}" })
            .ToList();
        var report = Calendar() with { UpcomingPublish = 7, UpcomingUnpublish = 2, UpcomingSend = 1, RecentlyPublished = 3, Upcoming = upcoming };

        var tile = OverviewReportBuilder.BuildCalendar(report, ReportPath);
        var list = tile.Lists.Single(l => l.Key == "upcoming");

        Assert.Multiple(() =>
        {
            Assert.That(list.Total, Is.EqualTo(10));
            Assert.That(list.Rows, Has.Count.EqualTo(OverviewReportBuilder.ListLimit));
            Assert.That(list.Rows[0].Detail, Is.EqualTo("Publish · Article · Site"));
            Assert.That(list.Rows[0].AdminPath, Is.EqualTo("/item/1"));
            Assert.That(list.Rows[0].Date, Is.EqualTo(when.AddHours(1)));
            Assert.That(tile.Kpis.Single(k => k.Key == "published").Value, Is.EqualTo(3));
        });
    }

    // Emails

    [Test]
    public void Emails_NoTables_NotAvailable()
    {
        var report = EmailSummaryReportBuilder.Build(range, EmailSummaryReportData.Unavailable);

        var tile = OverviewReportBuilder.BuildEmails(report, ReportPath);

        Assert.Multiple(() =>
        {
            Assert.That(tile.Available, Is.False);
            Assert.That(tile.Kpis, Is.Empty);
        });
    }

    [Test]
    public void Emails_KpisMatchTotals_BestAndWorstByClickRate()
    {
        var report = EmailSummaryReportBuilder.Build(range, EmailSummaryReportData.Empty);
        report = report with
        {
            ByClickRate = new(
                Ranked(Item("1", "Best", 0.2m), Item("2", "Second", 0.1m)),
                Ranked(Item("3", "Worst", 0.0123m))),
        };

        var tile = OverviewReportBuilder.BuildEmails(report, ReportPath);

        Assert.Multiple(() =>
        {
            Assert.That(tile.Available, Is.True);
            Assert.That(tile.Kpis.Select(k => k.Key), Is.EqualTo(new[] { "emails", "sent", "open-rate", "click-rate" }));
            Assert.That(tile.Kpis.Single(k => k.Key == "click-rate").Comparison, Is.EqualTo(report.Totals.ClickRate));
            Assert.That(tile.Lists.Single(l => l.Key == "best").Rows.Select(r => (r.Label, r.Detail)), Is.EqualTo(new[] { ("Best", "20% click rate") }));
            Assert.That(tile.Lists.Single(l => l.Key == "worst").Rows.Select(r => (r.Label, r.Detail)), Is.EqualTo(new[] { ("Worst", "1.2% click rate") }));
        });
    }

    // Forms

    [Test]
    public void Forms_TopThreeWithSubmissions_AndComparison()
    {
        var data = new FormSubmissionsData(
            [new(1, "a", "A", "T_A"), new(2, "b", "B", "T_B"), new(3, "c", "C", "T_C"), new(4, "d", "D", "T_D"), new(5, "e", "E", "T_E")],
            [
                new(1, new(2026, 10, 25), 5),
                new(2, new(2026, 10, 25), 3),
                new(3, new(2026, 10, 26), 2),
                new(4, new(2026, 10, 26), 1),
                new(1, new(2026, 10, 20), 4),
            ]);
        var report = FormSubmissionsReportBuilder.Build(range, data, id => $"/forms/{id}");

        var tile = OverviewReportBuilder.BuildForms(report, ReportPath);
        var kpi = tile.Kpis.Single();

        Assert.Multiple(() =>
        {
            Assert.That(kpi.Value, Is.EqualTo(11));
            Assert.That(kpi.Comparison!.Previous, Is.EqualTo(4));
            Assert.That(kpi.Comparison.Change, Is.EqualTo(report.TotalComparison.Change));
            Assert.That(tile.Lists.Single().Rows.Select(r => r.Label), Is.EqualTo(new[] { "A", "B", "C" }));
            Assert.That(tile.Lists.Single().Rows[0].Detail, Is.EqualTo("5 submissions"));
            Assert.That(tile.Lists.Single().Rows[0].AdminPath, Is.EqualTo("/forms/1"));
            Assert.That(tile.Lists.Single().Total, Is.EqualTo(4));
        });
    }

    [Test]
    public void Forms_NoSubmissions_EmptyList()
    {
        var report = FormSubmissionsReportBuilder.Build(range, new([new(1, "a", "A", "T_A")], []), _ => null);

        var tile = OverviewReportBuilder.BuildForms(report, ReportPath);

        Assert.Multiple(() =>
        {
            Assert.That(tile.Lists.Single().Rows, Is.Empty);
            Assert.That(tile.Kpis.Single().Value, Is.Zero);
        });
    }

    // Contacts

    [Test]
    public void Contacts_UsesComparisons()
    {
        var report = NewContactsReportBuilder.Build(range, [new(new(2026, 10, 25), 2, 3), new(new(2026, 10, 18), 1, 1)]);

        var tile = OverviewReportBuilder.BuildContacts(report, ReportPath);

        Assert.Multiple(() =>
        {
            Assert.That(tile.Kpis.Select(k => k.Value), Is.EqualTo(new decimal?[] { 5, 2, 3 }));
            Assert.That(tile.Kpis.Select(k => k.Comparison!.Previous), Is.EqualTo(new decimal?[] { 2, 1, 1 }));
        });
    }

    // Members and orders

    [Test]
    public void Members_NotAvailable_WhenTablesMissing()
    {
        var tile = OverviewReportBuilder.BuildMembers(MembersReportBuilder.Build(range, MembersReportData.Unavailable), ReportPath);

        Assert.That(tile.Available, Is.False);
    }

    [Test]
    public void Members_Available_HasNewMembers()
    {
        var report = MembersReportBuilder.Build(range, MembersReportData.Empty);

        var tile = OverviewReportBuilder.BuildMembers(report, ReportPath);

        Assert.Multiple(() =>
        {
            Assert.That(tile.Available, Is.True);
            Assert.That(tile.Kpis.Single().Comparison, Is.EqualTo(report.Totals.NewMembers));
        });
    }

    [Test]
    public void Orders_NotAvailable_WithoutCommerce()
    {
        var report = OrdersRevenueEmpty() with { CommerceAvailable = false };

        var tile = OverviewReportBuilder.BuildOrders(report, ReportPath);

        Assert.Multiple(() =>
        {
            Assert.That(tile.Available, Is.False);
            Assert.That(tile.Kpis, Is.Empty);
        });
    }

    [Test]
    public void Orders_Available_RevenueAndOrders()
    {
        var report = OrdersRevenueEmpty();

        var tile = OverviewReportBuilder.BuildOrders(report, ReportPath);

        Assert.Multiple(() =>
        {
            Assert.That(tile.Kpis.Select(k => k.Key), Is.EqualTo(new[] { "revenue", "orders" }));
            Assert.That(tile.Kpis[0].Comparison, Is.EqualTo(report.Totals.Revenue));
            Assert.That(tile.Kpis[0].Kind, Is.EqualTo(StatsValueKind.Amount));
        });
    }

    // Events: error increase rule

    [TestCase(4, 0, false)]
    [TestCase(5, 0, true)]
    [TestCase(5, 5, false)]
    [TestCase(6, 5, true)]
    [TestCase(5, 6, false)]
    [TestCase(0, 0, false)]
    public void Events_ErrorIncrease_NeedsMoreThanPreviousAndMinimum(int current, int previous, bool expected)
    {
        var tile = OverviewReportBuilder.BuildEvents(Events(current, previous, logSize: 10000), ReportPath);

        Assert.That(tile.Attention.Any(a => a.Key == OverviewReportBuilder.ErrorIncreaseKey), Is.EqualTo(expected));
        if (expected)
        {
            var item = tile.Attention.Single();
            Assert.Multiple(() =>
            {
                Assert.That(item.Tone, Is.EqualTo(StatsTone.Problem));
                Assert.That(item.Count, Is.EqualTo(current));
                Assert.That(item.AdminPath, Is.EqualTo(ReportPath));
                Assert.That(item.Text, Is.EqualTo($"{current} errors in the last 7 days, up from {previous} in the previous 7 days"));
            });
        }
    }

    [Test]
    public void Events_KpisAndLogSizeNote()
    {
        var tile = OverviewReportBuilder.BuildEvents(Events(2, 1, logSize: 10000), ReportPath);

        Assert.Multiple(() =>
        {
            Assert.That(tile.Kpis.Select(k => k.Key), Is.EqualTo(new[] { "errors", "warnings" }));
            Assert.That(tile.Kpis[0].Value, Is.EqualTo(2));
            Assert.That(tile.Kpis[0].Comparison!.Change, Is.EqualTo(1.0));
            Assert.That(tile.Note, Is.EqualTo("The event log keeps the newest 10,000 events; older events are deleted."));
        });
    }

    [Test]
    public void Events_NoLogSize_NoNote() =>
        Assert.That(OverviewReportBuilder.BuildEvents(Events(0, 0, logSize: null), ReportPath).Note, Is.Null);

    [Test]
    public void ErrorIncreaseMinimum_Is5() => Assert.That(OverviewReportBuilder.ErrorIncreaseMinimum, Is.EqualTo(5));

    // Traffic

    [Test]
    public void Traffic_TotalWithComparison()
    {
        var report = ActivityCountsReportBuilder.Build(
            range,
            [new("pagevisit", new(2026, 10, 25), 9), new("pagevisit", new(2026, 10, 20), 3)],
            new Dictionary<string, string>()) with
        { UpdatedAt = readAt };

        var tile = OverviewReportBuilder.BuildTraffic(report, ReportPath);
        var kpi = tile.Kpis.Single();

        Assert.Multiple(() =>
        {
            Assert.That(kpi.Value, Is.EqualTo(9));
            Assert.That(kpi.Comparison!.Previous, Is.EqualTo(3));
            Assert.That(kpi.Comparison.Change, Is.EqualTo(2.0));
            Assert.That(tile.UpdatedAt, Is.EqualTo(readAt));
        });
    }

    private static ContentInventoryResult Content(int inWorkflow, int overdue, int pending, int forgotten)
    {
        var report = ContentInventoryReportBuilder.Build(allContent, ContentInventoryData.Empty, _ => null, _ => null);

        return report with
        {
            Workflow = new(inWorkflow, overdue, ContentInventoryReportBuilder.OverdueDays, []),
            ForgottenEdits = new(pending, forgotten, ContentInventoryReportBuilder.OverdueDays, []),
            UpdatedAt = readAt,
        };
    }

    private static ContentLocksResult Locks(bool enabled, int locked, int old) =>
        ContentLocksReportBuilder.Build(allContent, ContentLocksData.Empty, enabled) with { LockedCount = locked, OldLockCount = old };

    private static PublishingCalendarResult Calendar() =>
        PublishingCalendarReportBuilder.Build(new(allContent, 7), PublishingCalendarData.Empty with { Now = new DateTime(2026, 10, 30, 8, 0, 0, DateTimeKind.Unspecified) });

    private static OrdersRevenueResult OrdersRevenueEmpty() =>
        OrdersRevenueReportBuilder.Build(new(range, null), OrdersRevenueReportData.Empty, []);

    private static EventLogResult Events(int currentErrors, int previousErrors, int? logSize)
    {
        var daily = new List<EventLogDailyCount>
        {
            new("W", new(2026, 10, 26), 1),
        };
        if (currentErrors > 0)
        {
            daily.Add(new("E", new(2026, 10, 25), currentErrors));
        }

        if (previousErrors > 0)
        {
            daily.Add(new("E", new(2026, 10, 18), previousErrors));
        }

        return EventLogReportBuilder.Build(new(range, null), EventLogReportData.Empty with { Daily = daily }, logSize, _ => null);
    }

    private static StatsRankedItem Item(string key, string label, decimal value) =>
        new(0, key, label, null, value, null, 0, null) { AdminPath = $"/email/{key}" };

    private static StatsRankedResult Ranked(params StatsRankedItem[] items) =>
        new(range.From, range.To, null, items, items.Sum(i => i.Value), items.Length);
}
