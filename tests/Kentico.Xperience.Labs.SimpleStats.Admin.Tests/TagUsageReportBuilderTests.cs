using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.TagUsage;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class TagUsageReportBuilderTests
{
    private static readonly Guid imageTaxonomy = new("b4c5f28d-b291-42fd-8104-8a314d4ed30f");
    private static readonly Guid productTaxonomy = new("1477faae-3c0b-4d40-baf0-e0d1315b8238");
    private static readonly Guid imageField = new("01a75a9a-3dd5-4d83-9a3b-ecaecf9e93c9");
    private static readonly Guid schemaField = new("a3b6d816-b57b-4e96-bd98-3afdff882619");
    private static readonly Guid cafeField = new("fab42a26-dde5-4c5b-8b7d-2c3fb440a83a");

    private static readonly IReadOnlyList<TagUsageFieldDefinition> fields =
    [
        new(imageField, "ImageTags", "Tags", [imageTaxonomy], null, [10]),
        new(schemaField, "ProductFieldTags", "Tags", [productTaxonomy], "Product fields", [20, 21, 22]),
        new(cafeField, "CafePromotion", "Cafe promotion", [productTaxonomy, imageTaxonomy], null, [30]),
    ];

    private static readonly TagUsageData data = new(
        [
            new(4, imageTaxonomy, "ImageTags", "Image tags", 3, 1, 2),
            new(7, productTaxonomy, "ProductTags", "Product tags", 3, 1, 1),
        ],
        [
            new(22, 4, "Product shot", "Image tags", null, 12),
            new(31, 7, "Acidy", "Product tags", "Sweet", 2),
            new(5, 4, "Brewing", "Image tags", null, 3),
        ],
        [
            new(27, 7, "Sweet", "Product tags", null, 0),
            new(8, 4, "Coffee", "Image tags", null, 0),
        ],
        [
            new(imageField, 10, 12),
            new(schemaField, 20, 2),
            new(schemaField, 21, 3),
            new(cafeField, 30, 1),
        ],
        [
            new(10, "Acme.Image", "Image", 20),
            new(20, "Acme.Coffee", "Coffee", 4),
            new(21, "Acme.Grinder", "Grinder", 5),
            new(30, "Acme.Cafe", "Cafe", 3),
        ]);

    [Test]
    public void Build_NoTaxonomies_IsEmpty()
    {
        var result = TagUsageReportBuilder.Build(null, null, TagUsageData.Empty, [], []);

        Assert.Multiple(() =>
        {
            Assert.That(result.TaxonomyCount, Is.Zero);
            Assert.That(result.TagCount, Is.Zero);
            Assert.That(result.UnusedTagCount, Is.Zero);
            Assert.That(result.UntaggedShare, Is.Null);
            Assert.That(result.TopTags.Items, Is.Empty);
            Assert.That(result.UnusedTags, Is.Empty);
            Assert.That(result.Fields, Is.Empty);
            Assert.That(result.TaxonomyOptions, Is.Empty);
        });
    }

    [Test]
    public void Build_All_SumsTaxonomies_AndRanksTopTags()
    {
        var result = TagUsageReportBuilder.Build(null, null, data, fields, TagUsageReportBuilder.GetTaxonomyOptions(data.Taxonomies));

        Assert.Multiple(() =>
        {
            Assert.That(result.TaxonomyCount, Is.EqualTo(2));
            Assert.That(result.TagCount, Is.EqualTo(6));
            Assert.That(result.UnusedTagCount, Is.EqualTo(2));
            Assert.That(result.TopTags.Items.Select(i => i.Label), Is.EqualTo(new[] { "Product shot", "Brewing", "Acidy" }));
            Assert.That(result.TopTags.Items.Select(i => i.SecondaryLabel), Is.EqualTo(new[] { "Image tags", "Image tags", "Product tags" }));
            Assert.That(result.TopTags.Items[0].Value, Is.EqualTo(12));
            Assert.That(result.TopTags.ItemCount, Is.EqualTo(3));
            Assert.That(result.TaxonomyOptions.Select(o => o.DisplayName), Is.EqualTo(new[] { "Image tags", "Product tags" }));
        });
    }

    [Test]
    public void Build_ParentWithUsedChild_ParentIsListedAsUnused()
    {
        var result = TagUsageReportBuilder.Build(null, null, data, fields, []);

        Assert.Multiple(() =>
        {
            Assert.That(result.TopTags.Items.Select(i => i.Label), Does.Contain("Acidy"));
            Assert.That(result.UnusedTags.Select(t => t.Tag), Is.EqualTo(new[] { "Sweet", "Coffee" }));
            Assert.That(result.UnusedTags[0].Parent, Is.Null);
        });
    }

    [Test]
    public void Build_SchemaField_IsOneRowForAllContentTypesWithVariants()
    {
        var result = TagUsageReportBuilder.Build(null, null, data, fields, []);

        var schemaRow = result.Fields.Single(f => f.Key == schemaField.ToString());

        Assert.Multiple(() =>
        {
            // Type 22 has no variants in the filter, so it is not counted or listed.
            Assert.That(schemaRow.Total, Is.EqualTo(9));
            Assert.That(schemaRow.Covered, Is.EqualTo(5));
            Assert.That(schemaRow.Missing, Is.EqualTo(4));
            Assert.That(schemaRow.SecondaryLabel, Is.EqualTo("Coffee, Grinder (schema Product fields)"));
            // Two fields have the caption "Tags": the schema or the content type tells them apart.
            Assert.That(schemaRow.Label, Is.EqualTo("Tags (Product fields)"));
            Assert.That(result.Fields.Single(f => f.Key == imageField.ToString()).Label, Is.EqualTo("Tags (Image)"));
        });
    }

    [Test]
    public void Build_Fields_UntaggedShareOverAllFields_MostUntaggedFirst()
    {
        var result = TagUsageReportBuilder.Build(null, null, data, fields, []);

        Assert.Multiple(() =>
        {
            Assert.That(result.Fields.Select(f => f.Missing), Is.EqualTo(new[] { 8, 4, 2 }));
            Assert.That(result.FieldVariants, Is.EqualTo(20 + 9 + 3));
            Assert.That(result.UntaggedVariants, Is.EqualTo(14));
            Assert.That(result.UntaggedShare, Is.EqualTo(14d / 32).Within(1e-9));
        });
    }

    [Test]
    public void Build_TaggedMoreThanVariants_IsCappedPerContentType()
    {
        var capped = data with { FieldCounts = [new(imageField, 10, 50)] };

        var result = TagUsageReportBuilder.Build(null, null, capped, fields, []);

        var image = result.Fields.Single(f => f.Key == imageField.ToString());
        Assert.Multiple(() =>
        {
            Assert.That(image.Covered, Is.EqualTo(20));
            Assert.That(image.Missing, Is.Zero);
        });
    }

    [Test]
    public void Build_TaxonomyFilter_KeepsTheTaxonomyAndItsFields()
    {
        var result = TagUsageReportBuilder.Build(null, 7, data, fields, TagUsageReportBuilder.GetTaxonomyOptions(data.Taxonomies));

        Assert.Multiple(() =>
        {
            Assert.That(result.TaxonomyId, Is.EqualTo(7));
            Assert.That(result.TaxonomyCount, Is.EqualTo(1));
            Assert.That(result.TagCount, Is.EqualTo(3));
            Assert.That(result.UnusedTagCount, Is.EqualTo(1));
            // The schema field and the cafe field (it offers both taxonomies) have Product tags; the image field does not.
            Assert.That(result.Fields.Select(f => f.Key), Is.EquivalentTo(new[] { schemaField.ToString(), cafeField.ToString() }));
            Assert.That(result.TaxonomyOptions, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void Build_LinksTagsToTheTaxonomiesApplication()
    {
        var result = TagUsageReportBuilder.Build(null, null, data, fields, [], (taxonomyId, tagId) => $"/taxonomy/{taxonomyId}/tag/{tagId}");

        Assert.Multiple(() =>
        {
            Assert.That(result.TopTags.Items[0].AdminPath, Is.EqualTo("/taxonomy/4/tag/22"));
            Assert.That(result.UnusedTags[0].AdminPath, Is.EqualTo("/taxonomy/7/tag/27"));
        });
    }

    [TestCase(7, 7)]
    [TestCase(99, null)]
    [TestCase(0, null)]
    [TestCase(-1, null)]
    [TestCase(null, null)]
    public void NormalizeTaxonomy_UnknownMeansAll(int? taxonomyId, int? expected) =>
        Assert.That(TagUsageReportBuilder.NormalizeTaxonomy(taxonomyId, TagUsageReportBuilder.GetTaxonomyOptions(data.Taxonomies)), Is.EqualTo(expected));

    [TestCase("[\"0eb82e7d-4af6-496a-bfc2-c0b3419956b7\"]", 1)]
    [TestCase("[\"0eb82e7d-4af6-496a-bfc2-c0b3419956b7\",\"1477faae-3c0b-4d40-baf0-e0d1315b8238\"]", 2)]
    [TestCase("", 0)]
    [TestCase(null, 0)]
    [TestCase("not json", 0)]
    public void ParseTaxonomies_ReadsTheTaxonomyGroupSetting(string? setting, int count) =>
        Assert.That(TagUsageFieldProvider.ParseTaxonomies(setting), Has.Count.EqualTo(count));
}
