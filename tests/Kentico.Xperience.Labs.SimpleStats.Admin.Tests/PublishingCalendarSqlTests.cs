using CMS.EmailMarketing;

using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PublishingCalendar;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class PublishingCalendarSqlTests
{
    // Upcoming and recent inserts into @Events.
    private const int FilteredStatements = 2;


    [Test]
    public void Build_WithoutSends_ReturnsEventsInsertsAndFourQueries_WithoutPlaceholders()
    {
        string sql = PublishingCalendarSql.Build(hasKind: true, hasChannel: true, withSends: false);

        Assert.That(Statements(sql), Has.Length.EqualTo(8)); // SET NOCOUNT ON, @Events, 2 inserts, 4 queries.
        Assert.That(sql, Does.Not.Contain("{").And.Not.Contain("}"));
        Assert.That(sql, Does.Not.Contain("EmailLibrary_SendConfiguration"));
        Assert.That(sql, Does.Not.Contain(PublishingCalendarSql.RegularPurposeParameter));
    }

    [Test]
    public void Build_NoFilters_UsesNoKindOrChannelParameter()
    {
        string sql = PublishingCalendarSql.Build(hasKind: false, hasChannel: false, withSends: false);

        Assert.That(Count(sql, "C.[ClassType] = " + StatsContentSql.ClassTypeParameter), Is.EqualTo(FilteredStatements));
        Assert.That(sql, Does.Not.Contain(StatsContentSql.KindParameter));
        Assert.That(sql, Does.Not.Contain(StatsContentSql.ChannelParameter));
    }

    [Test]
    public void Build_KindAndChannel_FilterEveryStatement_IncludingSends()
    {
        string sql = PublishingCalendarSql.Build(hasKind: true, hasChannel: true, withSends: true);

        Assert.That(Count(sql, "C.[ClassContentTypeType] = " + StatsContentSql.KindParameter), Is.EqualTo(FilteredStatements + 1));
        Assert.That(Count(sql, "I.[ContentItemChannelID] = " + StatsContentSql.ChannelParameter), Is.EqualTo(FilteredStatements + 1));
    }

    [Test]
    public void Build_ListsAreLimitedAndHaveLinkColumns()
    {
        string sql = PublishingCalendarSql.Build(hasKind: false, hasChannel: false, withSends: true);

        // Content: upcoming, recent. Sends: upcoming.
        Assert.That(Count(sql, "TOP (" + PublishingCalendarSql.LimitParameter + ")"), Is.EqualTo(3));
        Assert.That(Count(sql, "AS [WorkspaceID]"), Is.EqualTo(3));
        Assert.That(Count(sql, "[CMS_WebPageItem] W1"), Is.EqualTo(2));
    }

    [Test]
    public void Build_Sends_AreScheduledRegularEmails_AfterTheAvailabilityCheck()
    {
        string sql = PublishingCalendarSql.Build(hasKind: false, hasChannel: false, withSends: true);

        int check = sql.IndexOf("AS [" + PublishingCalendarSql.SendsAvailableColumn + "]", StringComparison.Ordinal);
        int sends = sql.IndexOf("FROM [EmailLibrary_SendConfiguration] SC", StringComparison.Ordinal);
        int recent = sql.IndexOf("D.[ContentItemCommonDataLastPublishedWhen] DESC", StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(check, Is.GreaterThan(recent), "Content results come first, so missing email tables do not stop them.");
            Assert.That(sends, Is.GreaterThan(check));
            Assert.That(sql, Does.Contain("OBJECT_ID(N'[EmailLibrary_SendConfiguration]', N'U') IS NULL"));
            Assert.That(sql, Does.Contain($"SC.[SendConfigurationStatus] = {(int)SendConfigurationStatus.Scheduled}"));
            Assert.That(sql, Does.Contain("E.[EmailConfigurationPurpose] = " + PublishingCalendarSql.RegularPurposeParameter));
            Assert.That(sql, Does.Contain($"{(int)PublishingAction.Send} AS [Action]"));
            // Only sends in the window are read; counts, days and the list use all of them.
            Assert.That(sql, Does.Contain("SC.[SendConfigurationScheduledTime] >= " + PublishingCalendarSql.UpcomingFromParameter));
            Assert.That(sql, Does.Contain("SC.[SendConfigurationScheduledTime] <= " + PublishingCalendarSql.UpcomingToParameter));
            Assert.That(sql, Does.Not.Contain("SC.[SendConfigurationScheduledTime] <" + " @"));
            Assert.That(sql, Does.Not.Contain("S.[When] <"));
        });
    }

    [TestCase(0, PublishingAction.Publish)]
    [TestCase(1, PublishingAction.Unpublish)]
    [TestCase(2, PublishingAction.Send)]
    public void ToAction_MapsSqlValues(int value, PublishingAction expected) =>
        Assert.That(PublishingCalendarRepository.ToAction(value), Is.EqualTo(expected));

    [Test]
    public void ToAction_UnknownValue_Throws() =>
        Assert.That(() => PublishingCalendarRepository.ToAction(9), Throws.TypeOf<ArgumentOutOfRangeException>());

    [Test]
    public void Build_Lists_ReadTheWorkspaceOfReusableItems()
    {
        string sql = PublishingCalendarSql.Build(hasKind: false, hasChannel: false, withSends: true);

        Assert.Multiple(() =>
        {
            // Content lists: upcoming, recent.
            Assert.That(Count(sql, "LEFT JOIN [CMS_Workspace] WS ON WS.[WorkspaceID] = I.[ContentItemWorkspaceID]"), Is.EqualTo(2));
            Assert.That(Count(sql, "I.[ContentItemIsReusable] AS [IsReusable]"), Is.EqualTo(2));
            Assert.That(Count(sql, "WS.[WorkspaceDisplayName]"), Is.EqualTo(2));
            // Sends are emails: never reusable.
            Assert.That(Count(sql, "CAST(0 AS bit) AS [IsReusable]"), Is.EqualTo(1));
            Assert.That(Count(sql, "CAST(NULL AS nvarchar(200)) AS [WorkspaceDisplayName]"), Is.EqualTo(1));
        });
    }

    private static string[] Statements(string sql) =>
        sql.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static int Count(string text, string value) =>
        (text.Length - text.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;
}
