using CMS.EmailMarketing;

using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PublishingCalendar;

/// <summary>
/// Builds the publishing calendar batch: counts, events per day and two lists in one round trip (see <see cref="Build"/>),
/// then optionally the scheduled sends of regular emails. Only constant SQL fragments are combined (product enum values
/// included as numbers); all values are parameters.
/// </summary>
/// <remarks>
/// <para>
/// Items, variants, the kind and channel filter and the link columns come from <see cref="StatsContentSql"/>.
/// A content event is a scheduled publish (<c>ContentItemLanguageMetadataScheduledPublishWhen</c>) or unpublish
/// (<c>...ScheduledUnpublishWhen</c>) of a language variant, so a variant with both gives two events.
/// </para>
/// <para>
/// A send event is a send configuration (<c>CMS.EmailMarketing.SendConfigurationInfo</c>, <c>EmailLibrary_SendConfiguration</c>) with
/// status <see cref="SendConfigurationStatus.Scheduled"/> of a regular email (<c>EmailLibrary_EmailConfiguration</c>), at
/// <c>SendConfigurationScheduledTime</c>. The email's name, language and channel are read like the email summary report: the language
/// variant in the email channel's primary language first, else the one with the lowest metadata ID. Sends follow the same kind and
/// channel filter through the email's content item. The send part starts with one row (<see cref="SendsAvailableColumn"/>):
/// <c>0</c> when an email table does not exist, and then returns nothing else.
/// </para>
/// <para>Times are compared as stored (server local time).</para>
/// </remarks>
internal static class PublishingCalendarSql
{
    /// <summary>Upcoming events are scheduled on or after this time (now).</summary>
    public const string UpcomingFromParameter = "@UpcomingFrom";

    /// <summary>Upcoming events are scheduled on or before this time (now + window).</summary>
    public const string UpcomingToParameter = "@UpcomingTo";

    /// <summary>Variants last published on or after this time count as recently published.</summary>
    public const string RecentFromParameter = "@RecentFrom";

    /// <summary>Maximum number of rows of each list.</summary>
    public const string LimitParameter = "@Limit";

    /// <summary>Purpose of regular emails (<c>EmailPurpose.Regular</c> as stored). Only with sends.</summary>
    public const string RegularPurposeParameter = "@RegularPurpose";

    public const string SendsAvailableColumn = "SendsAvailable";

    private const int PublishAction = (int)PublishingAction.Publish;
    private const int UnpublishAction = (int)PublishingAction.Unpublish;
    private const int SendAction = (int)PublishingAction.Send;
    private const int ScheduledStatus = (int)SendConfigurationStatus.Scheduled;

    // One row per scheduled publish and unpublish of a variant ([Action] is a PublishingAction). Not set: When is NULL,
    // which no time condition matches. EV, because E is the email link join of StatsContentSql.LinkApply.
    private static readonly string eventsApply = $"""
        CROSS APPLY (VALUES
            ({PublishAction}, M.[ContentItemLanguageMetadataScheduledPublishWhen]),
            ({UnpublishAction}, M.[ContentItemLanguageMetadataScheduledUnpublishWhen])
        ) EV([Action], [When])
        """;

    private const string UpcomingCondition = """
        AND EV.[When] >= @UpcomingFrom
            AND EV.[When] <= @UpcomingTo
        """;

    // The latest version of the variant carries the last publish time (a newer draft keeps it).
    private const string RecentJoin = """
        INNER JOIN [CMS_ContentItemCommonData] D
            ON D.[ContentItemCommonDataContentItemID] = M.[ContentItemLanguageMetadataContentItemID]
            AND D.[ContentItemCommonDataContentLanguageID] = M.[ContentItemLanguageMetadataContentLanguageID]
            AND D.[ContentItemCommonDataIsLatest] = 1
        """;

    private const string RecentCondition = """
        AND D.[ContentItemCommonDataLastPublishedWhen] >= @RecentFrom
        """;

    // Name of the user who last modified the variant (U).
    private const string ModifiedByColumn = """
        COALESCE(NULLIF(LTRIM(RTRIM(CONCAT(U.[FirstName], N' ', U.[LastName]))), N''), U.[UserName]) AS [ModifiedBy]
        """;

    // Columns and joins of the content lists besides the event or publish time. The workspace (WS) names the Content hub of reusable items.
    private const string ListColumns = """
        M.[ContentItemLanguageMetadataID] AS [RowID],
                I.[ContentItemID],
                {0}
                L.[ContentLanguageName],
                M.[ContentItemLanguageMetadataDisplayName] AS [DisplayName],
                C.[ClassDisplayName],
                L.[ContentLanguageDisplayName],
                CH.[ChannelDisplayName],
                I.[ContentItemIsReusable] AS [IsReusable],
                WS.[WorkspaceDisplayName],
                {1}
        """;

