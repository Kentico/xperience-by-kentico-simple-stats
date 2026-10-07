using System.Data.Common;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

/// <summary>
/// SQL fragments shared by the content reports (content inventory, publishing calendar, content locks): the filtered items and language
/// variants, the columns that link an item to where it is edited, its channel label and user names. Only constant fragments; all values are parameters.
/// </summary>
/// <remarks>
/// Items are <c>CMS_ContentItem</c> rows whose class is a content type (<c>ClassType</c> = <see cref="ClassTypeParameter"/>).
/// Items without a content type (page folders in website channels) are not counted.
/// The optional filters are the content type type (<c>CMS_Class.ClassContentTypeType</c>, <see cref="KindParameter"/>) and
/// the item's channel (<c>CMS_ContentItem.ContentItemChannelID</c>, <see cref="ChannelParameter"/>; reusable items have no channel).
/// A language variant is one <c>CMS_ContentItemLanguageMetadata</c> row (unique per item and language).
/// Aliases: <c>M</c> variant, <c>I</c> item, <c>C</c> content type.
/// </remarks>
internal static class StatsContentSql
{
    public const string ClassTypeParameter = "@ClassType";
    public const string KindParameter = "@Kind";
    public const string ChannelParameter = "@ChannelID";

    /// <summary>
    /// Filtered items; {0} = channel condition, {1} = kind condition. Use <see cref="ItemsWhere(bool, bool)"/>.
    /// </summary>
    private const string ItemsWhereFormat = """
        WHERE C.[ClassType] = @ClassType
            AND C.[ClassContentTypeType] IS NOT NULL{1}{0}
        """;

    /// <summary>
    /// Language variants (<c>M</c>) with their item (<c>I</c>) and content type (<c>C</c>). Filter with <see cref="ItemsWhere(bool, bool)"/>.
    /// </summary>
    public const string VariantsFrom = """
        FROM [CMS_ContentItemLanguageMetadata] M
        INNER JOIN [CMS_ContentItem] I ON I.[ContentItemID] = M.[ContentItemLanguageMetadataContentItemID]
        INNER JOIN [CMS_Class] C ON C.[ClassID] = I.[ContentItemContentTypeID]
        """;

    /// <summary>
    /// Where each listed item is edited: the workspace of reusable items, or the page, email or headless item
    /// of the item and its channel (see <see cref="StatsChannelItemPaths"/>). Ends with a comma.
    /// Needs <see cref="LinkApply"/> after the joins; read with <see cref="StatsContentLinkReader"/>
    /// (the select also needs <c>I.[ContentItemID]</c> and the variant's <c>[ContentLanguageName]</c>).
    /// </summary>
    public const string LinkColumns = """
        CASE WHEN I.[ContentItemIsReusable] = 1 THEN I.[ContentItemWorkspaceID] END AS [WorkspaceID],
                    P.[WebPageItemWebsiteChannelID] AS [WebsiteChannelID],
                    P.[WebPageItemID],
                    E.[EmailConfigurationEmailChannelID] AS [EmailChannelID],
                    E.[EmailConfigurationID],
                    H.[HeadlessItemHeadlessChannelID] AS [HeadlessChannelID],
                    H.[HeadlessItemID],
        """;

    /// <summary>
    /// Joins of <see cref="LinkColumns"/>. TOP (1) keeps one row per variant if data is inconsistent.
    /// </summary>
    public const string LinkApply = """
        OUTER APPLY (
                    SELECT TOP (1) W1.[WebPageItemID], W1.[WebPageItemWebsiteChannelID]
                    FROM [CMS_WebPageItem] W1
                    WHERE W1.[WebPageItemContentItemID] = I.[ContentItemID]
                    ORDER BY W1.[WebPageItemID]
                ) P
                OUTER APPLY (
                    SELECT TOP (1) E1.[EmailConfigurationID], E1.[EmailConfigurationEmailChannelID]
                    FROM [EmailLibrary_EmailConfiguration] E1
                    WHERE E1.[EmailConfigurationContentItemID] = I.[ContentItemID]
                    ORDER BY E1.[EmailConfigurationID]
                ) E
                OUTER APPLY (
                    SELECT TOP (1) H1.[HeadlessItemID], H1.[HeadlessItemHeadlessChannelID]
                    FROM [CMS_HeadlessItem] H1
                    WHERE H1.[HeadlessItemContentItemID] = I.[ContentItemID]
                    ORDER BY H1.[HeadlessItemID]
                ) H
        """;

    /// <summary>
    /// What the channel column of a list needs (see <see cref="StatsContentChannels.GetLabel"/>): the item's channel display name
    /// (<c>[ChannelDisplayName]</c>), whether it is reusable (<c>[IsReusable]</c>) and its workspace display name (<c>[WorkspaceDisplayName]</c>).
    /// Ends with a comma. Needs <see cref="ChannelLabelJoins"/>.
    /// </summary>
    public const string ChannelLabelColumns = """
        CH.[ChannelDisplayName],
                I.[ContentItemIsReusable] AS [IsReusable],
                WS.[WorkspaceDisplayName],
        """;

