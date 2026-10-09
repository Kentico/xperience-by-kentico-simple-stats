using System.Text.Json;

using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContentInventory;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PageFreshness;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class PageFreshnessReportBuilderTests
{
    private static readonly DateOnly today = new(2026, 10, 7);

    private static readonly StatsQuery query = new(new(2026, 9, 8), today, StatsGrouping.Day, null);

    private static readonly IReadOnlyList<StatsChannelOption> websiteChannels = [new(2, "Pages", "Website")];

    private static DateTime At(int year, int month, int day, int hour = 0) => new(year, month, day, hour, 0, 0, DateTimeKind.Unspecified);

    private static PageFreshnessRow Row(int id, int visits, DateTime modified, DateTime? firstPublished = null, string language = "en") =>
        new(id, id + 100, 1, $"Page {id}", "Pages", language, language == "en" ? "English" : "Spanish", $"/Page_{id}", modified, firstPublished, visits, visits > 0 ? 1 : 0);

    [Test]
    public void Build_NoPagesOrActivities_ReturnsZerosAndEmptyLists()
    {
        var result = PageFreshnessReportBuilder.Build(query, PageFreshnessData.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(result.PublishedPages, Is.Zero);
            Assert.That(result.Visits, Is.Zero);
            Assert.That(result.StaleShare, Is.Zero);
            Assert.That(result.StaleVisitShare, Is.Zero);
            Assert.That(result.StaleMonths, Is.EqualTo(ContentInventoryReportBuilder.StaleMonths));
            Assert.That(result.StalePopular, Is.Empty);
            Assert.That(result.NoVisits, Is.Empty);
            Assert.That(result.AgeBuckets.Items.Select(i => i.Value), Is.All.Zero);
        });
    }

    [Test]
    public void Build_PublishedPagesWithoutVisits_CountAsNoVisits()
    {
        var data = PageFreshnessData.Empty with
        {
            Totals = new(3, 1, 0, 0, 0, 3),
            NoVisits = [Row(1, 0, At(2024, 1, 1), At(2023, 1, 1)), Row(2, 0, At(2026, 9, 1), null)],
        };

        var result = PageFreshnessReportBuilder.Build(query, data);

        Assert.Multiple(() =>
        {
            Assert.That(result.NoVisitPages, Is.EqualTo(3));
            Assert.That(result.NoVisits.Select(i => i.FirstPublished), Is.EqualTo(new DateOnly?[] { new(2023, 1, 1), null }));
            Assert.That(result.StaleVisitShare, Is.Zero);
        });
    }

    [Test]
    public void Build_SharesAndAgeBuckets_UseTheContentInventoryBuckets()
    {
        var data = PageFreshnessData.Empty with
        {
            Totals = new(10, 4, 200, 50, 2, 1),
            AgePages = new(5, 1, 0, 4),
            AgeVisits = new(140, 10, 0, 50),
        };

        var result = PageFreshnessReportBuilder.Build(query, data);

        Assert.Multiple(() =>
        {
            Assert.That(result.StaleShare, Is.EqualTo(0.4).Within(1e-9));
            Assert.That(result.StaleVisitShare, Is.EqualTo(0.25).Within(1e-9));
            Assert.That(result.AgeBuckets.Items.Select(i => i.Key), Is.EqualTo(new[]
            {
                ContentInventoryReportBuilder.Under3MonthsKey,
                ContentInventoryReportBuilder.Months3To6Key,
                ContentInventoryReportBuilder.Months6To12Key,
                ContentInventoryReportBuilder.Over12MonthsKey,
            }));
            Assert.That(result.AgeBuckets.Items.Select(i => i.Value), Is.EqualTo(new[] { 5m, 1m, 0m, 4m }));
            Assert.That(result.AgeBuckets.Items.Select(i => i.SecondaryValue), Is.EqualTo(new decimal?[] { 140, 10, 0, 50 }));
            Assert.That(result.AgeBuckets.Total, Is.EqualTo(10));
            Assert.That(result.AgeBuckets.From, Is.EqualTo(query.From));
        });
    }

    [Test]
    public void Build_StalePopular_KeepsSqlOrder_WithLinksPerLanguage()
    {
        var data = PageFreshnessData.Empty with
        {
            Totals = new(2, 2, 7, 7, 2, 0),
            StalePopular = [Row(1, 5, At(2024, 5, 1, 10), At(2023, 1, 1)), Row(2, 2, At(2025, 1, 1), At(2024, 1, 1), "es")],
        };

        var result = PageFreshnessReportBuilder.Build(query, data);

        var first = result.StalePopular[0];
        Assert.Multiple(() =>
        {
            Assert.That(result.StalePopular.Select(i => i.Visits), Is.EqualTo(new[] { 5, 2 }));
            Assert.That(first.Key, Is.EqualTo("1-101"));
            Assert.That(first.Name, Is.EqualTo("Page 1"));
            Assert.That(first.Channel, Is.EqualTo("Pages"));
            Assert.That(first.Language, Is.EqualTo("English"));
            Assert.That(first.TreePath, Is.EqualTo("/Page_1"));
            Assert.That(first.LastModified, Is.EqualTo(new DateOnly(2024, 5, 1)));
            Assert.That(first.Visitors, Is.EqualTo(1));
            Assert.That(first.AdminPath, Is.EqualTo("/webpages-1/en_101/content"));
            Assert.That(result.StalePopular[1].AdminPath, Is.EqualTo("/webpages-1/es_102/content"));
        });
    }

    [Test]
    public void Build_Lists_AreLimited_CountsAreNot()
    {
        var rows = Enumerable.Range(1, PageFreshnessReportBuilder.ListLimit + 3).Select(id => Row(id, 0, At(2024, 1, 1))).ToList();
        var data = PageFreshnessData.Empty with { Totals = new(rows.Count, 0, 0, 0, 0, 0), NoVisits = rows };

        var result = PageFreshnessReportBuilder.Build(query, data);

        Assert.Multiple(() =>
        {
            Assert.That(result.NoVisits, Has.Count.EqualTo(PageFreshnessReportBuilder.ListLimit));
            Assert.That(result.NoVisitPages, Is.EqualTo(rows.Count));
        });
    }

    [Test]
    public void StaleThreshold_IsTheContentInventoryThreshold()
    {
        var now = At(2026, 10, 7, 12);

        Assert.Multiple(() =>
        {
            Assert.That(ContentInventorySql.GetStaleBefore(now), Is.EqualTo(At(2025, 10, 7, 12)));
            Assert.That(
                ContentInventorySql.GetAgeParameters(now).Single(p => p.Name == ContentInventorySql.Age12Parameter).Value,
                Is.EqualTo(ContentInventorySql.GetStaleBefore(now)));
        });
    }

    [TestCase(2, 2)]
    [TestCase(1, null)]
    [TestCase(0, null)]
    [TestCase(null, null)]
    public void Normalize_KeepsWebsiteChannelsOnly(int? channelId, int? expected)
    {
        var normalized = PageFreshnessReportBuilder.Normalize(new StatsFilter { ChannelId = channelId }, today, websiteChannels);

        Assert.That(normalized.ChannelId, Is.EqualTo(expected));
    }

    [Test]
    public void Normalize_IgnoresGrouping_AndDefaultsTo30Days()
    {
        var normalized = PageFreshnessReportBuilder.Normalize(new StatsFilter { Grouping = StatsGrouping.Month }, today, websiteChannels);

        Assert.That(normalized, Is.EqualTo(new StatsQuery(today.AddDays(-29), today, StatsGrouping.Day, null)));
    }

    [Test]
    public void Result_SerializesForTheClient()
    {
        var data = PageFreshnessData.Empty with
        {
            Totals = new(1, 1, 2, 2, 1, 0),
            StalePopular = [Row(1, 2, At(2025, 3, 1), null)],
        };

        string json = JsonSerializer.Serialize(PageFreshnessReportBuilder.Build(query, data));

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"LastModified\":\"2025-03-01\""));
            Assert.That(json, Does.Contain("\"FirstPublished\":null"));
            Assert.That(json, Does.Contain("\"From\":\"2026-09-08\""));
            Assert.That(json, Does.Contain("\"AdminPath\":\"/webpages-1/en_101/content\""));
            Assert.That(json, Does.Contain("\"StaleMonths\":12"));
        });
    }
}
