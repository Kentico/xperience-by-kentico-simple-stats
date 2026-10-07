using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContentInventory;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ReusableUsage;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class ReusableUsageSqlTests
{
    [Test]
    public void Build_ReturnsItemsStatementAndThreeQueries_WithoutPlaceholders()
    {
        string sql = ReusableUsageSql.Build(hasContentType: true);

        Assert.Multiple(() =>
        {
            Assert.That(Count(sql, "FROM @Items X"), Is.EqualTo(3));
            Assert.That(sql, Does.Contain("INSERT INTO @Items"));
            Assert.That(sql, Does.StartWith("SET NOCOUNT ON;"));
            Assert.That(sql, Does.Not.Contain("{").And.Not.Contain("}"));
        });
    }

    [Test]
    public void Build_ContentType_FiltersTheItemsOnce()
    {
        string withType = ReusableUsageSql.Build(hasContentType: true);
        string withoutType = ReusableUsageSql.Build(hasContentType: false);

        Assert.Multiple(() =>
        {
            Assert.That(Count(withType, "I.[ContentItemContentTypeID] = " + ReusableUsageSql.ContentTypeParameter), Is.EqualTo(1));
            Assert.That(withoutType, Does.Not.Contain(ReusableUsageSql.ContentTypeParameter));
        });
    }

    [Test]
    public void Build_UsesTheSharedReusableAndUsageDefinition()
    {
        string sql = ReusableUsageSql.Build(hasContentType: false);

        Assert.Multiple(() =>
        {
            Assert.That(Count(sql, StatsContentUsageSql.ReusableCondition), Is.EqualTo(1));
            Assert.That(Count(sql, StatsContentUsageSql.UsageQuery), Is.EqualTo(1));
            Assert.That(Count(sql, StatsContentUsageSql.LatestVariantApply), Is.EqualTo(1));
            Assert.That(sql, Does.Contain("LEFT JOIN [Usage] U ON U.[TargetItemID] = I.[ContentItemID]"));
        });
    }

    [Test]
    public void ContentInventory_UnusedItems_UseTheSameDefinition()
    {
        string sql = ContentInventorySql.Build(hasKind: false, hasChannel: false);

        Assert.Multiple(() =>
        {
            Assert.That(Count(sql, StatsContentUsageSql.ReusableCondition), Is.EqualTo(2));
            Assert.That(Count(sql, StatsContentUsageSql.UnusedCondition), Is.EqualTo(2));
            Assert.That(Count(sql, StatsContentUsageSql.LatestVariantApply), Is.EqualTo(1));
        });
    }

    [Test]
    public void UsageQuery_CountsDistinctReferencingItems_NotReferencesOrVariants()
    {
        string sql = StatsContentUsageSql.UsageQuery;

        Assert.Multiple(() =>
        {
            // Variants and versions of one referencing item are several reference rows but one (target, source item) row.
            Assert.That(sql, Does.Contain("SELECT DISTINCT"));
            Assert.That(sql, Does.Contain("D.[ContentItemCommonDataContentItemID] AS [SourceItemID]"));
            Assert.That(sql, Does.Contain("ON D.[ContentItemCommonDataID] = R.[ContentItemReferenceSourceCommonDataID]"));
            Assert.That(sql, Does.Contain("COUNT(*) AS [Usages]"));
            Assert.That(sql, Does.Contain("GROUP BY S.[TargetItemID]"));

            // Each kind of referencing item is counted with the product's content type type values (parameters).
            Assert.That(sql, Does.Contain("S.[SourceKind] = " + StatsContentUsageSql.WebsiteKindParameter + " THEN 1"));
            Assert.That(sql, Does.Contain("S.[SourceKind] = " + StatsContentUsageSql.EmailKindParameter + " THEN 1"));
            Assert.That(sql, Does.Contain("S.[SourceKind] = " + StatsContentUsageSql.ReusableKindParameter + " THEN 1"));
            Assert.That(sql, Does.Contain("S.[SourceKind] = " + StatsContentUsageSql.HeadlessKindParameter + " THEN 1"));

            // Self-references are not excluded: an item that references itself is used, as in the content inventory.
            Assert.That(sql, Does.Not.Contain("<> R.[ContentItemReferenceTargetItemID]"));
        });
    }

    [Test]
    public void Build_Totals_BucketByParameters()
    {
        string sql = ReusableUsageSql.Build(hasContentType: false);

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("X.[Usages] = 0 THEN 1"));
            Assert.That(sql, Does.Contain("X.[Usages] = 1 THEN 1"));
            Assert.That(sql, Does.Contain($"X.[Usages] > 1 AND X.[Usages] <= {ReusableUsageSql.FewMaxParameter} THEN 1"));
            Assert.That(sql, Does.Contain($"X.[Usages] > {ReusableUsageSql.FewMaxParameter} AND X.[Usages] <= {ReusableUsageSql.SomeMaxParameter} THEN 1"));
            Assert.That(sql, Does.Contain($"X.[Usages] > {ReusableUsageSql.SomeMaxParameter} THEN 1"));
        });
    }

    [Test]
    public void Build_MostUsed_IsLimitedAndOrderedByUsages()
    {
        string sql = ReusableUsageSql.Build(hasContentType: false);

        Assert.Multiple(() =>
        {
            Assert.That(Count(sql, "TOP (" + ReusableUsageSql.LimitParameter + ")"), Is.EqualTo(1));
            Assert.That(sql, Does.Contain("WHERE X.[Usages] > 0"));
            Assert.That(sql, Does.Contain("ORDER BY X.[Usages] DESC, I.[ContentItemID]"));
            Assert.That(sql, Does.Contain("I.[ContentItemWorkspaceID] AS [WorkspaceID]"));
            Assert.That(sql, Does.Contain("LEFT JOIN [CMS_Workspace] WS ON WS.[WorkspaceID] = I.[ContentItemWorkspaceID]"));
        });
    }

    private static int Count(string text, string value) =>
        (text.Length - text.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;
}
