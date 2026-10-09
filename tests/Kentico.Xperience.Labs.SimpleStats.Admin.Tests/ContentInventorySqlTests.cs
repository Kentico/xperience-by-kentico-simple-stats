using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContentInventory;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class ContentInventorySqlTests
{
    // Content types, languages, statuses, age, oldest, workflow, pending drafts.
    private const int FilteredStatements = 7;

    [Test]
    public void Build_NoFilters_UsesNoKindOrChannelParameter()
    {
        string sql = ContentInventorySql.Build(hasKind: false, hasChannel: false);

        Assert.That(sql, Does.Contain(ContentInventorySql.ClassTypeParameter));
        Assert.That(sql, Does.Not.Contain(ContentInventorySql.KindParameter + Environment.NewLine));
        Assert.That(sql, Does.Not.Contain("= " + ContentInventorySql.KindParameter));
        Assert.That(sql, Does.Not.Contain(ContentInventorySql.ChannelParameter));
        Assert.That(sql, Does.Not.Contain("{").And.Not.Contain("}"));
    }

    [Test]
    public void Build_ReturnsNineStatements()
    {
        string sql = ContentInventorySql.Build(hasKind: true, hasChannel: true);

        Assert.That(sql.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), Has.Length.EqualTo(9));
    }

    [Test]
    public void Build_Kind_FiltersEveryItemStatement()
    {
        string sql = ContentInventorySql.Build(hasKind: true, hasChannel: false);

        Assert.That(Count(sql, "C.[ClassContentTypeType] = " + ContentInventorySql.KindParameter), Is.EqualTo(FilteredStatements));
        Assert.That(sql, Does.Not.Contain(ContentInventorySql.ChannelParameter));
    }

    [Test]
    public void Build_Channel_FiltersEveryItemStatement_AndKeepsContentTypesWithoutItems()
    {
        string sql = ContentInventorySql.Build(hasKind: false, hasChannel: true);

        Assert.That(Count(sql, "I.[ContentItemChannelID] = " + ContentInventorySql.ChannelParameter), Is.EqualTo(FilteredStatements));

        // In the content types statement the channel belongs to the LEFT JOIN (before WHERE), not the WHERE clause.
        string contentTypes = sql[..sql.IndexOf(';')];
        Assert.That(
            contentTypes.IndexOf(ContentInventorySql.ChannelParameter, StringComparison.Ordinal),
            Is.LessThan(contentTypes.IndexOf("WHERE", StringComparison.Ordinal)));
    }

    [Test]
    public void Build_UnusedStatements_AreGatedAndCheckReferences()
    {
        string sql = ContentInventorySql.Build(hasKind: false, hasChannel: false);

        Assert.That(Count(sql, ContentInventorySql.IncludeUnusedParameter + " = 1"), Is.EqualTo(2));
        Assert.That(Count(sql, "[CMS_ContentItemReference]"), Is.EqualTo(2));
        Assert.That(sql, Does.Contain("R.[ContentItemReferenceTargetItemID] = I.[ContentItemID]"));
    }

    [Test]
    public void Build_PendingDrafts_PublishedVersionThatIsNotLatest_PerVariant()
    {
        string drafts = LastStatement(ContentInventorySql.Build(hasKind: false, hasChannel: false));

        // Only variants with a published version that has a newer version: initial drafts (no published version)
        // and published variants without a draft (the published version is the latest) are left out.
        Assert.That(drafts, Does.Contain("CROSS APPLY"));
        Assert.That(drafts, Does.Contain("D1.[ContentItemCommonDataVersionStatus] = " + ContentInventorySql.PublishedStatusParameter));
        Assert.That(drafts, Does.Contain("D1.[ContentItemCommonDataIsLatest] = 0"));

        // Same variant: item and language.
        Assert.That(drafts, Does.Contain("D1.[ContentItemCommonDataContentItemID] = M.[ContentItemLanguageMetadataContentItemID]"));
        Assert.That(drafts, Does.Contain("D1.[ContentItemCommonDataContentLanguageID] = M.[ContentItemLanguageMetadataContentLanguageID]"));

        // Drafts scheduled to publish are planned, not forgotten (they are in the Publishing calendar).
        Assert.That(drafts, Does.Contain("AND M.[ContentItemLanguageMetadataScheduledPublishWhen] IS NULL"));

        // Workflow step is optional (included, not required).
        Assert.That(drafts, Does.Contain("LEFT JOIN [CMS_ContentWorkflowStep]"));
        Assert.That(drafts, Does.Not.Contain("[ContentItemLanguageMetadataContentWorkflowStepID] IS NOT NULL"));
    }

    [Test]
    public void Build_PendingDrafts_CountsBeforeTop_OldestDraftFirst()
    {
        string drafts = LastStatement(ContentInventorySql.Build(hasKind: false, hasChannel: false));

        Assert.That(drafts, Does.StartWith("SELECT TOP (" + ContentInventorySql.LimitParameter + ")"));
        Assert.That(drafts, Does.Contain("COUNT(*) OVER () AS [PendingCount]"));
        Assert.That(drafts, Does.Contain(
            "SUM(CASE WHEN M.[ContentItemLanguageMetadataModifiedWhen] < " + ContentInventorySql.OverdueBeforeParameter + " THEN 1 ELSE 0 END) OVER () AS [OverdueCount]"));
        Assert.That(drafts, Does.Contain("ORDER BY M.[ContentItemLanguageMetadataModifiedWhen], M.[ContentItemLanguageMetadataID]"));
    }

    [Test]
    public void Build_PendingDrafts_KindAndChannelFilter()
    {
        string drafts = LastStatement(ContentInventorySql.Build(hasKind: true, hasChannel: true));

        Assert.That(drafts, Does.Contain("C.[ClassContentTypeType] = " + ContentInventorySql.KindParameter));
        Assert.That(drafts, Does.Contain("I.[ContentItemChannelID] = " + ContentInventorySql.ChannelParameter));
    }

    private static string LastStatement(string sql) =>
        sql.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[^1];

    private static int Count(string text, string value) =>
        (text.Length - text.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;
}
