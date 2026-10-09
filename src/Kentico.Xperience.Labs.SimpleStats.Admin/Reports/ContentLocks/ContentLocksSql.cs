using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContentLocks;

/// <summary>
/// Builds the content locks batch: locks per user and the locked variants in one round trip (see <see cref="Build"/>).
/// Only constant SQL fragments are combined; all values are parameters.
/// </summary>
/// <remarks>
/// <para>
/// Items, variants, the kind and channel filter, the link and channel columns and user names come from <see cref="StatsContentSql"/>.
/// A lock is a language variant (<c>CMS_ContentItemLanguageMetadata</c>) with <c>ContentItemLanguageMetadataLockedByUserID</c> set
/// (one lock per variant). The user is joined from <c>CMS_User</c> (left join, so locks of users that no longer exist are kept).
/// </para>
/// <para>
/// The lock time is <c>ContentItemLanguageMetadataLockedWhen</c>, or the variant's last change when it is not set.
/// Times are compared as stored (server local time).
/// </para>
/// </remarks>
internal static class ContentLocksSql
{
    /// <summary>Locks older than this time (locked before it) are old.</summary>
    public const string OldBeforeParameter = "@OldBefore";

    /// <summary>Maximum number of listed variants.</summary>
    public const string LimitParameter = "@Limit";

    // Lock time of a variant (M); the variant's last change when the lock time is missing.
    private const string LockedWhen =
        "COALESCE(M.[ContentItemLanguageMetadataLockedWhen], M.[ContentItemLanguageMetadataModifiedWhen])";

    private const string LockedCondition = "AND M.[ContentItemLanguageMetadataLockedByUserID] IS NOT NULL";

    // The user holding the lock (U).
    private const string UserJoin =
        "LEFT JOIN [CMS_User] U ON U.[UserID] = M.[ContentItemLanguageMetadataLockedByUserID]";

    // 1. Locks per user. Locks of users that no longer exist have no U row and form one group (UserID NULL).
    // {0} = variants FROM, {1} = items WHERE.
    private const string UsersQuery = $$"""
        SELECT
            U.[UserID],
            {{StatsContentSql.UserDisplayName}} AS [UserName],
            COUNT(*) AS [LockCount],
            MIN({{LockedWhen}}) AS [OldestLockedWhen]
        {0}
        {{UserJoin}}
        {1}
            {{LockedCondition}}
        GROUP BY U.[UserID], U.[FirstName], U.[LastName], U.[UserName];
        """;

    // 2. Locked variants, oldest lock first. The window count runs before TOP, so it covers all locks.
    // {0} = variants FROM, {1} = items WHERE, {2} = link columns, {3} = link joins.
    private const string ItemsQuery = $$"""
        SELECT TOP (@Limit)
            M.[ContentItemLanguageMetadataID] AS [RowID],
            I.[ContentItemID],
            {2}
            L.[ContentLanguageName],
            M.[ContentItemLanguageMetadataDisplayName] AS [DisplayName],
            C.[ClassDisplayName],
            L.[ContentLanguageDisplayName],
            {{StatsContentSql.ChannelLabelColumns}}
            U.[UserID],
            {{StatsContentSql.UserDisplayName}} AS [UserName],
            {{LockedWhen}} AS [LockedWhen],
            M.[ContentItemLanguageMetadataModifiedWhen] AS [ModifiedWhen],
            SUM(CASE WHEN {{LockedWhen}} < @OldBefore THEN 1 ELSE 0 END) OVER () AS [OldLockCount]
        {0}
        INNER JOIN [CMS_ContentLanguage] L ON L.[ContentLanguageID] = M.[ContentItemLanguageMetadataContentLanguageID]
        {{StatsContentSql.ChannelLabelJoins}}
        {{UserJoin}}
        {3}
        {1}
            {{LockedCondition}}
        ORDER BY {{LockedWhen}}, M.[ContentItemLanguageMetadataID];
        """;

    /// <summary>
    /// Returns the batch. Add <see cref="StatsContentSql.KindParameter"/> when <paramref name="hasKind"/> and
    /// <see cref="StatsContentSql.ChannelParameter"/> when <paramref name="hasChannel"/>; all other parameters
    /// (<see cref="StatsContentSql.ClassTypeParameter"/> and the ones of this class) are always used.
    /// </summary>
    /// <remarks>Result sets, in order: locks per user, locked variants.</remarks>
    /// <param name="hasKind">Filter by content type type.</param>
    /// <param name="hasChannel">Filter by channel.</param>
    public static string Build(bool hasKind, bool hasChannel)
    {
        object[] args =
        [
            StatsContentSql.VariantsFrom,
            StatsContentSql.ItemsWhere(hasKind, hasChannel),
            StatsContentSql.LinkColumns,
            StatsContentSql.LinkApply,
        ];

        return string.Join(
            Environment.NewLine,
            new[] { UsersQuery, ItemsQuery }.Select(query => string.Format(null, query, args)));
    }
}
