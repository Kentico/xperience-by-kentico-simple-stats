using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.EditorContributions;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class EditorContributionsSqlTests
{
    [Test]
    public void Build_HasNoPlaceholders_AndThreeResultSets()
    {
        string sql = EditorContributionsSql.Build(hasKind: true, hasChannel: true, withPublished: true);

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Not.Contain("{").And.Not.Contain("}"));
            Assert.That(Count(sql, "SELECT TOP (" + EditorContributionsSql.LimitParameter + ")"), Is.EqualTo(1));
            Assert.That(sql, Does.Contain("[" + EditorContributionsSql.DateColumn + "]"));
            Assert.That(sql, Does.Contain("[" + EditorContributionsSql.CountColumn + "]"));
            Assert.That(sql, Does.Contain("[" + EditorContributionsSql.IsSystemColumn + "]"));
            Assert.That(sql, Does.Contain("COUNT(*) OVER () AS [UserCount]"));
        });
    }

    [Test]
    public void Build_Variants_AreContentTypeItems_FoldersExcluded_WithFilters()
    {
        string sql = EditorContributionsSql.Build(hasKind: true, hasChannel: true, withPublished: false);

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
        string sql = EditorContributionsSql.Build(hasKind: false, hasChannel: false, withPublished: false);

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Not.Contain(StatsContentSql.KindParameter));
            Assert.That(sql, Does.Not.Contain(StatsContentSql.ChannelParameter));
        });
    }

    [Test]
    public void Build_Published_OnlyWithVersionHistory()
    {
        string with = EditorContributionsSql.Build(hasKind: false, hasChannel: false, withPublished: true);
        string without = EditorContributionsSql.Build(hasKind: false, hasChannel: false, withPublished: false);

        Assert.Multiple(() =>
        {
            Assert.That(with, Does.Contain("V.[ContentItemVersionAction] = " + EditorContributionsSql.PublishActionParameter));
            Assert.That(with, Does.Contain("U.[UserID] = V.[ContentItemVersionCreatedByUserID]"));
            Assert.That(without, Does.Not.Contain("CMS_ContentItemVersion"));
            Assert.That(without, Does.Not.Contain(EditorContributionsSql.PublishActionParameter));
        });
    }

    [Test]
    public void Build_CreatedAndModified_ByTheirOwnUser_DeletedUsersKept()
    {
        string sql = EditorContributionsSql.Build(hasKind: false, hasChannel: false, withPublished: false);

        Assert.Multiple(() =>
        {
            // Left joins: a user that no longer exists has no U row, so its UserID is NULL (one "unknown" group).
            Assert.That(sql, Does.Contain("LEFT JOIN [CMS_User] U ON U.[UserID] = X.[CreatedByUserID]"));
            Assert.That(sql, Does.Contain("LEFT JOIN [CMS_User] U ON U.[UserID] = X.[ModifiedByUserID]"));
            Assert.That(sql, Does.Not.Contain("INNER JOIN [CMS_User]"));
            Assert.That(sql, Does.Contain(
                "U.[UserName] IN (" + EditorContributionsSql.ServiceUserNameParameter + ", " + EditorContributionsSql.PublicUserNameParameter + ")"));
        });
    }

    [Test]
    public void Build_RangeEdges_FromInclusive_ToExclusive()
    {
        string sql = EditorContributionsSql.Build(hasKind: false, hasChannel: false, withPublished: true);

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("X.[CreatedWhen] >= @PreviousFrom AND X.[CreatedWhen] < @ToExclusive"));
            Assert.That(sql, Does.Contain("X.[ModifiedWhen] >= @PreviousFrom AND X.[ModifiedWhen] < @ToExclusive"));
            Assert.That(sql, Does.Contain("V.[ContentItemVersionCreatedWhen] < @ToExclusive"));
            Assert.That(sql, Does.Not.Contain("<= @ToExclusive"));
        });
    }

    private static int Count(string text, string value) =>
        (text.Length - text.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;
}
