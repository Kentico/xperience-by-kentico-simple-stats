using System.Text.Json;

using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.TranslationStatus;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class TranslationStatusReportBuilderTests
{
    private static readonly StatsSnapshotQuery all = new(null, null);

    private static readonly DateTime defaultModified = new(2026, 10, 6, 12, 0, 0, DateTimeKind.Unspecified);

    private static readonly TranslationLanguageRow english = new(1, "en", "English", true);
    private static readonly TranslationLanguageRow spanish = new(2, "es", "Spanish", false);
    private static readonly TranslationLanguageRow czech = new(3, "cs", "Czech", false);

    private static TranslationStatusData Data(
        IReadOnlyList<TranslationCountRow>? counts = null,
        IReadOnlyList<OutdatedVariantRow>? outdated = null) =>
        new([english, spanish, czech], counts ?? [], outdated ?? []);

    private static OutdatedVariantRow Outdated(int id, int languageId, double daysBehind, int? userId = 53, string? userName = "Admin") =>
        new(id, languageId, $"Item {id}", "Article", languageId == 2 ? "Spanish" : "Czech", userId, userName, defaultModified, defaultModified.AddDays(-daysBehind));

    [Test]
    public void Build_OnlyOneLanguage_HasNoOptionsOrRows()
    {
        var data = new TranslationStatusData([english], [], []);

        var result = TranslationStatusReportBuilder.Build(all, null, data);

        Assert.Multiple(() =>
        {
            Assert.That(result.LanguageOptions, Is.Empty);
            Assert.That(result.Languages, Is.Empty);
            Assert.That(result.DefaultLanguage, Is.EqualTo("English"));
            Assert.That(result.TranslatedCount, Is.Zero);
            Assert.That(result.OutdatedCount, Is.Zero);
            Assert.That(result.MissingCount, Is.Zero);
            Assert.That(result.Outdated, Is.Empty);
            Assert.That(result.ByContentType.Items, Is.Empty);
            Assert.That(result.ToleranceMinutes, Is.EqualTo(TranslationStatusReportBuilder.ToleranceMinutes));
        });
    }

    [Test]
    public void Build_NoItems_ListsEveryNonDefaultLanguageWithZeros()
    {
        var result = TranslationStatusReportBuilder.Build(all, null, Data());

        Assert.Multiple(() =>
        {
            Assert.That(result.LanguageOptions.Select(o => o.CodeName), Is.EqualTo(new[] { "cs", "es" }));
            Assert.That(result.Languages.Select(l => (l.Key, l.Covered, l.Total, l.Flagged)), Is.EqualTo(new[] { ("cs", 0, 0, 0), ("es", 0, 0, (int?)0) }));
        });
    }

    [Test]
    public void Build_SumsLanguagesOverContentTypes_MissingIsItemsWithoutAVariant()
    {
        // Spanish: 10 articles (8 translated, 2 outdated) and 5 pages (5 translated). Czech: 15 items, 1 translated.
        var data = Data(
        [
            new(2, 10, "Article", "Article", 10, 8, 2),
            new(2, 11, "Page", "Page", 5, 5, 0),
            new(3, 10, "Article", "Article", 10, 1, 0),
            new(3, 11, "Page", "Page", 5, 0, 0),
        ]);

        var result = TranslationStatusReportBuilder.Build(all, null, data);
        var spanishRow = result.Languages.Single(l => l.Key == "es");

        Assert.Multiple(() =>
        {
            Assert.That(spanishRow.Total, Is.EqualTo(15));
            Assert.That(spanishRow.Covered, Is.EqualTo(13));
            Assert.That(spanishRow.Flagged, Is.EqualTo(2));
            Assert.That(spanishRow.Missing, Is.EqualTo(2));
            Assert.That(spanishRow.SecondaryLabel, Is.EqualTo("es"));
            Assert.That(result.TranslatedCount, Is.EqualTo(14));
            Assert.That(result.OutdatedCount, Is.EqualTo(2));
            Assert.That(result.MissingCount, Is.EqualTo(16));
        });
    }

    [Test]
    public void Build_ItemWithoutDefaultVariant_IsTranslatedButNotOutdated()
    {
        // The SQL counts the variant as translated and never as outdated (no default variant to compare with).
        var data = Data([new(2, 10, "Article", "Article", 1, 1, 0)]);

        var result = TranslationStatusReportBuilder.Build(all, 2, data);

        Assert.Multiple(() =>
        {
            Assert.That(result.TranslatedCount, Is.EqualTo(1));
            Assert.That(result.OutdatedCount, Is.Zero);
            Assert.That(result.MissingCount, Is.Zero);
            Assert.That(result.ByContentType.Items, Is.Empty);
        });
    }

    [Test]
    public void Build_LanguageFilter_KeepsOnlyThatLanguage()
    {
        var data = Data(
            [new(2, 10, "Article", "Article", 10, 8, 2), new(3, 10, "Article", "Article", 10, 1, 1)],
            [Outdated(1, 2, 5), Outdated(2, 3, 9)]);

        var result = TranslationStatusReportBuilder.Build(all, 3, data);

        Assert.Multiple(() =>
        {
            Assert.That(result.LanguageId, Is.EqualTo(3));
            Assert.That(result.Languages.Select(l => l.Key), Is.EqualTo(new[] { "cs" }));
            Assert.That(result.OutdatedCount, Is.EqualTo(1));
            Assert.That(result.MissingCount, Is.EqualTo(9));
            Assert.That(result.Outdated.Select(i => i.Key), Is.EqualTo(new[] { "2" }));
            Assert.That(result.ByContentType.Items.Single().Value, Is.EqualTo(1m));
            Assert.That(result.ByContentType.Items.Single().SecondaryValue, Is.EqualTo(9m));
            Assert.That(result.LanguageOptions, Has.Count.EqualTo(2));
        });
    }

    [TestCase(1)]
    [TestCase(99)]
    [TestCase(0)]
    [TestCase(-1)]
    public void Build_DefaultOrUnknownLanguage_MeansAll(int languageId)
    {
        var result = TranslationStatusReportBuilder.Build(all, languageId, Data());

        Assert.Multiple(() =>
        {
            Assert.That(result.LanguageId, Is.Null);
            Assert.That(result.Languages, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void Build_PassesKindAndChannelThrough()
    {
        var result = TranslationStatusReportBuilder.Build(new("Website", 4), null, Data());

        Assert.Multiple(() =>
        {
            Assert.That(result.Kind, Is.EqualTo("Website"));
            Assert.That(result.ChannelId, Is.EqualTo(4));
            Assert.That(result.ByContentType.ChannelId, Is.EqualTo(4));
        });
    }

    [Test]
    public void Build_Outdated_MostDaysBehindFirst_WithDatesUserAndLinks()
    {
        var data = Data(outdated:
        [
            Outdated(1, 2, 3.5),
            Outdated(2, 3, 40, userId: null, userName: null) with { Link = new(ContentItemLocation.WebPage, 1, 42, "cs") },
            Outdated(3, 2, 10, userName: null),
        ]);

        var result = TranslationStatusReportBuilder.Build(all, null, data, link => $"/item/{link.ObjectId}");
        var first = result.Outdated[0];

        Assert.Multiple(() =>
        {
            Assert.That(result.Outdated.Select(i => i.Days), Is.EqualTo(new[] { 40, 10, 3 }));
            Assert.That(result.Outdated.Select(i => i.Detail), Is.EqualTo(new[] { StatsUserLabels.UnknownUserLabel, "User 53", "Admin" }));
            Assert.That(first.Since, Is.EqualTo(DateOnly.FromDateTime(defaultModified.AddDays(-40))));
            Assert.That(first.Until, Is.EqualTo(DateOnly.FromDateTime(defaultModified)));
            Assert.That(first.Language, Is.EqualTo("Czech"));
            Assert.That(first.AdminPath, Is.EqualTo("/item/42"));
            Assert.That(result.Outdated[1].AdminPath, Is.Null);
        });
    }

    [Test]
    public void Build_Outdated_IsLimited()
    {
        var rows = Enumerable.Range(1, TranslationStatusReportBuilder.ListLimit + 5).Select(i => Outdated(i, 2, i)).ToList();

        var result = TranslationStatusReportBuilder.Build(all, null, Data(outdated: rows));

        Assert.Multiple(() =>
        {
            Assert.That(result.Outdated, Has.Count.EqualTo(TranslationStatusReportBuilder.ListLimit));
            Assert.That(result.Outdated[0].Days, Is.EqualTo(TranslationStatusReportBuilder.ListLimit + 5));
        });
    }

    [Test]
    public void Build_ByContentType_MostOutdatedThenMissing_LeavesOutFullyTranslatedTypes()
    {
        var data = Data(
        [
            new(2, 10, "A.Article", "Article", 10, 10, 1),
            new(3, 10, "A.Article", "Article", 10, 10, 0),
            new(2, 11, "A.Page", "", 5, 2, 0),
            new(3, 11, "A.Page", "", 5, 5, 0),
            new(2, 12, "A.Done", "Done", 3, 3, 0),
            new(2, 13, "A.Event", "Event", 4, 4, 3),
        ]);

        var result = TranslationStatusReportBuilder.Build(all, null, data, getContentTypePath: id => $"/types/{id}");

        Assert.Multiple(() =>
        {
            Assert.That(result.ByContentType.Items.Select(i => i.Label), Is.EqualTo(new[] { "Event", "Article", "A.Page" }));
            Assert.That(result.ByContentType.Items.Select(i => i.Value), Is.EqualTo(new[] { 3m, 1m, 0m }));
            Assert.That(result.ByContentType.Items.Select(i => i.SecondaryValue), Is.EqualTo(new decimal?[] { 0, 0, 3 }));
            Assert.That(result.ByContentType.Items.Select(i => i.AdminPath), Is.EqualTo(new[] { "/types/13", "/types/10", "/types/11" }));
        });
    }

    [Test]
    public void Build_Languages_SerializeFlagged()
    {
        var result = TranslationStatusReportBuilder.Build(all, 2, Data([new(2, 10, "Article", "Article", 4, 3, 1)]));

        string json = JsonSerializer.Serialize(result.Languages[0], new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.That(json, Does.Contain("\"flagged\":1"));
    }
}