    /// <summary>
    /// Joins of <see cref="ChannelLabelColumns"/>: the item's channel (<c>CH</c>) and workspace (<c>WS</c>).
    /// </summary>
    public const string ChannelLabelJoins = """
        LEFT JOIN [CMS_Channel] CH ON CH.[ChannelID] = I.[ContentItemChannelID]
                LEFT JOIN [CMS_Workspace] WS ON WS.[WorkspaceID] = I.[ContentItemWorkspaceID]
        """;

    /// <summary>
    /// Display name of an administration user (<c>CMS_User</c>, alias <c>U</c>): first and last name, or the user name when both are empty.
    /// <c>NULL</c> when there is no such user. An expression without an alias.
    /// </summary>
    public const string UserDisplayName = """
        COALESCE(NULLIF(LTRIM(RTRIM(CONCAT(U.[FirstName], N' ', U.[LastName]))), N''), U.[UserName])
        """;

    /// <summary>
    /// Channel condition (starts with a line break), or empty without a channel filter.
    /// </summary>
    public static string ChannelCondition(bool hasChannel) => hasChannel ? ChannelConditionText : string.Empty;

    /// <summary>
    /// Kind condition (starts with a line break), or empty without a kind filter.
    /// </summary>
    public static string KindCondition(bool hasKind) => hasKind ? KindConditionText : string.Empty;

    /// <summary>
    /// <c>WHERE</c> clause of the filtered items. Add <see cref="KindParameter"/> when <paramref name="hasKind"/>
    /// and <see cref="ChannelParameter"/> when <paramref name="hasChannel"/>; <see cref="ClassTypeParameter"/> is always used.
    /// </summary>
    public static string ItemsWhere(bool hasKind, bool hasChannel) =>
        string.Format(null, ItemsWhereFormat, ChannelCondition(hasChannel), KindCondition(hasKind));

    private const string ChannelConditionText = """

            AND I.[ContentItemChannelID] = @ChannelID
        """;

    private const string KindConditionText = """

            AND C.[ClassContentTypeType] = @Kind
        """;
}

/// <summary>
/// Reads <see cref="StatsContentSql.LinkColumns"/> into a <see cref="ContentItemLink"/>.
/// </summary>
internal sealed record StatsContentLinkReader(
    int Item,
    int Language,
    int Workspace,
    int WebsiteChannel,
    int WebPage,
    int EmailChannel,
    int Email,
    int HeadlessChannel,
    int Headless)
{
    /// <summary>
    /// Column ordinals of the link columns. Without <paramref name="withChannels"/> only the workspace is read (reusable items); the others are -1.
    /// </summary>
    public static StatsContentLinkReader From(DbDataReader reader, bool withChannels) =>
        new(
            reader.GetOrdinal("ContentItemID"),
            reader.GetOrdinal("ContentLanguageName"),
            reader.GetOrdinal("WorkspaceID"),
            withChannels ? reader.GetOrdinal("WebsiteChannelID") : -1,
            withChannels ? reader.GetOrdinal("WebPageItemID") : -1,
            withChannels ? reader.GetOrdinal("EmailChannelID") : -1,
            withChannels ? reader.GetOrdinal("EmailConfigurationID") : -1,
            withChannels ? reader.GetOrdinal("HeadlessChannelID") : -1,
            withChannels ? reader.GetOrdinal("HeadlessItemID") : -1);

    /// <summary>
    /// Returns where the item of the current row is edited: the Content hub for reusable items (they have a workspace), else its page, email
    /// or headless item. <c>null</c> when none is known or the variant has no language.
    /// </summary>
    public ContentItemLink? Read(DbDataReader reader)
    {
        if (reader.IsDBNull(Language))
        {
            return null;
        }

        string language = reader.GetString(Language);

        int? Get(int ordinal) => ordinal < 0 || reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);

        if (Get(Workspace) is int workspaceId)
        {
            return new(ContentItemLocation.ContentHub, workspaceId, reader.GetInt32(Item), language);
        }
        if (Get(WebsiteChannel) is int websiteChannelId && Get(WebPage) is int webPageId)
        {
            return new(ContentItemLocation.WebPage, websiteChannelId, webPageId, language);
        }
        if (Get(EmailChannel) is int emailChannelId && Get(Email) is int emailId)
        {
            return new(ContentItemLocation.Email, emailChannelId, emailId, language);
        }
        if (Get(HeadlessChannel) is int headlessChannelId && Get(Headless) is int headlessId)
        {
            return new(ContentItemLocation.Headless, headlessChannelId, headlessId, language);
        }

        return null;
    }
}
