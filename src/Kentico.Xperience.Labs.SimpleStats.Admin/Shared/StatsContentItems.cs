using CMS.ContentEngine;
using CMS.DataEngine;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.Base.UIPages;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

/// <summary>
/// Kind (content type type) and channel filter of the content reports.
/// </summary>
internal static class StatsContentKinds
{
    /// <summary>
    /// Content type types the kind filter supports (<see cref="ClassContentTypeType"/>).
    /// </summary>
    public static IReadOnlyList<string> Kinds { get; } =
    [
        ClassContentTypeType.WEBSITE,
        ClassContentTypeType.REUSABLE,
        ClassContentTypeType.EMAIL,
        ClassContentTypeType.HEADLESS,
    ];

    /// <summary>
    /// Channel types in the channel filter, in this order.
    /// </summary>
    public static IReadOnlyList<ChannelType> ChannelTypes { get; } = [ChannelType.Website, ChannelType.Email, ChannelType.Headless];

    /// <summary>
    /// Returns the channel type items of a kind are in, or <c>null</c> for kinds without a channel (reusable) and for all kinds.
    /// </summary>
    public static ChannelType? GetChannelType(string? kind) =>
        kind switch
        {
            ClassContentTypeType.WEBSITE => ChannelType.Website,
            ClassContentTypeType.EMAIL => ChannelType.Email,
            ClassContentTypeType.HEADLESS => ChannelType.Headless,
            _ => null,
        };

    /// <summary>
    /// Returns <c>true</c> when the channel exists and fits the kind: the channel filter applies only to
    /// pages, emails and headless items, and only with a channel of the matching type.
    /// </summary>
    public static bool IsChannelAllowed(string? kind, int channelId, IEnumerable<StatsChannelOption> channels) =>
        GetChannelType(kind) is ChannelType type
        && channels.Any(c => c.Id == channelId && string.Equals(c.Type, type.ToString(), StringComparison.Ordinal));

    /// <summary>
    /// Normalizes the kind and channel of <paramref name="filter"/> (<c>null</c> for all). A channel that does not fit the kind
    /// (for example an email channel with pages) is dropped.
    /// </summary>
    /// <param name="filter">Filter sent by the client, or <c>null</c>.</param>
    /// <param name="channels">Channels of <see cref="ChannelTypes"/>.</param>
    public static StatsSnapshotQuery Normalize(StatsSnapshotFilter? filter, IEnumerable<StatsChannelOption> channels) =>
        (filter ?? new StatsSnapshotFilter()).Normalize(Kinds, (kind, channelId) => IsChannelAllowed(kind, channelId, channels));
}

/// <summary>
/// Channel column text of content lists (read with <see cref="StatsContentSql.ChannelLabelColumns"/>).
/// </summary>
internal static class StatsContentChannels
{
    /// <summary>
    /// Channel column text of reusable items, which have no channel and are edited in the Content hub:
    /// "Content hub - " plus the workspace display name, or only this text when the item has no workspace.
    /// </summary>
    public const string ContentHubLabel = "Content hub";

    /// <summary>
    /// Returns the channel, or for reusable items the Content hub with the workspace (see <see cref="ContentHubLabel"/>).
    /// <c>null</c> for other items without a channel.
    /// </summary>
    /// <param name="channel">Channel display name, or <c>null</c>.</param>
    /// <param name="isReusable">Whether the item is a reusable item.</param>
    /// <param name="workspace">Display name of the item's workspace, or <c>null</c>.</param>
    public static string? GetLabel(string? channel, bool isReusable, string? workspace)
    {
        if (!string.IsNullOrWhiteSpace(channel))
        {
            return channel;
        }
        if (!isReusable)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(workspace) ? ContentHubLabel : $"{ContentHubLabel} - {workspace}";
    }
}

/// <summary>
/// What is needed to link a content item in the admin.
/// </summary>
/// <param name="Location">Application the item is edited in.</param>
/// <param name="ContainerId">
/// Workspace ID (<see cref="ContentItemLocation.ContentHub"/>), <c>WebsiteChannelID</c>, <c>EmailChannelID</c> or <c>HeadlessChannelID</c>.
/// </param>
/// <param name="ObjectId">
/// Content item ID (<see cref="ContentItemLocation.ContentHub"/>), <c>WebPageItemID</c>, <c>EmailConfigurationID</c> or <c>HeadlessItemID</c>.
/// </param>
/// <param name="LanguageName">Code name of the language variant to open.</param>
internal sealed record ContentItemLink(ContentItemLocation Location, int ContainerId, int ObjectId, string LanguageName);

/// <summary>
/// Application a content item is edited in.
/// </summary>
internal enum ContentItemLocation
{
    /// <summary>Reusable item in the Content hub (by workspace).</summary>
    ContentHub,

    /// <summary>Page in a website channel.</summary>
    WebPage,

    /// <summary>Email in an email channel.</summary>
    Email,

    /// <summary>Headless item in a headless channel.</summary>
    Headless,
}

/// <summary>
/// Admin paths of content items where they are edited.
/// </summary>
internal static class StatsContentItemPaths
{
    /// <summary>
    /// Path of the item where it is edited: reusable items in the Content hub (with <see cref="IStatsAdminLinks"/>),
    /// pages, emails and headless items in their channel application (see <see cref="StatsChannelItemPaths"/>).
    /// </summary>
    public static string? GetPath(IStatsAdminLinks adminLinks, ContentItemLink link) =>
        link.Location switch
        {
            ContentItemLocation.ContentHub => GetContentHubPath(adminLinks, link),
            ContentItemLocation.WebPage => StatsChannelItemPaths.GetWebPagePath(link.ContainerId, link.LanguageName, link.ObjectId),
            ContentItemLocation.Email => StatsChannelItemPaths.GetEmailPath(link.ContainerId, link.LanguageName, link.ObjectId),
            ContentItemLocation.Headless => StatsChannelItemPaths.GetHeadlessItemPath(link.ContainerId, link.LanguageName, link.ObjectId),
            _ => throw new ArgumentOutOfRangeException(nameof(link), link.Location, "Unknown content item location."),
        };

    /// <summary>
    /// Path of the "Content" tab of a reusable item in the Content hub (all items of its workspace, the given language).
    /// </summary>
    private static string? GetContentHubPath(IStatsAdminLinks adminLinks, ContentItemLink link) =>
        adminLinks.GetPath<ContentItemEdit>(new PageParameterValues
        {
            { typeof(ContentHubWorkspace), link.ContainerId },
            { typeof(ContentHubContentLanguage), link.LanguageName },
            { typeof(ContentHubFolder), ContentHubSlugs.ALL_CONTENT_ITEMS },
            { typeof(ContentItemEditSection), link.ObjectId },
        });
}
