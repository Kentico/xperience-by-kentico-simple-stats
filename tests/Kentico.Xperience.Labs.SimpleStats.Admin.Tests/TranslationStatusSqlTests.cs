using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.TranslationStatus;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class TranslationStatusSqlTests
{
    [Test]
    public void Build_ReturnsDeclareAndThreeQueries_WithoutPlaceholders()
    {
        string sql = TranslationStatusSql.Build(hasKind: true, hasChannel: true);

        Assert.Multiple(() =>
        {
            Assert.That(Statements(sql), Has.Length.EqualTo(4));
            Assert.That(Statements(sql)[0], Does.StartWith("DECLARE @DefaultLanguageID int"));
            Assert.That(sql, Does.Not.Contain("{").And.Not.Contain("}"));
        });
    }

    [Test]
    public void Build_NoFilters_UsesNoKindOrChannelParameter()
    {
        string sql = TranslationStatusSql.Build(hasKind: false, hasChannel: false);

        Assert.Multiple(() =>
        {
            Assert.That(Count(sql, "C.[ClassType] = " + StatsContentSql.ClassTypeParameter), Is.EqualTo(2));
            Assert.That(sql, Does.Not.Contain(StatsContentSql.KindParameter));
            Assert.That(sql, Does.Not.Contain(StatsContentSql.ChannelParameter));
        });
    }

    [Test]
    public void Build_KindAndChannel_FilterCountsAndList()
    {
        string sql = TranslationStatusSql.Build(hasKind: true, hasChannel: true);

        Assert.Multiple(() =>
        {
            Assert.That(Count(sql, "C.[ClassContentTypeType] = " + StatsContentSql.KindParameter), Is.EqualTo(2));
            Assert.That(Count(sql, "I.[ContentItemChannelID] = " + StatsContentSql.ChannelParameter), Is.EqualTo(2));
        });
    }

    [Test]
    public void Build_Outdated_IsOlderThanTheDefaultVariantByMoreThanTheTolerance()
    {
        string sql = TranslationStatusSql.Build(hasKind: false, hasChannel: false);

        Assert.Multiple(() =>
        {
            // Within the tolerance (for example variants saved together by an import) is up to date.
            Assert.That(
                TranslationStatusSql.OutdatedCondition,
                Is.EqualTo("M.[ContentItemLanguageMetadataModifiedWhen] < DATEADD(minute, -" + TranslationStatusSql.ToleranceParameter + ", D.[ContentItemLanguageMetadataModifiedWhen])"));
            Assert.That(Count(sql, TranslationStatusSql.OutdatedCondition), Is.EqualTo(2));
        });
    }

    [Test]
    public void Build_Counts_PairEveryItemWithEveryNonDefaultLanguage_SoMissingVariantsCount()
    {
        string sql = TranslationStatusSql.Build(hasKind: false, hasChannel: false);
        string counts = Statements(sql)[2];

        Assert.Multiple(() =>
        {
            Assert.That(counts, Does.Contain("FROM [CMS_ContentItem] I"));
            Assert.That(counts, Does.Contain("CROSS JOIN [CMS_ContentLanguage] L"));
            Assert.That(counts, Does.Contain("LEFT JOIN [CMS_ContentItemLanguageMetadata] M"));
            // Items without a default variant are counted (left join) but never outdated (the condition needs D).
            Assert.That(counts, Does.Contain("LEFT JOIN [CMS_ContentItemLanguageMetadata] D"));
            Assert.That(counts, Does.Contain("COUNT(*) AS [ItemCount]"));
            Assert.That(counts, Does.Contain("COUNT(M.[ContentItemLanguageMetadataID]) AS [TranslatedCount]"));
            Assert.That(counts, Does.Contain("L.[ContentLanguageID] <> @DefaultLanguageID"));
            Assert.That(counts, Does.Contain("GROUP BY L.[ContentLanguageID], C.[ClassID]"));
        });
    }

    [Test]
    public void Build_List_NeedsADefaultVariant_IsLimitedPerLanguage_AndMostBehindFirst()
    {
        string sql = TranslationStatusSql.Build(hasKind: false, hasChannel: false);
        string list = Statements(sql)[3];

        Assert.Multiple(() =>
        {
            Assert.That(list, Does.Contain("INNER JOIN [CMS_ContentItemLanguageMetadata] D"));
            Assert.That(list, Does.Contain("PARTITION BY L.[ContentLanguageID]"));
            Assert.That(list, Does.Contain("WHERE X.[RowNumber] <= " + TranslationStatusSql.LimitParameter));
            Assert.That(list, Does.Contain("ORDER BY DATEDIFF(minute, X.[ModifiedWhen], X.[DefaultModifiedWhen]) DESC, X.[VariantID]"));
        });
    }

    [Test]
    public void Build_List_HasLinkChannelAndLastModifierColumns()
    {
        string list = Statements(TranslationStatusSql.Build(hasKind: false, hasChannel: false))[3];

        Assert.Multiple(() =>
        {
            Assert.That(list, Does.Contain("AS [WorkspaceID]"));
            Assert.That(list, Does.Contain("[CMS_WebPageItem] W1"));
            Assert.That(list, Does.Contain("I.[ContentItemIsReusable] AS [IsReusable]"));
            Assert.That(list, Does.Contain("LEFT JOIN [CMS_User] U ON U.[UserID] = M.[ContentItemLanguageMetadataModifiedByUserID]"));
            Assert.That(list, Does.Contain(StatsContentSql.UserDisplayName + " AS [UserName]"));
            Assert.That(list, Does.Contain("D.[ContentItemLanguageMetadataModifiedWhen] AS [DefaultModifiedWhen]"));
        });
    }

    private static string[] Statements(string sql) =>
        sql.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static int Count(string text, string value) =>
        (text.Length - text.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;
}
