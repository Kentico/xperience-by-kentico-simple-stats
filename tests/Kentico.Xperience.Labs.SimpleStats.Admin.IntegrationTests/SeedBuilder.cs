using System.Globalization;
using System.Text;

using CMS.DataEngine;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.IntegrationTests;

/// <summary>
/// Builds one SQL batch that inserts fixed rows into the real Xperience tables. Only the columns the reports read are set;
/// the other required columns get unique values (GUIDs, code names) or keep their defaults. IDs are explicit (<c>IDENTITY_INSERT</c>),
/// so tests can name them. Dates are fixed values, never the current time.
/// </summary>
/// <remarks>
/// <see cref="IntegrationDatabase.Clean"/> removes the rows again: all rows of the data tables, and rows with an ID from
/// <see cref="FirstSharedId"/> in the tables a new database already fills (classes, languages, workspaces).
/// </remarks>
internal sealed class SeedBuilder
{
    /// <summary>
    /// First ID of seeded classes, languages and workspaces (a new database has its own rows there).
    /// </summary>
    public const int FirstSharedId = 900_000;

    // Insert order (parents first).
    private static readonly string[] tables =
    [
        "CMS_ContentLanguage", "CMS_Workspace", "CMS_ContentFolder", "CMS_Channel", "CMS_WebsiteChannel", "CMS_Class", "CMS_Form",
        "CMS_ContentItem", "CMS_ContentItemLanguageMetadata", "CMS_ContentItemCommonData", "CMS_ContentItemVersion",
        "CMS_ContentItemReference", "CMS_Taxonomy", "CMS_Tag", "CMS_ContentItemTag", "CMS_WebPageItem",
        "OM_Contact", "OM_Activity",
    ];

    private readonly Dictionary<string, (string Columns, List<string> Rows)> inserts = [];

    public SeedBuilder Language(int id, string name, string displayName) =>
        Add("CMS_ContentLanguage", "[ContentLanguageID], [ContentLanguageName], [ContentLanguageDisplayName], [ContentLanguageCultureFormat], [ContentLanguageGUID]",
            id, name, displayName, "en-US", newId);

    /// <summary>
    /// A workspace with its root content folder (same ID). Reusable items are put in that folder.
    /// </summary>
    public SeedBuilder Workspace(int id, string displayName) =>
        Add("CMS_Workspace", "[WorkspaceID], [WorkspaceName], [WorkspaceDisplayName], [WorkspaceGUID]", id, "it-workspace-" + Text(id), displayName, newId)
        .Add("CMS_ContentFolder", "[ContentFolderID], [ContentFolderName], [ContentFolderDisplayName], [ContentFolderTreePath], [ContentFolderWorkspaceID], [ContentFolderGUID]",
            id, "it-root-" + Text(id), "Root", "/", id, newId);

    /// <summary>
    /// A website channel (language prefixes): <c>CMS_Channel</c> and <c>CMS_WebsiteChannel</c> rows with the same ID.
    /// </summary>
    public SeedBuilder WebsiteChannel(int id, string displayName, int primaryLanguageId) =>
        Add("CMS_Channel", "[ChannelID], [ChannelName], [ChannelDisplayName], [ChannelType], [ChannelGUID]", id, "it-channel-" + Text(id), displayName, "Website", newId)
        .Add("CMS_WebsiteChannel", "[WebsiteChannelID], [WebsiteChannelChannelID], [WebsiteChannelPrimaryContentLanguageID], [WebsiteChannelDomain], [WebsiteChannelGUID]",
            id, id, primaryLanguageId, "it-" + Text(id) + ".test", newId);

