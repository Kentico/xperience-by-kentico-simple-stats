using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ActivityCounts;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class ActivityCountsReportBuilderTests
{
    private static readonly Dictionary<string, string> displayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pagevisit"] = "Page visit",
        ["bizformsubmit"] = "Form submission",
    };

    [Test]
    public void Build_FillsMissingDaysWithZero()
    {
        var query = new StatsQuery(new(2026, 9, 1), new(2026, 9, 5), StatsGrouping.Day, null);
        ActivityDailyCount[] rows =
        [
            new("pagevisit", new(2026, 9, 2), 3),
            new("pagevisit", new(2026, 9, 5), 1),
        ];

        var result = ActivityCountsReportBuilder.Build(query, rows, displayNames);

        Assert.That(result.Periods, Has.Count.EqualTo(5));
        Assert.That(result.Series, Has.Count.EqualTo(1));
        Assert.That(result.Series[0].Values, Is.EqualTo(new[] { 0, 3, 0, 0, 1 }));
        Assert.That(result.Series[0].Total, Is.EqualTo(4));
        Assert.That(result.Series[0].DisplayName, Is.EqualTo("Page visit"));
        Assert.That(result.Total, Is.EqualTo(4));
    }

    [Test]
    public void Build_Week_SumsDaysIntoWeeks()
    {
        // Mon Sep 7 .. Sun Sep 20 -> two weeks
        var query = new StatsQuery(new(2026, 9, 7), new(2026, 9, 20), StatsGrouping.Week, null);
        ActivityDailyCount[] rows =
        [
            new("pagevisit", new(2026, 9, 7), 2),
            new("pagevisit", new(2026, 9, 13), 3),
            new("pagevisit", new(2026, 9, 14), 5),
        ];

        var result = ActivityCountsReportBuilder.Build(query, rows, displayNames);

        Assert.That(result.Series[0].Values, Is.EqualTo(new[] { 5, 5 }));
    }

    [Test]
    public void Build_Month_SumsDaysIntoMonths()
    {
        var query = new StatsQuery(new(2026, 7, 15), new(2026, 9, 10), StatsGrouping.Month, null);
        ActivityDailyCount[] rows =
        [
            new("bizformsubmit", new(2026, 7, 20), 1),
            new("bizformsubmit", new(2026, 9, 1), 4),
            new("bizformsubmit", new(2026, 9, 10), 1),
        ];

        var result = ActivityCountsReportBuilder.Build(query, rows, displayNames);

        Assert.That(result.Periods.Select(p => p.Label), Is.EqualTo(new[] { "Jul 2026", "Aug 2026", "Sep 2026" }));
        Assert.That(result.Series[0].Values, Is.EqualTo(new[] { 1, 0, 5 }));
    }

    [Test]
    public void Build_SortsSeriesByTotalAndFallsBackToCodeName()
    {
        var query = new StatsQuery(new(2026, 9, 1), new(2026, 9, 2), StatsGrouping.Day, null);
        ActivityDailyCount[] rows =
        [
            new("pagevisit", new(2026, 9, 1), 1),
            new("customthing", new(2026, 9, 1), 7),
            new("bizformsubmit", new(2026, 9, 2), 3),
        ];

        var result = ActivityCountsReportBuilder.Build(query, rows, displayNames);

        Assert.That(result.Series.Select(s => s.DisplayName), Is.EqualTo(new[] { "customthing", "Form submission", "Page visit" }));
        Assert.That(result.Total, Is.EqualTo(11));
    }

    [Test]
    public void Build_IgnoresRowsOutsideRange()
    {
        var query = new StatsQuery(new(2026, 9, 1), new(2026, 9, 2), StatsGrouping.Week, null);
        ActivityDailyCount[] rows =
        [
            new("pagevisit", new(2026, 8, 31), 10), // same week as Sep 1, but before From
            new("pagevisit", new(2026, 9, 1), 1),
        ];

        var result = ActivityCountsReportBuilder.Build(query, rows, displayNames);

        Assert.That(result.Series[0].Values, Is.EqualTo(new[] { 1 }));
    }

    [Test]
    public void Build_NoData_ReturnsZeroFilledAxisAndNoSeries()
    {
        var query = new StatsQuery(new(2026, 9, 1), new(2026, 9, 3), StatsGrouping.Day, 5);

        var result = ActivityCountsReportBuilder.Build(query, [], displayNames);

        Assert.Multiple(() =>
        {
            Assert.That(result.Periods, Has.Count.EqualTo(3));
            Assert.That(result.Series, Is.Empty);
            Assert.That(result.Total, Is.Zero);
            Assert.That(result.ChannelId, Is.EqualTo(5));
        });
    }

    [Test]
    public void Build_ComparesTotalWithPreviousPeriod_PreviousTypesNotInSeries()
    {
        // Sep 4–6; previous period Sep 1–3.
        var query = new StatsQuery(new(2026, 9, 4), new(2026, 9, 6), StatsGrouping.Day, null);
        ActivityDailyCount[] rows =
        [
            new("pagevisit", new(2026, 8, 31), 50),
            new("pagevisit", new(2026, 9, 2), 2),
            new("bizformsubmit", new(2026, 9, 3), 2),
            new("pagevisit", new(2026, 9, 5), 6),
        ];

        var result = ActivityCountsReportBuilder.Build(query, rows, displayNames);

        Assert.Multiple(() =>
        {
            Assert.That(result.Series.Select(s => s.ActivityType), Is.EqualTo(new[] { "pagevisit" }));
            Assert.That(result.Total, Is.EqualTo(6));
            Assert.That(result.TotalComparison.Current, Is.EqualTo(6));
            Assert.That(result.TotalComparison.Previous, Is.EqualTo(4));
            Assert.That(result.TotalComparison.Change, Is.EqualTo(0.5).Within(1e-9));
        });
    }
}