    private const string ListJoins = """
        INNER JOIN [CMS_ContentLanguage] L ON L.[ContentLanguageID] = M.[ContentItemLanguageMetadataContentLanguageID]
                LEFT JOIN [CMS_Channel] CH ON CH.[ChannelID] = I.[ContentItemChannelID]
                LEFT JOIN [CMS_User] U ON U.[UserID] = M.[ContentItemLanguageMetadataModifiedByUserID]
                LEFT JOIN [CMS_Workspace] WS ON WS.[WorkspaceID] = I.[ContentItemWorkspaceID]
                {0}
        """;

    // 1. Counts (one row), not limited by @Limit.
    private const string CountsQuery = """
        SELECT
            (SELECT COUNT(*)
                {0}
                {2}
                {1}
                    {3}
                    AND EV.[Action] = {8}) AS [UpcomingPublish],
            (SELECT COUNT(*)
                {0}
                {2}
                {1}
                    {3}
                    AND EV.[Action] = {9}) AS [UpcomingUnpublish],
            (SELECT COUNT(*)
                {0}
                {4}
                {1}
                    {5}) AS [RecentlyPublished];
        """;

    // 2. Upcoming events per day. Days without events are filled with 0 by the report builder.
    private const string DaysQuery = """
        SELECT
            CAST(EV.[When] AS date) AS [Day],
            SUM(CASE WHEN EV.[Action] = {8} THEN 1 ELSE 0 END) AS [Publish],
            SUM(CASE WHEN EV.[Action] = {9} THEN 1 ELSE 0 END) AS [Unpublish]
        {0}
        {2}
        {1}
            {3}
        GROUP BY CAST(EV.[When] AS date);
        """;

    // 3. Upcoming events, soonest first (publish before unpublish at the same time).
    private const string UpcomingQuery = """
        SELECT TOP (@Limit)
            EV.[When],
            EV.[Action],
            {6}
        {0}
        {2}
        {7}
        {1}
            {3}
        ORDER BY EV.[When], M.[ContentItemLanguageMetadataID], EV.[Action];
        """;

    // 4. Recently published variants, newest first.
    private const string RecentQuery = """
        SELECT TOP (@Limit)
            D.[ContentItemCommonDataLastPublishedWhen] AS [When],
            {6}
        {0}
        {4}
        {7}
        {1}
            {5}
        ORDER BY D.[ContentItemCommonDataLastPublishedWhen] DESC, M.[ContentItemLanguageMetadataID];
        """;

    private static readonly string sendsAvailabilityCheck = StatsSql.BuildAvailabilityCheck(
        SendsAvailableColumn,
        "EmailLibrary_EmailConfiguration",
        "EmailLibrary_EmailChannel",
        "EmailLibrary_SendConfiguration");

    // 5. Scheduled sends of regular emails in the window, with what the lists need. {0} = items WHERE clause.
    private static readonly string sendsTable = $$"""
        DECLARE @Sends TABLE (
            [SendID] int PRIMARY KEY,
            [When] datetime2(7) NOT NULL,
            [EmailID] int NOT NULL,
            [EmailChannelID] int NULL,
            [ContentItemID] int NOT NULL,
            [ChannelID] int NULL,
            [ClassDisplayName] nvarchar(200) NULL,
            [DisplayName] nvarchar(200) NULL,
            [LanguageName] nvarchar(100) NULL,
            [LanguageDisplayName] nvarchar(200) NULL,
            [ModifiedByUserID] int NULL);

        INSERT INTO @Sends
        SELECT
            SC.[SendConfigurationID],
            SC.[SendConfigurationScheduledTime],
            E.[EmailConfigurationID],
            E.[EmailConfigurationEmailChannelID],
            I.[ContentItemID],
            I.[ContentItemChannelID],
            C.[ClassDisplayName],
            LM.[ContentItemLanguageMetadataDisplayName],
            COALESCE(LM.[ContentLanguageName], P.[ContentLanguageName]),
            COALESCE(LM.[ContentLanguageDisplayName], P.[ContentLanguageDisplayName]),
            LM.[ContentItemLanguageMetadataModifiedByUserID]
        FROM [EmailLibrary_SendConfiguration] SC
        INNER JOIN [EmailLibrary_EmailConfiguration] E ON E.[EmailConfigurationID] = SC.[SendConfigurationEmailConfigurationID]
        INNER JOIN [CMS_ContentItem] I ON I.[ContentItemID] = E.[EmailConfigurationContentItemID]
        INNER JOIN [CMS_Class] C ON C.[ClassID] = I.[ContentItemContentTypeID]
        LEFT JOIN [EmailLibrary_EmailChannel] EC ON EC.[EmailChannelID] = E.[EmailConfigurationEmailChannelID]
        LEFT JOIN [CMS_ContentLanguage] P ON P.[ContentLanguageID] = EC.[EmailChannelPrimaryContentLanguageID]
        OUTER APPLY (
            SELECT TOP (1)
                LM1.[ContentItemLanguageMetadataDisplayName],
                LM1.[ContentItemLanguageMetadataModifiedByUserID],
                L1.[ContentLanguageName],
                L1.[ContentLanguageDisplayName]
            FROM [CMS_ContentItemLanguageMetadata] LM1
            INNER JOIN [CMS_ContentLanguage] L1 ON L1.[ContentLanguageID] = LM1.[ContentItemLanguageMetadataContentLanguageID]
            WHERE LM1.[ContentItemLanguageMetadataContentItemID] = I.[ContentItemID]
            ORDER BY
                CASE WHEN LM1.[ContentItemLanguageMetadataContentLanguageID] = EC.[EmailChannelPrimaryContentLanguageID] THEN 0 ELSE 1 END,
                LM1.[ContentItemLanguageMetadataID]
        ) LM
        {0}
            AND SC.[SendConfigurationStatus] = {{ScheduledStatus}}
            AND E.[EmailConfigurationPurpose] = @RegularPurpose
            AND SC.[SendConfigurationScheduledTime] >= @UpcomingFrom
            AND SC.[SendConfigurationScheduledTime] <= @UpcomingTo;
        """;

