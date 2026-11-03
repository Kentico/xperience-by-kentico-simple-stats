using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.NewContacts;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class NewContactsReportBuilderTests
{
    [Test]
    public void Build_SplitsIdentifiedAndAnonymous_InFixedOrder()
    {
        var query = new StatsQuery(new(2026, 9, 1), new(2026, 9, 3), StatsGrouping.Day, null);
        NewContactsDailyCount[] rows =
        [
            new(new(2026, 9, 1), 1, 6),
            new(new(2026, 9, 3), 2, 1),
        ];

        var result = NewContactsReportBuilder.Build(query, rows);

        // Anonymous has the larger total but stays second.
        Assert.That(result.Trend.Series.Select(s => s.Key), Is.EqualTo(new[] { NewContactsReportBuilder.IdentifiedKey, NewContactsReportBuilder.AnonymousKey }));
        Assert.That(result.Trend.Series.Select(s => s.DisplayName), Is.EqualTo(new[] { "Identified", "Anonymous" }));
        Assert.That(result.Trend.Series[0].Values, Is.EqualTo(new[] { 1, 0, 2 }));
        Assert.That(result.Trend.Series[1].Values, Is.EqualTo(new[] { 6, 0, 1 }));
        Assert.That(result.Identified, Is.EqualTo(3));
        Assert.That(result.Anonymous, Is.EqualTo(7));
        Assert.That(result.Trend.Total, Is.EqualTo(10));
        Assert.That(result.IdentifiedShare, Is.EqualTo(0.3).Within(1e-9));
    }

    [Test]
    public void Build_Week_SumsDaysIntoWeeks()
    {
        // Mon Sep 7 .. Sun Sep 20 -> two weeks
        var query = new StatsQuery(new(2026, 9, 7), new(2026, 9, 20), StatsGrouping.Week, null);
        NewContactsDailyCount[] rows =
        [
            new(new(2026, 9, 7), 1, 1),
            new(new(2026, 9, 13), 1, 0),
            new(new(2026, 9, 14), 0, 4),
        ];

        var result = NewContactsReportBuilder.Build(query, rows);

        Assert.That(result.Trend.Periods, Has.Count.EqualTo(2));
        Assert.That(result.Trend.Series[0].Values, Is.EqualTo(new[] { 2, 0 }));
        Assert.That(result.Trend.Series[1].Values, Is.EqualTo(new[] { 1, 4 }));
    }

    [Test]
    public void Build_Month_SumsDaysIntoMonths()
    {
        var query = new StatsQuery(new(2026, 7, 15), new(2026, 9, 10), StatsGrouping.Month, null);
        NewContactsDailyCount[] rows =
        [
            new(new(2026, 7, 20), 1, 0),
            new(new(2026, 9, 1), 3, 2),
            new(new(2026, 9, 10), 1, 0),
        ];

        var result = NewContactsReportBuilder.Build(query, rows);

        Assert.That(result.Trend.Periods.Select(p => p.Label), Is.EqualTo(new[] { "Jul 2026", "Aug 2026", "Sep 2026" }));
        Assert.That(result.Trend.Series[0].Values, Is.EqualTo(new[] { 1, 0, 4 }));
        Assert.That(result.Trend.Series[1].Values, Is.EqualTo(new[] { 0, 0, 2 }));
    }

    [Test]
    public void Build_NoData_ReturnsZeroFilledSeriesAndZeroShare()
    {
        var query = new StatsQuery(new(2026, 9, 1), new(2026, 9, 5), StatsGrouping.Day, null);

        var result = NewContactsReportBuilder.Build(query, []);

        Assert.Multiple(() =>
        {
            Assert.That(result.Trend.Periods, Has.Count.EqualTo(5));
            Assert.That(result.Trend.Series, Has.Count.EqualTo(2));
            Assert.That(result.Trend.Series[0].Values, Is.EqualTo(new[] { 0, 0, 0, 0, 0 }));
            Assert.That(result.Trend.Series[1].Values, Is.EqualTo(new[] { 0, 0, 0, 0, 0 }));
            Assert.That(result.Trend.Total, Is.Zero);
            Assert.That(result.Identified, Is.Zero);
            Assert.That(result.Anonymous, Is.Zero);
            Assert.That(result.IdentifiedShare, Is.Zero);
        });
    }

    [Test]
    public void Build_ComparesWithPreviousPeriod_RowsBeforeRangeNotInTrend()
    {
        // Sep 4–6; previous period Sep 1–3.
        var query = new StatsQuery(new(2026, 9, 4), new(2026, 9, 6), StatsGrouping.Day, null);
        NewContactsDailyCount[] rows =
        [
            new(new(2026, 8, 31), 9, 9),
            new(new(2026, 9, 1), 2, 2),
            new(new(2026, 9, 3), 0, 2),
            new(new(2026, 9, 5), 3, 1),
        ];

        var result = NewContactsReportBuilder.Build(query, rows);

        Assert.Multiple(() =>
        {
            Assert.That(result.Trend.Total, Is.EqualTo(4));
            Assert.That(result.TotalComparison.Current, Is.EqualTo(4));
            Assert.That(result.TotalComparison.Previous, Is.EqualTo(6));
            Assert.That(result.TotalComparison.Change, Is.EqualTo(-2.0 / 6).Within(1e-9));
            Assert.That(result.TotalComparison.PreviousFrom, Is.EqualTo(new DateOnly(2026, 9, 1)));
            Assert.That(result.TotalComparison.PreviousTo, Is.EqualTo(new DateOnly(2026, 9, 3)));
            Assert.That((result.IdentifiedComparison.Current, result.IdentifiedComparison.Previous), Is.EqualTo((3, 2)));
            Assert.That((result.AnonymousComparison.Current, result.AnonymousComparison.Previous), Is.EqualTo((1, 4)));
        });
    }

    [Test]
    public void Build_NoPreviousContacts_ChangeIsNull()
    {
        var query = new StatsQuery(new(2026, 9, 4), new(2026, 9, 6), StatsGrouping.Day, null);

        var result = NewContactsReportBuilder.Build(query, [new(new(2026, 9, 5), 1, 0)]);

        Assert.Multiple(() =>
        {
            Assert.That(result.TotalComparison.Previous, Is.Zero);
            Assert.That(result.TotalComparison.Change, Is.Null);
            Assert.That(result.AnonymousComparison.Change, Is.Null);
        });
    }

    [Test]
    public void Build_OnlyAnonymous_ShareIsZero()
    {
        var query = new StatsQuery(new(2026, 9, 1), new(2026, 9, 1), StatsGrouping.Day, null);

        var result = NewContactsReportBuilder.Build(query, [new(new(2026, 9, 1), 0, 4)]);

        Assert.That(result.IdentifiedShare, Is.Zero);
        Assert.That(result.Anonymous, Is.EqualTo(4));
    }
}
