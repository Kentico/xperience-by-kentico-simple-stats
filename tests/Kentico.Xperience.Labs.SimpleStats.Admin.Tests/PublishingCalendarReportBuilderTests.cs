using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PublishingCalendar;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class PublishingCalendarReportBuilderTests
{
    private static readonly DateTime now = new(2026, 10, 6, 14, 30, 0, DateTimeKind.Unspecified);
    private static readonly DateOnly today = DateOnly.FromDateTime(now);

    private static readonly IReadOnlyList<StatsChannelOption> channels =
    [
        new(1, "Site", "Website"),
        new(2, "Newsletters", "Email"),
        new(3, "App", "Headless"),
    ];

    private static PublishingCalendarQuery Query(int window = 30, string? kind = null, int? channelId = null) =>
        new(new StatsSnapshotQuery(kind, channelId), window);

    [Test]
    public void Build_NoSchedules_ReturnsZerosAndEmptyLists_WithZeroFilledDays()
    {
        var result = PublishingCalendarReportBuilder.Build(Query(), PublishingCalendarData.Empty with { Now = now });

        Assert.Multiple(() =>
        {
            Assert.That(result.UpcomingPublish, Is.Zero);
            Assert.That(result.UpcomingUnpublish, Is.Zero);
            Assert.That(result.UpcomingSend, Is.Zero);
            Assert.That(result.RecentlyPublished, Is.Zero);
            Assert.That(result.Upcoming, Is.Empty);
            Assert.That(result.Recent, Is.Empty);
            Assert.That(result.Days.Periods, Has.Count.EqualTo(31));
            Assert.That(result.Days.Series.Select(s => s.Key), Is.EqualTo(new[] { PublishingCalendarReportBuilder.PublishKey, PublishingCalendarReportBuilder.UnpublishKey, PublishingCalendarReportBuilder.SendKey }));
            Assert.That(result.Days.Series.SelectMany(s => s.Values), Is.All.Zero);
            Assert.That(result.RecentDays, Is.EqualTo(PublishingCalendarReportBuilder.RecentDays));
        });
    }

    [TestCase(7, 8)]
    [TestCase(30, 31)]
    [TestCase(90, 91)]
    public void Build_Days_RunFromTodayToTheDayOfTheWindowEnd(int window, int days)
    {
        var result = PublishingCalendarReportBuilder.Build(Query(window), PublishingCalendarData.Empty with { Now = now });

        Assert.Multiple(() =>
        {
            Assert.That(result.Window, Is.EqualTo(window));
            Assert.That(result.Days.Periods, Has.Count.EqualTo(days));
            Assert.That(result.Days.From, Is.EqualTo(today));
            Assert.That(result.Days.To, Is.EqualTo(DateOnly.FromDateTime(PublishingCalendarReportBuilder.GetUpcomingTo(now, window))));
        });
    }

    [Test]
    public void Build_Days_CountsWindowEdges_AndFillsDaysWithoutEvents()
    {
        // Events exactly now (first day) and exactly now + window (last day).
        var data = PublishingCalendarData.Empty with
        {
            Now = now,
            Days =
            [
                new(today, 2, 0, 0),
                new(today.AddDays(7), 0, 1, 0),
                new(today.AddDays(8), 5, 5, 5),
            ],
        };

        var result = PublishingCalendarReportBuilder.Build(Query(7), data);

        var publish = result.Days.Series.Single(s => s.Key == PublishingCalendarReportBuilder.PublishKey);
        var unpublish = result.Days.Series.Single(s => s.Key == PublishingCalendarReportBuilder.UnpublishKey);
        Assert.Multiple(() =>
        {
            Assert.That(publish.Values, Is.EqualTo(new[] { 2, 0, 0, 0, 0, 0, 0, 0 }));
            Assert.That(unpublish.Values, Is.EqualTo(new[] { 0, 0, 0, 0, 0, 0, 0, 1 }));
            Assert.That(result.Days.Total, Is.EqualTo(3));
        });
    }

    [Test]
    public void Build_Days_AddsSendsToTheSameDay()
    {
        // Content and sends are read separately, so a day can come twice.
        var data = PublishingCalendarData.Empty with
        {
            Now = now,
            Days =
            [
                new(today.AddDays(1), 1, 0, 0),
                new(today.AddDays(1), 0, 0, 2),
                new(today.AddDays(3), 0, 0, 1),
            ],
        };

        var result = PublishingCalendarReportBuilder.Build(Query(7), data);

        var publish = result.Days.Series.Single(s => s.Key == PublishingCalendarReportBuilder.PublishKey);
        var send = result.Days.Series.Single(s => s.Key == PublishingCalendarReportBuilder.SendKey);
        Assert.Multiple(() =>
        {
            Assert.That(publish.Values, Is.EqualTo(new[] { 0, 1, 0, 0, 0, 0, 0, 0 }));
            Assert.That(send.Values, Is.EqualTo(new[] { 0, 2, 0, 1, 0, 0, 0, 0 }));
            Assert.That(send.DisplayName, Is.EqualTo("Send"));
            Assert.That(result.Days.Total, Is.EqualTo(4));
        });
    }

    [Test]
    public void Build_SendsAndContentEvents_AreMergedSoonestFirst_AndLimited()
    {
        var emailLink = new ContentItemLink(ContentItemLocation.Email, 1, 21, "en");
        var content = Enumerable.Range(1, PublishingCalendarReportBuilder.ListLimit)
            .Select(i => new PublishingRow(i, now.AddHours(i), PublishingAction.Publish, $"Item {i}", "Article", "English", "Site", null))
            .ToList();
        var data = PublishingCalendarData.Empty with
        {
            Now = now,
            Counts = new(50, 0, 1, 0),
            Upcoming = [.. content, new(16, now.AddMinutes(30), PublishingAction.Send, "Newsletter", "Email", "English", "Emails", "admin") { Link = emailLink }],
        };

        var result = PublishingCalendarReportBuilder.Build(Query(), data, l => $"/email/{l.ObjectId}");

        Assert.Multiple(() =>
        {
            Assert.That(result.UpcomingSend, Is.EqualTo(1));
            Assert.That(result.Upcoming, Has.Count.EqualTo(PublishingCalendarReportBuilder.ListLimit));
            Assert.That(result.Upcoming[0].Key, Is.EqualTo("send-16"));
            Assert.That(result.Upcoming[0].Action, Is.EqualTo(PublishingAction.Send));
            Assert.That(result.Upcoming[0].AdminPath, Is.EqualTo("/email/21"));
            Assert.That(result.Upcoming[^1].Key, Is.EqualTo("49-publish"));
        });
    }

    [TestCase(null, true)]
    [TestCase("Email", true)]
    [TestCase("Website", false)]
    [TestCase("Reusable", false)]
    [TestCase("Headless", false)]
    public void IncludesSends_OnlyForAllAndEmails(string? kind, bool expected) =>
        Assert.That(PublishingCalendarReportBuilder.IncludesSends(kind), Is.EqualTo(expected));

    [Test]
    public void Result_SerializesActionsAsNames()
    {
        var result = PublishingCalendarReportBuilder.Build(Query(), PublishingCalendarData.Empty with
        {
            Now = now,
            Upcoming =
            [
                new(1, now.AddHours(1), PublishingAction.Publish, "A", "T", "English", null, null),
                new(1, now.AddHours(2), PublishingAction.Unpublish, "A", "T", "English", null, null),
                new(2, now.AddHours(3), PublishingAction.Send, "B", "T", "English", null, null),
            ],
            Recent = [new(1, now.AddHours(-1), null, "A", "T", "English", null, null)],
        });

        string json = System.Text.Json.JsonSerializer.Serialize(result);

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"Action\":\"Publish\""));
            Assert.That(json, Does.Contain("\"Action\":\"Unpublish\""));
            Assert.That(json, Does.Contain("\"Action\":\"Send\""));
            Assert.That(json, Does.Contain("\"Action\":null"));
            Assert.That(json, Does.Contain("\"Grouping\":\"Day\""));
        });
    }

    [Test]
    public void WindowAndLimits_AreCountedFromNow() => Assert.Multiple(() =>
                                                            {
                                                                Assert.That(PublishingCalendarReportBuilder.GetUpcomingTo(now, 30), Is.EqualTo(now.AddDays(30)));
                                                                Assert.That(PublishingCalendarReportBuilder.GetRecentFrom(now), Is.EqualTo(now.AddDays(-7)));
                                                            });

    [Test]
    public void Build_PublishAndUnpublishOfOneVariant_AreTwoRowsWithUniqueKeys()
    {
        var link = new ContentItemLink(ContentItemLocation.WebPage, 1, 42, "en");
        var data = PublishingCalendarData.Empty with
        {
            Now = now,
            Counts = new(1, 1, 0, 0),
            Upcoming =
            [
                new(5, now.AddDays(1), PublishingAction.Publish, "Coffee", "Article", "English", "Site", "Jane Doe") { Link = link },
                new(5, now.AddDays(10), PublishingAction.Unpublish, "Coffee", "Article", "English", "Site", "Jane Doe") { Link = link },
            ],
        };

        var result = PublishingCalendarReportBuilder.Build(Query(), data, l => $"/path/{l.ObjectId}");

        Assert.Multiple(() =>
        {
            Assert.That(result.UpcomingPublish, Is.EqualTo(1));
            Assert.That(result.UpcomingUnpublish, Is.EqualTo(1));
            Assert.That(result.Upcoming.Select(i => i.Key), Is.EqualTo(new[] { "5-publish", "5-unpublish" }));
            Assert.That(result.Upcoming.Select(i => i.Action), Is.EqualTo(new PublishingAction?[] { PublishingAction.Publish, PublishingAction.Unpublish }));
            Assert.That(result.Upcoming.Select(i => i.AdminPath), Is.All.EqualTo("/path/42"));
            Assert.That(result.Upcoming[0].ModifiedBy, Is.EqualTo("Jane Doe"));
        });
    }

    [Test]
    public void Build_UpcomingAndRecentRows_KeepOrderAndClearEmptyTexts()
    {
        var data = PublishingCalendarData.Empty with
        {
            Now = now,
            Counts = new(0, 60, 0, 2),
            Upcoming = [new(3, now.AddHours(2), PublishingAction.Unpublish, "Sale", "Banner", "English", " ", null)],
            Recent =
            [
                new(8, now.AddDays(-1), null, "News", "Article", "Spanish", "Site", "admin"),
                new(9, now.AddDays(-3), null, "Logo", "Image", "English", null, null),
            ],
        };

        var result = PublishingCalendarReportBuilder.Build(Query(), data);

        Assert.Multiple(() =>
        {
            // The count is not limited by the list size.
            Assert.That(result.UpcomingUnpublish, Is.EqualTo(60));
            Assert.That(result.Upcoming.Single().Key, Is.EqualTo("3-unpublish"));
            Assert.That(result.Upcoming.Single().Channel, Is.Null);
            Assert.That(result.Upcoming.Single().AdminPath, Is.Null);
            Assert.That(result.RecentlyPublished, Is.EqualTo(2));
            Assert.That(result.Recent.Select(i => i.Key), Is.EqualTo(new[] { "8", "9" }));
            Assert.That(result.Recent.Select(i => i.Action), Is.All.Null);
        });
    }

    [Test]
    public void Build_NegativeCounts_AreZero()
    {
        var result = PublishingCalendarReportBuilder.Build(Query(), PublishingCalendarData.Empty with { Now = now, Counts = new(-1, -1, -1, -1) });

        Assert.That(new[] { result.UpcomingPublish, result.UpcomingUnpublish, result.UpcomingSend, result.RecentlyPublished }, Is.All.Zero);
    }

    [Test]
    public void Normalize_Defaults_AreAllKindsAndChannels_And30Days()
    {
        var query = PublishingCalendarReportBuilder.Normalize(null, channels);

        Assert.That(query, Is.EqualTo(new PublishingCalendarQuery(new StatsSnapshotQuery(null, null), 30)));
    }

    [TestCase("website", 1, 7, "Website", 1, 7)]
    [TestCase("Website", 2, 90, "Website", null, 90)]
    [TestCase("Email", 2, 30, "Email", 2, 30)]
    [TestCase("Reusable", 1, 14, "Reusable", null, 30)]
    [TestCase(null, 1, null, null, null, 30)]
    [TestCase("Unknown", 1, 7, null, null, 7)]
    public void Normalize_KindChannelAndWindow(string? kind, int? channelId, int? window, string? expectedKind, int? expectedChannel, int expectedWindow)
    {
        var query = PublishingCalendarReportBuilder.Normalize(new StatsSnapshotFilter { Kind = kind, ChannelId = channelId, Window = window }, channels);

        Assert.That(query, Is.EqualTo(new PublishingCalendarQuery(new StatsSnapshotQuery(expectedKind, expectedChannel), expectedWindow)));
    }

    [TestCase("Headless", null)]
    [TestCase("Headless", 3)]
    [TestCase("headless", 1)]
    public void Normalize_HeadlessKind_IsAllKinds(string kind, int? channelId)
    {
        var query = PublishingCalendarReportBuilder.Normalize(new StatsSnapshotFilter { Kind = kind, ChannelId = channelId, Window = 7 }, channels);

        Assert.That(query, Is.EqualTo(new PublishingCalendarQuery(new StatsSnapshotQuery(null, null), 7)));
    }

    [TestCase(null)]
    [TestCase("Website")]
    [TestCase("Email")]
    public void Normalize_HeadlessChannel_IsDropped(string? kind)
    {
        var query = PublishingCalendarReportBuilder.Normalize(new StatsSnapshotFilter { Kind = kind, ChannelId = 3 }, channels);

        Assert.That(query.Filter, Is.EqualTo(new StatsSnapshotQuery(kind, null)));
    }

    [Test]
    public void KindsAndChannelTypes_LeaveOutHeadless() => Assert.Multiple(() =>
                                                                {
                                                                    Assert.That(PublishingCalendarReportBuilder.Kinds, Is.EqualTo(new[] { "Website", "Reusable", "Email" }));
                                                                    Assert.That(PublishingCalendarReportBuilder.ChannelTypes.Select(t => t.ToString()), Is.EqualTo(new[] { "Website", "Email" }));
                                                                    // Content inventory keeps headless items.
                                                                    Assert.That(StatsContentKinds.Kinds, Does.Contain("Headless"));
                                                                });

    [Test]
    public void Build_ReusableItems_ShowTheContentHubWithTheWorkspaceAsChannel()
    {
        var data = PublishingCalendarData.Empty with
        {
            Now = now,
            Upcoming =
            [
                new(1, now.AddHours(1), PublishingAction.Publish, "Banner", "Image", "English", null, null) { IsReusable = true, Workspace = "Marketing" },
                new(2, now.AddHours(2), PublishingAction.Publish, "Logo", "Image", "English", null, null) { IsReusable = true },
                new(3, now.AddHours(3), PublishingAction.Publish, "Home", "Page", "English", "Site", null) { Workspace = "Marketing" },
                new(4, now.AddHours(4), PublishingAction.Publish, "Orphan", "Page", "English", " ", null),
                new(5, now.AddHours(5), PublishingAction.Send, "Mail", "Email", "English", null, null) { Workspace = "Shop" },
            ],
            Recent = [new(6, now.AddHours(-2), null, "News", "Article", "English", null, null) { IsReusable = true, Workspace = " " }],
        };

        var result = PublishingCalendarReportBuilder.Build(Query(), data);

        Assert.Multiple(() =>
        {
            // Only reusable items get the Content hub; a page without a channel or a send stays empty.
            Assert.That(result.Upcoming.Select(i => i.Channel), Is.EqualTo(new[] { "Content hub - Marketing", "Content hub", "Site", null, null }));
            Assert.That(result.Recent.Single().Channel, Is.EqualTo(StatsContentChannels.ContentHubLabel));
        });
    }
}