    // 6. Send counts (one row).
    private const string SendCountsQuery = """
        SELECT COUNT(*) AS [UpcomingSend]
        FROM @Sends S;
        """;

    // 7. Upcoming sends per day.
    private const string SendDaysQuery = """
        SELECT CAST(S.[When] AS date) AS [Day], COUNT(*) AS [Send]
        FROM @Sends S
        GROUP BY CAST(S.[When] AS date);
        """;

    // Send list columns, named like the content lists (link columns: only the email).
    private static readonly string sendListSelect = $"""
        SELECT TOP (@Limit)
            S.[When],
            {SendAction} AS [Action],
            S.[SendID] AS [RowID],
            S.[ContentItemID],
            CAST(NULL AS int) AS [WorkspaceID],
            CAST(NULL AS int) AS [WebsiteChannelID],
            CAST(NULL AS int) AS [WebPageItemID],
            S.[EmailChannelID],
            S.[EmailID] AS [EmailConfigurationID],
            CAST(NULL AS int) AS [HeadlessChannelID],
            CAST(NULL AS int) AS [HeadlessItemID],
            S.[LanguageName] AS [ContentLanguageName],
            COALESCE(S.[DisplayName], N'') AS [DisplayName],
            S.[ClassDisplayName],
            COALESCE(S.[LanguageDisplayName], N'') AS [ContentLanguageDisplayName],
            CH.[ChannelDisplayName],
            CAST(0 AS bit) AS [IsReusable],
            CAST(NULL AS nvarchar(200)) AS [WorkspaceDisplayName],
            {ModifiedByColumn}
        FROM @Sends S
        LEFT JOIN [CMS_Channel] CH ON CH.[ChannelID] = S.[ChannelID]
        LEFT JOIN [CMS_User] U ON U.[UserID] = S.[ModifiedByUserID]
        """;

    // 8. Upcoming sends, soonest first.
    private static readonly string sendUpcomingQuery = sendListSelect + """

        ORDER BY S.[When], S.[SendID];
        """;

    /// <summary>
    /// Returns the batch. Add <see cref="StatsContentSql.KindParameter"/> when <paramref name="hasKind"/>,
    /// <see cref="StatsContentSql.ChannelParameter"/> when <paramref name="hasChannel"/> and <see cref="RegularPurposeParameter"/> when
    /// <paramref name="withSends"/>; all other parameters (<see cref="StatsContentSql.ClassTypeParameter"/> and the ones of this class) are always used.
    /// </summary>
    /// <remarks>
    /// Result sets, in order: counts (one row), upcoming events per day, upcoming events, recently published variants.
    /// With <paramref name="withSends"/> then: <see cref="SendsAvailableColumn"/> (one row; nothing more when it is <c>0</c>), send counts
    /// (one row), upcoming sends per day, upcoming sends.
    /// </remarks>
    /// <param name="hasKind">Filter by content type type.</param>
    /// <param name="hasChannel">Filter by channel.</param>
    /// <param name="withSends">Also read scheduled sends (all kinds or emails).</param>
    public static string Build(bool hasKind, bool hasChannel, bool withSends)
    {
        string itemsWhere = StatsContentSql.ItemsWhere(hasKind, hasChannel);
        object[] args =
        [
            StatsContentSql.VariantsFrom,
            itemsWhere,
            eventsApply,
            UpcomingCondition,
            RecentJoin,
            RecentCondition,
            string.Format(null, ListColumns, StatsContentSql.LinkColumns, ModifiedByColumn),
            string.Format(null, ListJoins, StatsContentSql.LinkApply),
            PublishAction,
            UnpublishAction,
        ];

        var statements = new List<string>
        {
            "SET NOCOUNT ON;",
        };
        statements.AddRange(new[] { CountsQuery, DaysQuery, UpcomingQuery, RecentQuery }.Select(query => string.Format(null, query, args)));

        if (withSends)
        {
            statements.Add(sendsAvailabilityCheck);
            statements.Add(string.Format(null, sendsTable, itemsWhere));
            statements.AddRange([SendCountsQuery, SendDaysQuery, sendUpcomingQuery]);
        }

        return string.Join(Environment.NewLine, statements);
    }
}
