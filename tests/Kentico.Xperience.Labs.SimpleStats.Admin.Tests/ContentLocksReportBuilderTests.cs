using System.Text.Json;

using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContentLocks;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class ContentLocksReportBuilderTests
{
    private static readonly DateTime now = new(2026, 10, 6, 14, 30, 0, DateTimeKind.Unspecified);

    private static readonly StatsSnapshotQuery all = new(null, null);

    private static readonly IReadOnlyList<StatsChannelOption> channels =
    [
        new(1, "Site", "Website"),
        new(2, "Newsletters", "Email"),
        new(3, "App", "Headless"),
    ];

    private static ContentLockRow Row(int id, int? userId, string? userName, DateTime lockedWhen) =>
        new(id, $"Item {id}", "Article", "English", userId, userName, lockedWhen, lockedWhen.AddMinutes(5));

    [Test]
    public void Build_NoLocks_ReturnsZerosAndEmptyLists()
    {
        var result = ContentLocksReportBuilder.Build(all, ContentLocksData.Empty with { Now = now }, lockingEnabled: true);

        Assert.Multiple(() =>
        {
            Assert.That(result.LockingEnabled, Is.True);
            Assert.That(result.LockedCount, Is.Zero);
            Assert.That(result.UserCount, Is.Zero);
            Assert.That(result.OldLockCount, Is.Zero);
            Assert.That(result.OldLockDays, Is.EqualTo(ContentLocksReportBuilder.OldLockDays));
            Assert.That(result.OldestLockedSince, Is.Null);
            Assert.That(result.OldestLockDays, Is.Null);
            Assert.That(result.ByUser.Items, Is.Empty);
            Assert.That(result.Items, Is.Empty);
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Build_LockingDisabledOrEnabled_StillReportsExistingLocks(bool enabled)
    {
        var data = new ContentLocksData(0, [new(53, "Admin", 1, now.AddHours(-2))], [Row(1, 53, "Admin", now.AddHours(-2))]) { Now = now };

        var result = ContentLocksReportBuilder.Build(all, data, enabled);

        Assert.Multiple(() =>
        {
            Assert.That(result.LockingEnabled, Is.EqualTo(enabled));
            Assert.That(result.LockedCount, Is.EqualTo(1));
            Assert.That(result.Items, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void Build_RanksUsersByLocks_WithOldestLockDaysAndUserLinks()
    {
        var data = new ContentLocksData(
            1,
            [
                new(53, "Admin", 1, now.AddDays(-5)),
                new(120, "Editor", 3, now.AddHours(-1)),
            ],
            [])
        { Now = now };

        var result = ContentLocksReportBuilder.Build(all, data, lockingEnabled: true, getUserPath: id => $"/users/{id}");

        Assert.Multiple(() =>
        {
            Assert.That(result.LockedCount, Is.EqualTo(4));
            Assert.That(result.UserCount, Is.EqualTo(2));
            Assert.That(result.ByUser.Items.Select(i => i.Label), Is.EqualTo(new[] { "Editor", "Admin" }));
            Assert.That(result.ByUser.Items.Select(i => i.Value), Is.EqualTo(new[] { 3m, 1m }));
            Assert.That(result.ByUser.Items.Select(i => i.SecondaryValue), Is.EqualTo(new decimal?[] { 0, 5 }));
            Assert.That(result.ByUser.Items.Select(i => i.AdminPath), Is.EqualTo(new[] { "/users/120", "/users/53" }));
            Assert.That(result.OldestLockedSince, Is.EqualTo(DateOnly.FromDateTime(now.AddDays(-5))));
            Assert.That(result.OldestLockDays, Is.EqualTo(5));
        });
    }

    [Test]
    public void Build_DeletedUsers_AreOneUnknownRowWithoutLink()
    {
        var data = new ContentLocksData(
            0,
            [
                new(null, null, 2, now.AddDays(-1)),
                new(null, null, 1, now.AddDays(-2)),
                new(53, null, 1, now),
            ],
            [Row(1, null, null, now.AddDays(-2)), Row(2, 53, null, now)])
        { Now = now };

        var result = ContentLocksReportBuilder.Build(all, data, lockingEnabled: true, getUserPath: id => $"/users/{id}");

        var unknown = result.ByUser.Items.Single(i => i.Key == ContentLocksReportBuilder.UnknownUserKey);
        Assert.Multiple(() =>
        {
            Assert.That(result.UserCount, Is.EqualTo(2));
            Assert.That(unknown.Label, Is.EqualTo(ContentLocksReportBuilder.UnknownUserLabel));
            Assert.That(unknown.Value, Is.EqualTo(3));
            Assert.That(unknown.SecondaryValue, Is.EqualTo(2));
            Assert.That(unknown.AdminPath, Is.Null);
            // A user without a name is shown by ID.
            Assert.That(result.ByUser.Items.Single(i => i.Key == "user:53").Label, Is.EqualTo("User 53"));
            Assert.That(result.Items.Select(i => i.Detail), Is.EqualTo(new[] { ContentLocksReportBuilder.UnknownUserLabel, "User 53" }));
        });
    }

    [Test]
    public void Build_Items_AreAgedRows_WithUserChannelLinkAndLastChange()
    {
        var lockedWhen = now.AddDays(-4).AddHours(-1);
        var data = new ContentLocksData(
            1,
            [new(53, "Admin", 2, lockedWhen)],
            [
                Row(7, 53, "Admin", lockedWhen) with { Channel = "Site", Link = new(ContentItemLocation.WebPage, 1, 42, "en") },
                Row(8, 53, "Admin", now) with { IsReusable = true, Workspace = "Marketing" },
                Row(9, 53, "Admin", now) with { IsReusable = true },
            ])
        { Now = now };

        var result = ContentLocksReportBuilder.Build(all, data, lockingEnabled: true, getContentItemPath: link => $"/item/{link.ObjectId}");

        var first = result.Items[0];
        Assert.Multiple(() =>
        {
            Assert.That(first.Key, Is.EqualTo("7"));
            Assert.That(first.Label, Is.EqualTo("Item 7"));
            Assert.That(first.Category, Is.EqualTo("Article"));
            Assert.That(first.Language, Is.EqualTo("English"));
            Assert.That(first.Detail, Is.EqualTo("Admin"));
            Assert.That(first.Since, Is.EqualTo(DateOnly.FromDateTime(lockedWhen)));
            Assert.That(first.Days, Is.EqualTo(4));
            Assert.That(first.LastModified, Is.EqualTo(DateOnly.FromDateTime(lockedWhen.AddMinutes(5))));
            Assert.That(first.AdminPath, Is.EqualTo("/item/42"));
            Assert.That(first.Channel, Is.EqualTo("Site"));
            Assert.That(result.Items[1].Channel, Is.EqualTo($"{StatsContentChannels.ContentHubLabel} - Marketing"));
            Assert.That(result.Items[2].Channel, Is.EqualTo(StatsContentChannels.ContentHubLabel));
            Assert.That(result.Items[1].AdminPath, Is.Null);
            Assert.That(result.OldLockCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void OldLocks_AreOlderThanTheThreshold() =>
        Assert.That(ContentLocksReportBuilder.GetOldBefore(now), Is.EqualTo(now.AddDays(-ContentLocksReportBuilder.OldLockDays)));

    [Test]
    public void Build_Items_AreLimited()
    {
        var rows = Enumerable.Range(1, ContentLocksReportBuilder.ListLimit + 5).Select(id => Row(id, 53, "Admin", now)).ToList();
        var data = new ContentLocksData(0, [new(53, "Admin", rows.Count, now)], rows) { Now = now };

        var result = ContentLocksReportBuilder.Build(all, data, lockingEnabled: true);

        Assert.Multiple(() =>
        {
            Assert.That(result.Items, Has.Count.EqualTo(ContentLocksReportBuilder.ListLimit));
            Assert.That(result.LockedCount, Is.EqualTo(rows.Count));
        });
    }

    [Test]
    public void Build_KeepsTheAppliedFilter()
    {
        var result = ContentLocksReportBuilder.Build(new("Website", 1), ContentLocksData.Empty with { Now = now }, lockingEnabled: true);

        Assert.Multiple(() =>
        {
            Assert.That(result.Kind, Is.EqualTo("Website"));
            Assert.That(result.ChannelId, Is.EqualTo(1));
            Assert.That(result.ByUser.ChannelId, Is.EqualTo(1));
        });
    }

    [TestCase("headless", 3, "Headless", 3)]
    [TestCase("Website", 1, "Website", 1)]
    [TestCase("Website", 2, "Website", null)]
    [TestCase("Reusable", 1, "Reusable", null)]
    [TestCase(null, 1, null, null)]
    public void Filter_KindAndChannel_AsContentInventory_IncludingHeadless(string? kind, int? channelId, string? expectedKind, int? expectedChannel)
    {
        var query = StatsContentKinds.Normalize(new StatsSnapshotFilter { Kind = kind, ChannelId = channelId }, channels);

        Assert.That(query, Is.EqualTo(new StatsSnapshotQuery(expectedKind, expectedChannel)));
    }

    [Test]
    public void Result_SerializesForTheClient()
    {
        var data = new ContentLocksData(
            0,
            [new(53, "Admin", 1, now)],
            [Row(7, 53, "Admin", now) with { Channel = "Site" }])
        { Now = now };

        string json = JsonSerializer.Serialize(ContentLocksReportBuilder.Build(all, data, lockingEnabled: false));

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"LockingEnabled\":false"));
            Assert.That(json, Does.Contain("\"OldestLockedSince\":\"2026-10-06\""));
            Assert.That(json, Does.Contain("\"Since\":\"2026-10-06\""));
            Assert.That(json, Does.Contain("\"LastModified\":\"2026-10-06\""));
            Assert.That(json, Does.Contain("\"Channel\":\"Site\""));
            Assert.That(json, Does.Contain("\"Detail\":\"Admin\""));
        });
    }

    [Test]
    public void AgedItem_LeavesOutChannelAndLastModified_FromJson_WhenNull()
    {
        string json = JsonSerializer.Serialize(new StatsAgedItem("1", "Item", null, null, null, new(2026, 1, 1), 3));

        Assert.That(json, Does.Not.Contain("Channel").And.Not.Contain("LastModified"));
    }
}