    /// <summary>
    /// A class. <paramref name="contentTypeType"/> is <c>ClassContentTypeType</c> (<c>null</c>: not a content type type, like a page folder).
    /// </summary>
    public SeedBuilder Class(int id, string displayName, string classType, string? contentTypeType) =>
        Add("CMS_Class", "[ClassID], [ClassDisplayName], [ClassName], [ClassType], [ClassContentTypeType], [ClassXmlSchema], [ClassFormDefinition], [ClassLastModified], [ClassGUID]",
            id, displayName, "IT.Class" + Text(id), classType, contentTypeType, string.Empty, string.Empty, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), newId);

    public SeedBuilder ContentType(int id, string displayName, string? contentTypeType) =>
        Class(id, displayName, ClassType.CONTENT_TYPE, contentTypeType);

    public SeedBuilder Form(int id, string displayName, int classId) =>
        Add("CMS_Form", "[FormID], [FormDisplayName], [FormName], [FormClassID], [FormGUID]", id, displayName, "it_form_" + Text(id), classId, newId);

    public SeedBuilder Item(int id, int contentTypeId, int? channelId = null, int? workspaceId = null) =>
        Add("CMS_ContentItem",
            "[ContentItemID], [ContentItemName], [ContentItemGUID], [ContentItemIsReusable], [ContentItemContentTypeID], [ContentItemChannelID], " +
            "[ContentItemWorkspaceID], [ContentItemContentFolderID]",
            id, "it-item-" + Text(id), newId, workspaceId is not null, contentTypeId, channelId, workspaceId, workspaceId);

    public SeedBuilder Variant(
        int id,
        int itemId,
        int languageId,
        string displayName,
        DateTime created,
        DateTime? scheduledPublish = null,
        DateTime? scheduledUnpublish = null,
        int? modifiedByUserId = null) =>
        Add("CMS_ContentItemLanguageMetadata",
            "[ContentItemLanguageMetadataID], [ContentItemLanguageMetadataContentItemID], [ContentItemLanguageMetadataContentLanguageID], " +
            "[ContentItemLanguageMetadataDisplayName], [ContentItemLanguageMetadataCreatedWhen], [ContentItemLanguageMetadataModifiedWhen], " +
            "[ContentItemLanguageMetadataScheduledPublishWhen], [ContentItemLanguageMetadataScheduledUnpublishWhen], " +
            "[ContentItemLanguageMetadataModifiedByUserID], [ContentItemLanguageMetadataGUID]",
            id, itemId, languageId, displayName, created, created, scheduledPublish, scheduledUnpublish, modifiedByUserId, newId);

    public SeedBuilder CommonData(int id, int itemId, int languageId, int versionStatus, bool isLatest, DateTime? firstPublished = null, DateTime? lastPublished = null) =>
        Add("CMS_ContentItemCommonData",
            "[ContentItemCommonDataID], [ContentItemCommonDataContentItemID], [ContentItemCommonDataContentLanguageID], [ContentItemCommonDataVersionStatus], " +
            "[ContentItemCommonDataIsLatest], [ContentItemCommonDataFirstPublishedWhen], [ContentItemCommonDataLastPublishedWhen]",
            id, itemId, languageId, versionStatus, isLatest, firstPublished, lastPublished);

    public SeedBuilder Version(int id, int itemId, int languageId, int action, DateTime created) =>
        Add("CMS_ContentItemVersion",
            "[ContentItemVersionID], [ContentItemVersionContentItemID], [ContentItemVersionContentLanguageID], [ContentItemVersionAction], [ContentItemVersionCreatedWhen]",
            id, itemId, languageId, action, created);

    public SeedBuilder Reference(int id, int sourceCommonDataId, int targetItemId) =>
        Add("CMS_ContentItemReference", "[ContentItemReferenceID], [ContentItemReferenceSourceCommonDataID], [ContentItemReferenceTargetItemID]",
            id, sourceCommonDataId, targetItemId);

    public SeedBuilder Taxonomy(int id, string title) =>
        Add("CMS_Taxonomy", "[TaxonomyID], [TaxonomyName], [TaxonomyTitle], [TaxonomyGUID]", id, "it-taxonomy-" + Text(id), title, newId);

    public SeedBuilder Tag(int id, int taxonomyId, Guid guid, string title) =>
        Add("CMS_Tag", "[TagID], [TagTaxonomyID], [TagGUID], [TagName], [TagTitle]", id, taxonomyId, guid, "it-tag-" + Text(id), title);

    public SeedBuilder ItemTag(int id, int variantId, Guid tagGuid) =>
        Add("CMS_ContentItemTag", "[ContentItemTagID], [ContentItemTagContentItemLanguageMetadataID], [ContentItemTagTagGUID]", id, variantId, tagGuid);

    public SeedBuilder WebPage(int id, Guid guid, int websiteChannelId, int itemId) =>
        Add("CMS_WebPageItem", "[WebPageItemID], [WebPageItemGUID], [WebPageItemWebsiteChannelID], [WebPageItemContentItemID], [WebPageItemName], [WebPageItemTreePath]",
            id, guid, websiteChannelId, itemId, "it-page-" + Text(id), "/it-page-" + Text(id));

    public SeedBuilder Contact(int id, DateTime created) =>
        Add("OM_Contact", "[ContactID], [ContactCreated], [ContactLastModified], [ContactGUID]", id, created, created, newId);

    public SeedBuilder Activity(
        int id,
        int contactId,
        DateTime created,
        string type,
        int? channelId = null,
        Guid? page = null,
        int? languageId = null,
        string? url = null,
        string? utmSource = null,
        string? utmContent = null,
        int? itemId = null) =>
        Add("OM_Activity",
            "[ActivityID], [ActivityContactID], [ActivityCreated], [ActivityType], [ActivityChannelID], [ActivityWebPageItemGUID], [ActivityLanguageID], " +
            "[ActivityURL], [ActivityUTMSource], [ActivityUTMContent], [ActivityItemID]",
            id, contactId, created, type, channelId, page, languageId, url, utmSource, utmContent, itemId);

    /// <summary>
    /// Returns the batch.
    /// </summary>
    public string Build()
    {
        var sql = new StringBuilder("SET NOCOUNT ON;\n");
        foreach (string table in tables)
        {
            if (!inserts.TryGetValue(table, out var insert))
            {
                continue;
            }

            sql.Append(CultureInfo.InvariantCulture, $"SET IDENTITY_INSERT [{table}] ON;\n");
            sql.Append(CultureInfo.InvariantCulture, $"INSERT INTO [{table}] ({insert.Columns}) VALUES\n    ");
            sql.AppendJoin(",\n    ", insert.Rows);
            sql.Append(CultureInfo.InvariantCulture, $";\nSET IDENTITY_INSERT [{table}] OFF;\n");
        }

        return sql.ToString();
    }

    private static readonly object newId = new();

    private SeedBuilder Add(string table, string columns, params object?[] values)
    {
        if (!inserts.TryGetValue(table, out var insert))
        {
            insert = (columns, []);
            inserts[table] = insert;
        }

        insert.Rows.Add("(" + string.Join(", ", values.Select(Literal)) + ")");

        return this;
    }

    private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Literal(object? value) => value switch
    {
        null => "NULL",
        _ when ReferenceEquals(value, newId) => "NEWID()",
        string text => "N'" + text.Replace("'", "''", StringComparison.Ordinal) + "'",
        bool flag => flag ? "1" : "0",
        int number => Text(number),
        DateTime date => "'" + date.ToString("yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture) + "'",
        Guid guid => "'" + guid.ToString() + "'",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported seed value."),
    };
}
