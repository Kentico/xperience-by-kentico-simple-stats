using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.TagUsage;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class TagUsageSqlTests
{
    [Test]
    public void Build_ReturnsStatementsAndFiveQueries_WithoutPlaceholders()
    {
        string sql = TagUsageSql.Build(hasKind: true, hasTaxonomy: true);

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.StartWith("SET NOCOUNT ON;"));
            Assert.That(sql, Does.Contain("INSERT INTO @Uses"));
            Assert.That(sql, Does.Contain("INSERT INTO @Tags"));
            Assert.That(Count(sql, "FROM @Uses U"), Is.EqualTo(3));
            Assert.That(Count(sql, "FROM @Tags S"), Is.EqualTo(2));
            Assert.That(sql, Does.Not.Contain("{").And.Not.Contain("}"));
        });
    }

    [Test]
    public void Build_Kind_FiltersUsesAndVariants()
    {
        string withKind = TagUsageSql.Build(hasKind: true, hasTaxonomy: false);
        string withoutKind = TagUsageSql.Build(hasKind: false, hasTaxonomy: false);

        Assert.Multiple(() =>
        {
            Assert.That(Count(withKind, "C.[ClassContentTypeType] = " + StatsContentSql.KindParameter), Is.EqualTo(2));
            Assert.That(withoutKind, Does.Not.Contain(StatsContentSql.KindParameter));
            Assert.That(Count(withKind, StatsContentSql.VariantsFrom), Is.EqualTo(1));
        });
    }

    [Test]
    public void Build_Taxonomy_FiltersUsesAndTags()
    {
        string withTaxonomy = TagUsageSql.Build(hasKind: false, hasTaxonomy: true);
        string withoutTaxonomy = TagUsageSql.Build(hasKind: false, hasTaxonomy: false);

        Assert.Multiple(() =>
        {
            Assert.That(Count(withTaxonomy, "G.[TagTaxonomyID] = " + TagUsageSql.TaxonomyParameter), Is.EqualTo(2));
            Assert.That(withoutTaxonomy, Does.Not.Contain(TagUsageSql.TaxonomyParameter));
        });
    }

    [Test]
    public void Build_UsesAreDistinctVariants_InAnyField()
    {
        string sql = TagUsageSql.Build(hasKind: false, hasTaxonomy: false);

        Assert.Multiple(() =>
        {
            // Two languages of one item are two variants (two uses); one variant with the tag in two fields is one use.
            Assert.That(sql, Does.Contain("COUNT(DISTINCT U.[VariantID]) AS [Uses]"));
            Assert.That(sql, Does.Contain("GROUP BY G.[TagID], G.[TagTaxonomyID], G.[TagTitle], X.[TaxonomyTitle], P.[TagTitle]"));
            Assert.That(sql, Does.Contain("COUNT(DISTINCT U.[VariantID]) AS [Tagged]"));
            Assert.That(sql, Does.Contain("GROUP BY U.[FieldGUID], U.[ClassID]"));
        });
    }

    [Test]
    public void Build_UnusedTags_CountOnlyTheirOwnRows_InAllContent()
    {
        string sql = TagUsageSql.Build(hasKind: true, hasTaxonomy: false);

        Assert.Multiple(() =>
        {
            // A parent tag is used only when it is assigned itself; uses of its children do not count. The kind filter does not apply.
            Assert.That(sql, Does.Contain("SELECT 1 FROM [CMS_ContentItemTag] T WHERE T.[ContentItemTagTagGUID] = G.[TagGUID]"));
            Assert.That(sql, Does.Contain("WHERE S.[IsUsed] = 0"));
            Assert.That(sql, Does.Not.Contain("TagParentID] IN"));
        });
    }

    [Test]
    public void Build_Lists_AreLimited()
    {
        string sql = TagUsageSql.Build(hasKind: false, hasTaxonomy: false);

        Assert.Multiple(() =>
        {
            Assert.That(Count(sql, "TOP (" + TagUsageSql.LimitParameter + ")"), Is.EqualTo(1));
            Assert.That(Count(sql, "TOP (" + TagUsageSql.UnusedLimitParameter + ")"), Is.EqualTo(1));
            Assert.That(sql, Does.Contain("ORDER BY [Uses] DESC, G.[TagTitle], G.[TagID]"));
        });
    }

    private static int Count(string text, string value) =>
        (text.Length - text.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;
}
