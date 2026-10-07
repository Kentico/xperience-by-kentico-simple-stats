using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PublishingActivity;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class PublishingActivitySqlTests
{
    [Test]
    public void Build_HasNoPlaceholders_AndFourResultSets()
    {
        string sql = PublishingActivitySql.Build(hasKind: true, hasChannel: true);

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Not.Contain("{").And.Not.Contain("}"));
            Assert.That(Count(sql, "SELECT TOP (" + PublishingActivitySql.LimitParameter + ")"), Is.EqualTo(1));
            Assert.That(sql, Does.Contain("[" + PublishingActivitySql.SeriesKeyColumn + "]"));
            Assert.That(sql, Does.Contain("[" + PublishingActivitySql.DateColumn + "]"));
            Assert.That(sql, Does.Contain("[" + PublishingActivitySql.CountColumn + "]"));
        });
    }

    [Test]
    public void Build_Variants_AreContentTypeItems_FoldersExcluded_WithFilters()
    {
        string sql = PublishingActivitySql.Build(hasKind: true, hasChannel: true);

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("C.[ClassType] = " + StatsContentSql.ClassTypeParameter));
            Assert.That(sql, Does.Contain("C.[ClassContentTypeType] IS NOT NULL"));
            Assert.That(Count(sql, "C.[ClassContentTypeType] = " + StatsContentSql.KindParameter), Is.EqualTo(1));
            Assert.That(Count(sql, "I.[ContentItemChannelID] = " + StatsContentSql.ChannelParameter), Is.EqualTo(1));
        });
    }

    [Test]
    public void Build_WithoutFilters_HasNoFilterParameters()
    {
        string sql = PublishingActivitySql.Build(hasKind: false, hasChannel: false);

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Not.Contain(StatsContentSql.KindParameter));
            Assert.That(sql, Does.Not.Contain(StatsContentSql.ChannelParameter));
        });
    }

    [Test]
    public void Build_FirstPublished_IsTheEarliestOfAllCommonDataRows()
    {
        string sql = PublishingActivitySql.Build(hasKind: false, hasChannel: false);

        Assert.Multiple(() =>
        {
            // Several common data rows per variant (published + newer draft): MIN, one row per variant (primary key).
            Assert.That(sql, Does.Contain("MIN(D1.[ContentItemCommonDataFirstPublishedWhen]) AS [FirstPublishedWhen]"));
            Assert.That(sql, Does.Contain("[VariantID] int NOT NULL PRIMARY KEY"));
            Assert.That(sql, Does.Contain("IN (" + PublishingActivitySql.PublishedStatusParameter + ", " + PublishingActivitySql.UnpublishedStatusParameter + ")"));
            // Published without a date: counted as unknown, not in the series.
            Assert.That(sql, Does.Contain("X.[WasPublished] = 1 AND X.[FirstPublishedWhen] IS NULL"));
        });
    }

    [Test]
    public void Build_Updates_ArePublishVersions_ExceptTheFirstPublish()
    {
        string sql = PublishingActivitySql.Build(hasKind: false, hasChannel: false);

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("V.[ContentItemVersionAction] = " + PublishingActivitySql.PublishActionParameter));
            Assert.That(sql, Does.Contain("U.[PublishNumber] = 1"));
            Assert.That(sql, Does.Contain(
                "ABS(DATEDIFF(second, X.[FirstPublishedWhen], U.[CreatedWhen])) <= " + PublishingActivitySql.FirstPublishToleranceParameter));
            Assert.That(sql, Does.Contain("U.[CreatedWhen] >= " + PublishingActivitySql.PreviousFromParameter));
        });
    }

    [Test]
    public void Build_TimeToPublish_LeavesOutFirstPublishBeforeCreated()
    {
        string sql = PublishingActivitySql.Build(hasKind: false, hasChannel: false);

        Assert.Multiple(() =>
        {
            Assert.That(Count(sql, "AND X.[FirstPublishedWhen] >= X.[CreatedWhen]"), Is.EqualTo(3));
            Assert.That(sql, Does.Contain("PERCENTILE_CONT(0.5)"));
            Assert.That(sql, Does.Contain("PERCENTILE_CONT(0.9)"));
            Assert.That(sql, Does.Contain("ORDER BY DATEDIFF(minute, X.[CreatedWhen], X.[FirstPublishedWhen]) DESC, X.[VariantID]"));
        });
    }

    [Test]
    public void Build_RangeEdges_FromInclusive_ToExclusive()
    {
        string sql = PublishingActivitySql.Build(hasKind: false, hasChannel: false);

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("X.[CreatedWhen] >= @From AND X.[CreatedWhen] < @ToExclusive"));
            Assert.That(sql, Does.Contain("X.[FirstPublishedWhen] >= @From AND X.[FirstPublishedWhen] < @ToExclusive"));
            Assert.That(sql, Does.Not.Contain("<= @ToExclusive"));
        });
    }

    private static int Count(string text, string value) =>
        (text.Length - text.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;
}
