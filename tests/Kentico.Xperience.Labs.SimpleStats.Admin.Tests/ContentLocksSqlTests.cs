using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContentLocks;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class ContentLocksSqlTests
{
    private const string LockedCondition = "M.[ContentItemLanguageMetadataLockedByUserID] IS NOT NULL";

    private const string LockedWhen =
        "COALESCE(M.[ContentItemLanguageMetadataLockedWhen], M.[ContentItemLanguageMetadataModifiedWhen])";

    [Test]
    public void Build_ReturnsTwoQueries_WithoutPlaceholders()
    {
        string sql = ContentLocksSql.Build(hasKind: true, hasChannel: true);

        Assert.Multiple(() =>
        {
            Assert.That(Statements(sql), Has.Length.EqualTo(2));
            Assert.That(sql, Does.Not.Contain("{").And.Not.Contain("}"));
        });
    }

    [Test]
    public void Build_NoFilters_UsesNoKindOrChannelParameter()
    {
        string sql = ContentLocksSql.Build(hasKind: false, hasChannel: false);

        Assert.Multiple(() =>
        {
            Assert.That(Count(sql, "C.[ClassType] = " + StatsContentSql.ClassTypeParameter), Is.EqualTo(2));
            Assert.That(sql, Does.Not.Contain(StatsContentSql.KindParameter));
            Assert.That(sql, Does.Not.Contain(StatsContentSql.ChannelParameter));
        });
    }

    [Test]
    public void Build_KindAndChannel_FilterBothQueries()
    {
        string sql = ContentLocksSql.Build(hasKind: true, hasChannel: true);

        Assert.Multiple(() =>
        {
            Assert.That(Count(sql, "C.[ClassContentTypeType] = " + StatsContentSql.KindParameter), Is.EqualTo(2));
            Assert.That(Count(sql, "I.[ContentItemChannelID] = " + StatsContentSql.ChannelParameter), Is.EqualTo(2));
        });
    }

    [Test]
    public void Build_ReadsOnlyLockedVariants_AndKeepsLocksOfDeletedUsers()
    {
        string sql = ContentLocksSql.Build(hasKind: false, hasChannel: false);

        Assert.Multiple(() =>
        {
            Assert.That(Count(sql, "AND " + LockedCondition), Is.EqualTo(2));
            Assert.That(Count(sql, "LEFT JOIN [CMS_User] U ON U.[UserID] = M.[ContentItemLanguageMetadataLockedByUserID]"), Is.EqualTo(2));
            Assert.That(sql, Does.Not.Contain("INNER JOIN [CMS_User]"));
            Assert.That(sql, Does.Contain("GROUP BY U.[UserID]"));
        });
    }

    [Test]
    public void Build_List_IsOldestFirstAndLimited_WithOldLockCountBeforeTop()
    {
        string sql = ContentLocksSql.Build(hasKind: false, hasChannel: false);

        Assert.Multiple(() =>
        {
            Assert.That(Count(sql, "TOP (" + ContentLocksSql.LimitParameter + ")"), Is.EqualTo(1));
            Assert.That(sql, Does.Contain($"ORDER BY {LockedWhen}, M.[ContentItemLanguageMetadataID]"));
            Assert.That(sql, Does.Contain($"SUM(CASE WHEN {LockedWhen} < {ContentLocksSql.OldBeforeParameter} THEN 1 ELSE 0 END) OVER () AS [OldLockCount]"));
            Assert.That(sql, Does.Contain($"MIN({LockedWhen}) AS [OldestLockedWhen]"));
        });
    }

    [Test]
    public void Build_List_HasLinkChannelAndUserColumns()
    {
        string sql = ContentLocksSql.Build(hasKind: false, hasChannel: false);

        Assert.Multiple(() =>
        {
            Assert.That(Count(sql, "AS [WorkspaceID]"), Is.EqualTo(1));
            Assert.That(Count(sql, "[CMS_WebPageItem] W1"), Is.EqualTo(1));
            Assert.That(Count(sql, "[CMS_HeadlessItem] H1"), Is.EqualTo(1));
            Assert.That(Count(sql, "I.[ContentItemIsReusable] AS [IsReusable]"), Is.EqualTo(1));
            Assert.That(Count(sql, "LEFT JOIN [CMS_Workspace] WS ON WS.[WorkspaceID] = I.[ContentItemWorkspaceID]"), Is.EqualTo(1));
            Assert.That(Count(sql, StatsContentSql.UserDisplayName + " AS [UserName]"), Is.EqualTo(2));
            Assert.That(sql, Does.Contain("M.[ContentItemLanguageMetadataModifiedWhen] AS [ModifiedWhen]"));
        });
    }

    private static string[] Statements(string sql) =>
        sql.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static int Count(string text, string value) =>
        (text.Length - text.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;
}
