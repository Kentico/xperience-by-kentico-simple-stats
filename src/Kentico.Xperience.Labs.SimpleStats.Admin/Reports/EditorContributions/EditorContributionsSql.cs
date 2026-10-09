using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.EditorContributions;

/// <summary>
/// Builds the editor contributions batch: created per user and day, contributions per user and totals in one round trip
/// (see <see cref="Build"/>). Only constant SQL fragments are combined; all values are parameters.
/// </summary>
/// <remarks>
/// <para>
/// Variants: language variants (<c>CMS_ContentItemLanguageMetadata</c>) of the filtered items (<see cref="StatsContentSql"/>: content types only,
/// so page folders are left out; kind and channel filter). Created: <c>...CreatedWhen</c> by <c>...CreatedByUserID</c>.
/// Last modified: <c>...ModifiedWhen</c> by <c>...ModifiedByUserID</c>. Only the latest change of a variant is stored, so earlier changes
/// (also by other users) are not counted.
/// </para>
/// <para>
/// Published (optional): <c>CMS_ContentItemVersion</c> rows with the publish action (<c>ContentItemVersionAction.Publish</c>) by
/// <c>ContentItemVersionCreatedByUserID</c>, first publishes and updates. Only exists while content version history is enabled, and
/// is trimmed to a number of versions per variant.
/// </para>
/// <para>
/// The user is joined from <c>CMS_User</c> (left join): a user that no longer exists or is not set has no row, so all of those form one
/// group with <c>UserID</c> <c>NULL</c>. System users (the product's service user and the public user, by user name parameters) are flagged.
/// Contributions from the start of the previous period to the end of the range are collected once into the <c>@Contributions</c> table.
/// Times are compared as stored (server local time).
/// </para>
/// </remarks>
internal static class EditorContributionsSql
{
    /// <summary>Content item version action of a publish.</summary>
    public const string PublishActionParameter = "@PublishAction";

    /// <summary>User name of the product's system service user.</summary>
    public const string ServiceUserNameParameter = "@ServiceUserName";

    /// <summary>User name of the public (anonymous) user.</summary>
    public const string PublicUserNameParameter = "@PublicUserName";

    /// <summary>Start of the previous period (inclusive).</summary>
    public const string PreviousFromParameter = "@PreviousFrom";

    /// <summary>Start of the range (inclusive).</summary>
    public const string FromParameter = "@From";

    /// <summary>Day after the end of the range (exclusive).</summary>
    public const string ToExclusiveParameter = "@ToExclusive";

    /// <summary>Maximum number of users.</summary>
    public const string LimitParameter = "@Limit";

    public const string UserIdColumn = "UserID";
    public const string UserNameColumn = "UserName";
    public const string IsSystemColumn = "IsSystemUser";
    public const string DateColumn = "ActivityDate";
    public const string CountColumn = "ActivityCount";

    // Contribution kinds in @Contributions.
    private const string Created = "1";
    private const string LastModified = "2";
    private const string Published = "3";

    // {0} = variants FROM, {1} = items WHERE.
    private const string VariantsTable = """
        DECLARE @Variants TABLE (
            [VariantID] int NOT NULL PRIMARY KEY,
            [ContentItemID] int NOT NULL,
            [LanguageID] int NOT NULL,
            [ClassID] int NOT NULL,
            [CreatedWhen] datetime2(7) NOT NULL,
            [CreatedByUserID] int NULL,
            [ModifiedWhen] datetime2(7) NOT NULL,
            [ModifiedByUserID] int NULL,
            UNIQUE ([ContentItemID], [LanguageID])
        );

        INSERT INTO @Variants ([VariantID], [ContentItemID], [LanguageID], [ClassID], [CreatedWhen], [CreatedByUserID], [ModifiedWhen], [ModifiedByUserID])
        SELECT
            M.[ContentItemLanguageMetadataID],
            I.[ContentItemID],
            M.[ContentItemLanguageMetadataContentLanguageID],
            C.[ClassID],
            M.[ContentItemLanguageMetadataCreatedWhen],
            M.[ContentItemLanguageMetadataCreatedByUserID],
            M.[ContentItemLanguageMetadataModifiedWhen],
            M.[ContentItemLanguageMetadataModifiedByUserID]
        {0}
        {1};
        """;

