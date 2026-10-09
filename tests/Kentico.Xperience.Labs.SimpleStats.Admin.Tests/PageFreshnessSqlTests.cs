using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContentInventory;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PageFreshness;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class PageFreshnessSqlTests
{
    [Test]
    public void Build_HasNoPlaceholders_AndFourResultSets()
    {
        string sql = PageFreshnessSql.Build(hasChannel: true);

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Not.Contain("{").And.Not.Contain("}"));
            Assert.That(Count(sql, "SELECT TOP (" + PageFreshnessSql.LimitParameter + ")"), Is.EqualTo(2));
            Assert.That(Count(sql, "FROM @Pages X"), Is.EqualTo(4));
        });
    }

    [Test]
    public void Build_AggregatesVisitsOnce_PerPageAndLanguage()
    {
        string sql = PageFreshnessSql.Build(hasChannel: false);

        Assert.Multiple(() =>
        {
            Assert.That(Count(sql, "[OM_Activity]"), Is.EqualTo(1));
            Assert.That(sql, Does.Contain("A.[ActivityType] = " + PageFreshnessSql.PageVisitTypeParameter));
            Assert.That(sql, Does.Contain("GROUP BY A.[ActivityWebPageItemGUID], A.[ActivityLanguageID]"));
            // A visit counts only for the variant in the visit's language (as the web page Stats tab).
            Assert.That(sql, Does.Contain("V.[PageGUID] = P.[WebPageItemGUID]"));
            Assert.That(sql, Does.Contain("AND V.[LanguageID] = M.[ContentItemLanguageMetadataContentLanguageID]"));
            Assert.That(sql, Does.Contain("COUNT(DISTINCT A.[ActivityContactID]) AS [Visitors]"));
        });
    }

    [Test]
    public void Build_PagesArePublishedWebsiteItemsWithUrl_FoldersExcluded()
    {
        string sql = PageFreshnessSql.Build(hasChannel: false);

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("C.[ClassType] = " + StatsContentSql.ClassTypeParameter));
            Assert.That(sql, Does.Contain("C.[ClassContentTypeType] = " + StatsContentSql.KindParameter));
            Assert.That(sql, Does.Contain("AND C.[ClassWebPageHasUrl] = 1"));
            Assert.That(sql, Does.Contain("D1.[ContentItemCommonDataVersionStatus] = " + PageFreshnessSql.PublishedStatusParameter));
            Assert.That(sql, Does.Not.Contain(StatsContentSql.ChannelParameter));
        });
    }

    [Test]
    public void Build_Channel_FiltersThePages()
    {
        string sql = PageFreshnessSql.Build(hasChannel: true);

        Assert.That(Count(sql, "I.[ContentItemChannelID] = " + StatsContentSql.ChannelParameter), Is.EqualTo(1));
    }

    [Test]
    public void Build_Stale_IsBeforeTheContentInventoryThreshold()
    {
        string sql = PageFreshnessSql.Build(hasChannel: false);

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("X.[ModifiedWhen] < " + ContentInventorySql.Age12Parameter));
            Assert.That(sql, Does.Contain(ContentInventorySql.AgeColumns("X.[ModifiedWhen]")));
            Assert.That(sql, Does.Contain(ContentInventorySql.AgeColumns("X.[ModifiedWhen]", "X.[Visits]", PageFreshnessSql.VisitsSuffix)));
            Assert.That(sql, Does.Contain("WHERE X.[ModifiedWhen] < @Age12 AND X.[Visits] > 0"));
            Assert.That(sql, Does.Contain("ORDER BY X.[Visits] DESC, X.[Visitors] DESC, X.[VariantID]"));
        });
    }

    [Test]
    public void Build_NoVisits_OnlyPagesFirstPublishedBeforeTheRange()
    {
        string sql = PageFreshnessSql.Build(hasChannel: false);

        Assert.Multiple(() =>
        {
            Assert.That(
                Count(sql, "X.[Visits] = 0 AND (X.[FirstPublishedWhen] IS NULL OR X.[FirstPublishedWhen] < " + PageFreshnessSql.FromParameter + ")"),
                Is.EqualTo(2));
            Assert.That(sql, Does.Contain("COALESCE(D1.[ContentItemCommonDataFirstPublishedWhen], D1.[ContentItemCommonDataLastPublishedWhen])"));
            Assert.That(sql, Does.Contain("ORDER BY X.[FirstPublishedWhen], X.[VariantID]"));
        });
    }

    [Test]
    public void ContentInventory_AgeColumns_AreUnchanged()
    {
        string sql = ContentInventorySql.Build(hasKind: false, hasChannel: false);

        Assert.That(sql, Does.Contain(
            "ISNULL(SUM(CASE WHEN M.[ContentItemLanguageMetadataModifiedWhen] < @Age12 THEN 1 ELSE 0 END), 0) AS [Over12Months]"));
    }

    private static int Count(string text, string value) =>
        (text.Length - text.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;
}
