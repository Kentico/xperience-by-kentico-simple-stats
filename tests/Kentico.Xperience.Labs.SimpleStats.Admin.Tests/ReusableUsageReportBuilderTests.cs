using System.Text.Json;

using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ReusableUsage;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class ReusableUsageReportBuilderTests
{
    private static readonly DateTime modified = new(2026, 10, 1, 9, 30, 0, DateTimeKind.Unspecified);

    [Test]
    public void Build_NoReferences_AllItemsAreUnused()
    {
        var data = new ReusableUsageData(
            new ReusableUsageTotals(4, 0, 4, 0, 0, 0, 0, 0),
            [],
            [new(10, "Acme.Image", "Image", 4, 0, 0)]);

        var result = ReusableUsageReportBuilder.Build(null, data, []);

        Assert.Multiple(() =>
        {
            Assert.That(result.ReusableItems, Is.EqualTo(4));
            Assert.That(result.UsedItems, Is.Zero);
            Assert.That(result.UsedShare, Is.Zero);
            Assert.That(result.Unused, Is.EqualTo(4));
            Assert.That(result.UsedItems + result.Unused, Is.EqualTo(result.ReusableItems));
            Assert.That(result.MostUsed, Is.Empty);
            Assert.That(result.Distribution.Items.Select(i => i.Value), Is.EqualTo(new decimal[] { 4, 0, 0, 0, 0 }));
            Assert.That(result.ByContentType.Items.Single().Value, Is.Zero);
        });
    }

    [Test]
    public void Build_EmptyData_HasNoItemsAndZeroShare()
    {
        var result = ReusableUsageReportBuilder.Build(null, ReusableUsageData.Empty, []);

        Assert.Multiple(() =>
        {
            Assert.That(result.ReusableItems, Is.Zero);
            Assert.That(result.UsedShare, Is.Zero);
            Assert.That(result.Distribution.Items, Has.Count.EqualTo(5));
            Assert.That(result.ByContentType.Items, Is.Empty);
        });
    }

    [Test]
    public void Build_Distribution_HasFixedOrderAndLabels_SharesOfAllItems()
    {
        var data = new ReusableUsageData(new ReusableUsageTotals(10, 8, 2, 3, 2, 2, 1, 120), [], []);

        var result = ReusableUsageReportBuilder.Build(null, data, []);

        Assert.Multiple(() =>
        {
            Assert.That(result.Distribution.Items.Select(i => i.Key), Is.EqualTo(new[]
            {
                ReusableUsageReportBuilder.UnusedKey,
                ReusableUsageReportBuilder.OnceKey,
                ReusableUsageReportBuilder.FewKey,
                ReusableUsageReportBuilder.SomeKey,
                ReusableUsageReportBuilder.ManyKey,
            }));
            Assert.That(result.Distribution.Items.Select(i => i.Label), Is.EqualTo(new[] { "Unused", "1 usage", "2–5 usages", "6–20 usages", "Over 20 usages" }));
            Assert.That(result.Distribution.Items.Select(i => i.Value), Is.EqualTo(new decimal[] { 2, 3, 2, 2, 1 }));
            Assert.That(result.Distribution.Total, Is.EqualTo(10));
            Assert.That(result.Distribution.Items[0].Share, Is.EqualTo(0.2).Within(1e-9));
            Assert.That(result.UsedShare, Is.EqualTo(0.8).Within(1e-9));
            Assert.That(result.UsedOnce, Is.EqualTo(3));
            Assert.That(result.Usages, Is.EqualTo(120));
        });
    }

    [Test]
    public void Build_MostUsed_HasUsagesPerKind_ContentHubLabelAndLinks()
    {
        var data = new ReusableUsageData(
            new ReusableUsageTotals(2, 2, 0, 1, 1, 0, 0, 5),
            [
                new(77, "Logo", "Image", 4, 1, 1, 1, 1, modified) { Workspace = "Marketing", Link = new(ContentItemLocation.ContentHub, 3, 77, "en") },
                new(78, "Icon", "", 1, 0, 0, 1, 0, null),
            ],
            []);

        var result = ReusableUsageReportBuilder.Build(null, data, [], link => $"/hub/{link.ContainerId}/{link.LanguageName}/{link.ObjectId}");

        var first = result.MostUsed[0];
        var second = result.MostUsed[1];
        Assert.Multiple(() =>
        {
            Assert.That(first.Key, Is.EqualTo("77"));
            Assert.That((first.Pages, first.Emails, first.ReusableItems, first.HeadlessItems, first.Usages), Is.EqualTo((1, 1, 1, 1, 4)));
            Assert.That(first.Channel, Is.EqualTo("Content hub - Marketing"));
            Assert.That(first.LastModified, Is.EqualTo(new DateOnly(2026, 10, 1)));
            Assert.That(first.AdminPath, Is.EqualTo("/hub/3/en/77"));
            Assert.That(second.ContentType, Is.Null);
            Assert.That(second.Channel, Is.EqualTo(StatsContentChannels.ContentHubLabel));
            Assert.That(second.LastModified, Is.Null);
            Assert.That(second.AdminPath, Is.Null);
        });
    }

    [Test]
    public void Build_MostUsed_LeavesOutUnusedRowsAndKeepsTheLimit()
    {
        var rows = Enumerable.Range(1, ReusableUsageReportBuilder.ListLimit + 5)
            .Select(i => new ReusableUsageRow(i, $"Item {i}", "Image", 100 - i, 0, 0, 100 - i, 0, modified))
            .Append(new ReusableUsageRow(999, "Unused", "Image", 0, 0, 0, 0, 0, modified))
            .ToList();
        var data = new ReusableUsageData(new ReusableUsageTotals(31, 30, 1, 0, 0, 0, 30, 2000), rows, []);

        var result = ReusableUsageReportBuilder.Build(null, data, []);

        Assert.Multiple(() =>
        {
            Assert.That(result.MostUsed, Has.Count.EqualTo(ReusableUsageReportBuilder.ListLimit));
            Assert.That(result.MostUsed.Select(i => i.Key), Does.Not.Contain("999"));
        });
    }

    [Test]
    public void Build_ByContentType_RanksByUsages_WithItemsUsedItemsAndLinks()
    {
        var data = new ReusableUsageData(
            new ReusableUsageTotals(9, 6, 3, 4, 2, 0, 0, 12),
            [],
            [
                new(10, "Acme.Image", "Image", 5, 4, 5),
                new(11, "Acme.Contact", "", 1, 0, 0),
                new(12, "Acme.SocialLink", "Social link", 3, 2, 7),
            ]);

        var result = ReusableUsageReportBuilder.Build(null, data, [], getContentTypePath: classId => $"/types/{classId}");

        Assert.Multiple(() =>
        {
            Assert.That(result.ByContentType.Items.Select(i => i.Label), Is.EqualTo(new[] { "Social link", "Image", "Acme.Contact" }));
            Assert.That(result.ByContentType.Items.Select(i => i.Value), Is.EqualTo(new decimal[] { 7, 5, 0 }));
            Assert.That(result.ByContentType.Items.Select(i => i.SecondaryValue), Is.EqualTo(new decimal?[] { 3, 5, 1 }));
            Assert.That(result.ByContentType.Items.Select(i => i.TertiaryValue), Is.EqualTo(new decimal?[] { 2, 4, 0 }));
            Assert.That(result.ByContentType.Items.Select(i => i.AdminPath), Is.EqualTo(new[] { "/types/12", "/types/10", "/types/11" }));
            Assert.That(result.ByContentType.Total, Is.EqualTo(12));
        });
    }

    [Test]
    public void GetContentTypeOptions_HasTypesWithItems_ByDisplayName()
    {
        var options = ReusableUsageReportBuilder.GetContentTypeOptions(
        [
            new(12, "Acme.SocialLink", "Social link", 3, 2, 7),
            new(10, "Acme.Image", "Image", 5, 4, 5),
            new(13, "Acme.Empty", "Empty", 0, 0, 0),
            new(10, "Acme.Image", "Image", 5, 4, 5),
        ]);

        Assert.That(options, Is.EqualTo(new ReusableUsageContentTypeOption[] { new(10, "Image", 5), new(12, "Social link", 3) }));
    }

    [TestCase(10, 10)]
    [TestCase(99, null)]
    [TestCase(0, null)]
    [TestCase(-1, null)]
    [TestCase(null, null)]
    public void NormalizeContentType_KeepsKnownTypesOnly(int? contentTypeId, int? expected)
    {
        ReusableUsageContentTypeOption[] options = [new(10, "Image", 5)];

        Assert.That(ReusableUsageReportBuilder.NormalizeContentType(contentTypeId, options), Is.EqualTo(expected));
    }

    [Test]
    public void Build_KeepsUsedPlusUnusedWithinTheItems()
    {
        var data = new ReusableUsageData(new ReusableUsageTotals(3, 5, 4, 7, 0, 0, 0, 9), [], []);

        var result = ReusableUsageReportBuilder.Build(null, data, []);

        Assert.Multiple(() =>
        {
            Assert.That(result.UsedItems, Is.EqualTo(3));
            Assert.That(result.Unused, Is.Zero);
            Assert.That(result.UsedOnce, Is.EqualTo(3));
        });
    }

    [Test]
    public void Result_SerializesForTheClient()
    {
        var data = new ReusableUsageData(
            new ReusableUsageTotals(1, 1, 0, 1, 0, 0, 0, 1),
            [new(77, "Logo", "Image", 1, 1, 0, 0, 0, modified) { Workspace = "Marketing" }],
            [new(10, "Acme.Image", "Image", 1, 1, 1)]);

        string json = JsonSerializer.Serialize(ReusableUsageReportBuilder.Build(10, data, [new(10, "Image", 1)]));

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"ContentTypeId\":10"));
            Assert.That(json, Does.Contain("\"LastModified\":\"2026-10-01\""));
            Assert.That(json, Does.Contain("\"Channel\":\"Content hub - Marketing\""));
            Assert.That(json, Does.Contain("\"ContentTypeOptions\":[{\"Id\":10,\"DisplayName\":\"Image\",\"ItemCount\":1}]"));
            Assert.That(json, Does.Contain("\"TertiaryValue\":1"));
        });
    }
}