    // Created and last modified from the previous period to the end of the range. UserID is NULL when the user no longer exists or is not set.
    private const string ContributionsTable = $$"""
        DECLARE @Contributions TABLE (
            [Kind] tinyint NOT NULL,
            [UserID] int NULL,
            [ClassID] int NOT NULL,
            [ActionWhen] datetime2(7) NOT NULL
        );

        INSERT INTO @Contributions ([Kind], [UserID], [ClassID], [ActionWhen])
        SELECT {{Created}}, U.[UserID], X.[ClassID], X.[CreatedWhen]
        FROM @Variants X
        LEFT JOIN [CMS_User] U ON U.[UserID] = X.[CreatedByUserID]
        WHERE X.[CreatedWhen] >= @PreviousFrom AND X.[CreatedWhen] < @ToExclusive
        UNION ALL
        SELECT {{LastModified}}, U.[UserID], X.[ClassID], X.[ModifiedWhen]
        FROM @Variants X
        LEFT JOIN [CMS_User] U ON U.[UserID] = X.[ModifiedByUserID]
        WHERE X.[ModifiedWhen] >= @PreviousFrom AND X.[ModifiedWhen] < @ToExclusive;
        """;

    // Publish version rows of the filtered variants from the previous period to the end of the range (first publishes and updates).
    private const string PublishedInsert = $$"""
        INSERT INTO @Contributions ([Kind], [UserID], [ClassID], [ActionWhen])
        SELECT {{Published}}, U.[UserID], X.[ClassID], V.[ContentItemVersionCreatedWhen]
        FROM [CMS_ContentItemVersion] V
        INNER JOIN @Variants X ON X.[ContentItemID] = V.[ContentItemVersionContentItemID] AND X.[LanguageID] = V.[ContentItemVersionContentLanguageID]
        LEFT JOIN [CMS_User] U ON U.[UserID] = V.[ContentItemVersionCreatedByUserID]
        WHERE V.[ContentItemVersionAction] = @PublishAction
            AND V.[ContentItemVersionCreatedWhen] >= @PreviousFrom
            AND V.[ContentItemVersionCreatedWhen] < @ToExclusive;
        """;

    // User name and system flag of A.[UserID] (U joined on it).
    private const string UserColumns = $$"""
        {{StatsContentSql.UserDisplayName}} AS [UserName],
                CAST(CASE WHEN U.[UserName] IN (@ServiceUserName, @PublicUserName) THEN 1 ELSE 0 END AS bit) AS [IsSystemUser]
        """;

    // 1. Created per user and day in the range.
    private const string DailyQuery = $$"""
        SELECT
            A.[UserID],
            {{UserColumns}},
            CAST(A.[ActionWhen] AS date) AS [ActivityDate],
            COUNT(*) AS [ActivityCount]
        FROM @Contributions A
        LEFT JOIN [CMS_User] U ON U.[UserID] = A.[UserID]
        WHERE A.[Kind] = {{Created}} AND A.[ActionWhen] >= @From
        GROUP BY A.[UserID], U.[FirstName], U.[LastName], U.[UserName], CAST(A.[ActionWhen] AS date);
        """;

    // 2. Contributions per user in the range, most created + last modified first. The window count runs before TOP, so it covers all users.
    private const string UsersQuery = $$"""
        SELECT TOP (@Limit)
            A.[UserID],
            {{UserColumns}},
            SUM(CASE WHEN A.[Kind] = {{Created}} THEN 1 ELSE 0 END) AS [Created],
            SUM(CASE WHEN A.[Kind] = {{LastModified}} THEN 1 ELSE 0 END) AS [LastModified],
            SUM(CASE WHEN A.[Kind] = {{Published}} THEN 1 ELSE 0 END) AS [Published],
            COUNT(DISTINCT A.[ClassID]) AS [ContentTypes],
            COUNT(*) OVER () AS [UserCount]
        FROM @Contributions A
        LEFT JOIN [CMS_User] U ON U.[UserID] = A.[UserID]
        WHERE A.[ActionWhen] >= @From
        GROUP BY A.[UserID], U.[FirstName], U.[LastName], U.[UserName]
        ORDER BY
            SUM(CASE WHEN A.[Kind] IN ({{Created}}, {{LastModified}}) THEN 1 ELSE 0 END) DESC,
            SUM(CASE WHEN A.[Kind] = {{Published}} THEN 1 ELSE 0 END) DESC,
            A.[UserID];
        """;

    // 3. Totals (one row), range and previous period. Active editors: users (that still exist) with any created or last modified.
    private const string TotalsQuery = $$"""
        SELECT
            COUNT(DISTINCT CASE WHEN A.[ActionWhen] >= @From AND A.[Kind] IN ({{Created}}, {{LastModified}}) THEN A.[UserID] END) AS [ActiveEditors],
            COUNT(DISTINCT CASE WHEN A.[ActionWhen] < @From AND A.[Kind] IN ({{Created}}, {{LastModified}}) THEN A.[UserID] END) AS [PreviousActiveEditors],
            ISNULL(SUM(CASE WHEN A.[ActionWhen] >= @From AND A.[Kind] = {{Created}} THEN 1 ELSE 0 END), 0) AS [Created],
            ISNULL(SUM(CASE WHEN A.[ActionWhen] < @From AND A.[Kind] = {{Created}} THEN 1 ELSE 0 END), 0) AS [PreviousCreated],
            ISNULL(SUM(CASE WHEN A.[ActionWhen] >= @From AND A.[Kind] = {{LastModified}} THEN 1 ELSE 0 END), 0) AS [LastModified],
            ISNULL(SUM(CASE WHEN A.[ActionWhen] < @From AND A.[Kind] = {{LastModified}} THEN 1 ELSE 0 END), 0) AS [PreviousLastModified],
            ISNULL(SUM(CASE WHEN A.[ActionWhen] >= @From AND A.[Kind] = {{Published}} THEN 1 ELSE 0 END), 0) AS [Published],
            ISNULL(SUM(CASE WHEN A.[ActionWhen] < @From AND A.[Kind] = {{Published}} THEN 1 ELSE 0 END), 0) AS [PreviousPublished]
        FROM @Contributions A;
        """;

    /// <summary>
    /// Returns the batch. Add <see cref="StatsContentSql.KindParameter"/> when <paramref name="hasKind"/>,
    /// <see cref="StatsContentSql.ChannelParameter"/> when <paramref name="hasChannel"/> and <see cref="PublishActionParameter"/> when
    /// <paramref name="withPublished"/>; all other parameters (<see cref="StatsContentSql.ClassTypeParameter"/> and the ones of this class) are always used.
    /// </summary>
    /// <remarks>Result sets, in order: created per user and day, users, totals (one row).</remarks>
    /// <param name="hasKind">Filter by content type type.</param>
    /// <param name="hasChannel">Filter by channel.</param>
    /// <param name="withPublished">Count publishes from content version history (only when it is enabled).</param>
    public static string Build(bool hasKind, bool hasChannel, bool withPublished) =>
        string.Join(
            Environment.NewLine,
            "SET NOCOUNT ON;",
            string.Format(null, VariantsTable, StatsContentSql.VariantsFrom, StatsContentSql.ItemsWhere(hasKind, hasChannel)),
            ContributionsTable,
            withPublished ? PublishedInsert : string.Empty,
            DailyQuery,
            UsersQuery,
            TotalsQuery);
}
