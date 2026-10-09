using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContactStats;

/// <summary>
/// SQL of the contact "Stats (Labs)" tab. Constant SQL only; all values are parameters.
/// </summary>
/// <remarks>
/// The activity type filter is one parameter (<see cref="TypesParameter"/>): the selected code names delimited and wrapped by '|'
/// (see <see cref="StatsSql.FormatDelimitedList"/>), matched with one constant <c>CHARINDEX</c> check, so any number of types needs no
/// string concatenation of values. <see cref="AllTypesParameter"/> = 1 skips the check.
/// </remarks>
internal static class ContactStatsSql
{
    public const string ContactParameter = "@ContactID";
    public const string PreviousFromParameter = "@PreviousFrom";
    public const string FromParameter = "@From";
    public const string ToExclusiveParameter = "@ToExclusive";
    public const string AllTypesParameter = "@AllTypes";
    public const string TypesParameter = "@Types";

    /// <summary>
    /// Activity type filter on activity alias <c>A</c>.
    /// </summary>
    public const string TypeCondition = "(@AllTypes = 1 OR CHARINDEX(N'|' + A.[ActivityType] + N'|', @Types) > 0)";

    /// <summary>
    /// Contact creation date, then activity types of the contact (any date) with counts and first activity.
    /// </summary>
    public const string InfoQuery = """
        SET NOCOUNT ON;
        SELECT C.[ContactCreated] FROM [OM_Contact] C WHERE C.[ContactID] = @ContactID;
        SELECT A.[ActivityType], COUNT(*) AS [Activities], MIN(A.[ActivityCreated]) AS [FirstActivity]
        FROM [OM_Activity] A
        WHERE A.[ActivityContactID] = @ContactID AND A.[ActivityType] IS NOT NULL
        GROUP BY A.[ActivityType]
        ORDER BY COUNT(*) DESC, A.[ActivityType];
        """;

    /// <summary>
    /// Activities by weekday (0 = Monday, independent of <c>DATEFIRST</c>: 1900-01-01 was a Monday) and hour in the range.
    /// </summary>
    public const string HeatmapQuery = $$"""
        SELECT W.[Weekday], W.[Hour], COUNT(*) AS [Activities]
        FROM [OM_Activity] A
        CROSS APPLY (SELECT
            DATEDIFF(day, CAST('19000101' AS date), CAST(A.[ActivityCreated] AS date)) % 7 AS [Weekday],
            DATEPART(hour, A.[ActivityCreated]) AS [Hour]) W
        WHERE A.[ActivityContactID] = @ContactID
            AND A.[ActivityCreated] >= @From
            AND A.[ActivityCreated] < @ToExclusive
            AND {{TypeCondition}}
        GROUP BY W.[Weekday], W.[Hour];
        """;

    /// <summary>
    /// Main batch. Activities of the contact from the previous period start to the range end are copied once into <c>@Acts</c>
    /// (one contact, so few rows). Result sets, in order: totals, daily counts by type, pages, forms, emails, sources,
    /// source and content pairs, content types, taxonomy options, tags (see <see cref="TagsQuery"/>).
    /// </summary>
    public const string Batch = $$"""
        SET NOCOUNT ON;
        DECLARE @Acts TABLE (
            [ActivityID] int NOT NULL,
            [IsCurrent] bit NOT NULL,
            [Created] datetime2 NOT NULL,
            [Day] date NOT NULL,
            [Type] nvarchar(250) NOT NULL,
            [ItemID] int NULL,
            [PageGUID] uniqueidentifier NULL,
            [LanguageID] int NULL,
            [Source] nvarchar(200) NULL,
            [Content] nvarchar(200) NULL,
            [Url] nvarchar(max) NULL
        );

        INSERT INTO @Acts
        SELECT
            A.[ActivityID],
            CASE WHEN A.[ActivityCreated] >= @From THEN 1 ELSE 0 END,
            A.[ActivityCreated],
            CAST(A.[ActivityCreated] AS date),
            A.[ActivityType],
            A.[ActivityItemID],
            A.[ActivityWebPageItemGUID],
            A.[ActivityLanguageID],
            {{StatsUtm.SourceColumn}},
            {{StatsUtm.ContentColumn}},
            CASE WHEN A.[ActivityCreated] >= @From AND A.[ActivityType] = @PageVisitType THEN {{StatsSql.ActivityUrlWithoutQuery}} END
        FROM [OM_Activity] A
        {{StatsSql.ActivityUrlCutApply}}
        WHERE A.[ActivityContactID] = @ContactID
            AND A.[ActivityType] IS NOT NULL
            AND A.[ActivityCreated] >= @PreviousFrom
            AND A.[ActivityCreated] < @ToExclusive
            AND {{TypeCondition}};

        -- 1. Totals.
        SELECT
            ISNULL(SUM(CASE WHEN X.[IsCurrent] = 1 THEN 1 ELSE 0 END), 0) AS [Activities],
            ISNULL(SUM(CASE WHEN X.[IsCurrent] = 0 THEN 1 ELSE 0 END), 0) AS [PreviousActivities],
            ISNULL(SUM(CASE WHEN X.[IsCurrent] = 1 AND X.[Type] = @LandingPageType THEN 1 ELSE 0 END), 0) AS [Sessions],
            ISNULL(SUM(CASE WHEN X.[IsCurrent] = 1 AND X.[Type] = @PageVisitType THEN 1 ELSE 0 END), 0) AS [PageVisits],
            ISNULL(SUM(CASE WHEN X.[IsCurrent] = 1 AND X.[Type] = @FormSubmitType THEN 1 ELSE 0 END), 0) AS [FormSubmissions],
            ISNULL(SUM(CASE WHEN X.[IsCurrent] = 1 AND X.[Type] = @EmailClickType THEN 1 ELSE 0 END), 0) AS [EmailClicks],
            COUNT(DISTINCT CASE WHEN X.[IsCurrent] = 1 THEN X.[Day] END) AS [ActiveDays],
            ISNULL(SUM(CASE WHEN X.[IsCurrent] = 1 AND X.[Type] = @LandingPageType AND X.[Source] IS NOT NULL THEN 1 ELSE 0 END), 0) AS [CampaignSessions],
            (SELECT MIN(A.[ActivityCreated]) FROM [OM_Activity] A WHERE A.[ActivityContactID] = @ContactID AND {{TypeCondition}}) AS [FirstSeen],
            (SELECT MAX(A.[ActivityCreated]) FROM [OM_Activity] A WHERE A.[ActivityContactID] = @ContactID AND {{TypeCondition}}) AS [LastSeen]
        FROM @Acts X;

        -- 2. Daily counts by type in the range.
        SELECT X.[Type], X.[Day], COUNT(*) AS [Activities]
        FROM @Acts X
        WHERE X.[IsCurrent] = 1
        GROUP BY X.[Type], X.[Day];

        -- 3. Page visits by page variant, with the page name in the visit's language and its channel.
        WITH [Pages] AS (
            SELECT X.[PageGUID], X.[LanguageID], COUNT(*) AS [Visits], MAX(X.[Created]) AS [LastVisited], MAX(X.[Url]) AS [Url],
                COUNT(*) OVER () AS [PageCount]
            FROM @Acts X
            WHERE X.[IsCurrent] = 1 AND X.[Type] = @PageVisitType
            GROUP BY X.[PageGUID], X.[LanguageID]
        )
        SELECT TOP (@PageLimit)
            G.[PageGUID], G.[LanguageID], G.[Visits], G.[LastVisited], G.[Url], G.[PageCount],
            M.[ContentItemLanguageMetadataDisplayName] AS [DisplayName],
            CL.[ContentLanguageDisplayName] AS [Language],
            CH.[ChannelDisplayName] AS [Channel]
        FROM [Pages] G
        LEFT JOIN [CMS_WebPageItem] P ON P.[WebPageItemGUID] = G.[PageGUID]
        LEFT JOIN [CMS_ContentItemLanguageMetadata] M
            ON M.[ContentItemLanguageMetadataContentItemID] = P.[WebPageItemContentItemID]
            AND M.[ContentItemLanguageMetadataContentLanguageID] = G.[LanguageID]
        LEFT JOIN [CMS_ContentLanguage] CL ON CL.[ContentLanguageID] = G.[LanguageID]
        LEFT JOIN [CMS_WebsiteChannel] W ON W.[WebsiteChannelID] = P.[WebPageItemWebsiteChannelID]
        LEFT JOIN [CMS_Channel] CH ON CH.[ChannelID] = W.[WebsiteChannelChannelID]
        ORDER BY G.[Visits] DESC, G.[LastVisited] DESC;

        -- 4. Form submissions by form (ActivityItemID = FormID).
        SELECT TOP (@FormLimit) F.[ItemID], F.[Activities], F.[Last], F.[ItemCount], B.[FormDisplayName] AS [DisplayName]
        FROM (
            SELECT X.[ItemID], COUNT(*) AS [Activities], MAX(X.[Created]) AS [Last], COUNT(*) OVER () AS [ItemCount]
            FROM @Acts X
            WHERE X.[IsCurrent] = 1 AND X.[Type] = @FormSubmitType
            GROUP BY X.[ItemID]
        ) F
        LEFT JOIN [CMS_Form] B ON B.[FormID] = F.[ItemID]
        ORDER BY F.[Activities] DESC, F.[Last] DESC;

        -- 5. Email clicks by email (ActivityItemID = EmailConfigurationID). Names are added only when the email tables exist.
        DECLARE @Emails TABLE (
            [ItemID] int NULL, [LanguageID] int NULL, [Activities] int NOT NULL, [Last] datetime2 NOT NULL,
            [DisplayName] nvarchar(200) NULL, [EmailChannelID] int NULL, [LanguageName] nvarchar(100) NULL);
        INSERT INTO @Emails ([ItemID], [LanguageID], [Activities], [Last])
        SELECT X.[ItemID], MAX(X.[LanguageID]), COUNT(*), MAX(X.[Created])
        FROM @Acts X
        WHERE X.[IsCurrent] = 1 AND X.[Type] = @EmailClickType
        GROUP BY X.[ItemID];

        IF OBJECT_ID(N'[EmailLibrary_EmailConfiguration]', N'U') IS NOT NULL AND OBJECT_ID(N'[EmailLibrary_EmailChannel]', N'U') IS NOT NULL
        BEGIN
            UPDATE E SET
                E.[DisplayName] = M.[ContentItemLanguageMetadataDisplayName],
                E.[EmailChannelID] = C.[EmailChannelID],
                E.[LanguageName] = M.[ContentLanguageName]
            FROM @Emails E
            INNER JOIN [EmailLibrary_EmailConfiguration] EC ON EC.[EmailConfigurationID] = E.[ItemID]
            LEFT JOIN [EmailLibrary_EmailChannel] C ON C.[EmailChannelID] = EC.[EmailConfigurationEmailChannelID]
            OUTER APPLY (
                SELECT TOP (1) LM.[ContentItemLanguageMetadataDisplayName], L.[ContentLanguageName]
                FROM [CMS_ContentItemLanguageMetadata] LM
                INNER JOIN [CMS_ContentLanguage] L ON L.[ContentLanguageID] = LM.[ContentItemLanguageMetadataContentLanguageID]
                WHERE LM.[ContentItemLanguageMetadataContentItemID] = EC.[EmailConfigurationContentItemID]
                ORDER BY
                    CASE WHEN LM.[ContentItemLanguageMetadataContentLanguageID] = E.[LanguageID] THEN 0 ELSE 1 END,
                    LM.[ContentItemLanguageMetadataID]
            ) M;
        END;

        SELECT TOP (@EmailLimit) E.[ItemID], E.[Activities], E.[Last], COUNT(*) OVER () AS [ItemCount], E.[DisplayName], E.[EmailChannelID], E.[LanguageName]
        FROM @Emails E
        ORDER BY E.[Activities] DESC, E.[Last] DESC;

        -- 6. Sessions (landings) by UTM source.
        SELECT TOP (@SourceLimit) X.[Source], COUNT(*) AS [Sessions], COUNT(*) OVER () AS [GroupCount]
        FROM @Acts X
        WHERE X.[IsCurrent] = 1 AND X.[Type] = @LandingPageType AND X.[Source] IS NOT NULL
        GROUP BY X.[Source]
        ORDER BY COUNT(*) DESC, X.[Source];

        -- 7. Sessions by UTM source and content.
        SELECT TOP (@SourceLimit) X.[Source], X.[Content], COUNT(*) AS [Sessions], COUNT(*) OVER () AS [GroupCount]
        FROM @Acts X
        WHERE X.[IsCurrent] = 1 AND X.[Type] = @LandingPageType AND X.[Source] IS NOT NULL
        GROUP BY X.[Source], X.[Content]
        ORDER BY COUNT(*) DESC, X.[Source], X.[Content];

        -- 8. Page visits by content type of the visited page.
        SELECT TOP (@InterestLimit)
            CC.[ClassID], CC.[ClassDisplayName], COUNT(*) AS [Visits], COUNT(DISTINCT X.[PageGUID]) AS [Pages],
            COUNT(*) OVER () AS [GroupCount], SUM(COUNT(*)) OVER () AS [MatchedVisits]
        FROM @Acts X
        INNER JOIN [CMS_WebPageItem] P ON P.[WebPageItemGUID] = X.[PageGUID]
        INNER JOIN [CMS_ContentItem] CI ON CI.[ContentItemID] = P.[WebPageItemContentItemID]
        INNER JOIN [CMS_Class] CC ON CC.[ClassID] = CI.[ContentItemContentTypeID]
        WHERE X.[IsCurrent] = 1 AND X.[Type] = @PageVisitType
        GROUP BY CC.[ClassID], CC.[ClassDisplayName]
        ORDER BY COUNT(*) DESC, CC.[ClassDisplayName];

        {{TagsQuery}}
        """;

    /// <summary>
    /// Tag interests, part of <see cref="Batch"/> (uses <c>@Acts</c>). Tags reached by each page visit in the range: tags of the visited page,
    /// plus tags of the items its published version links to (<c>CMS_ContentItemReference</c>, one level deep). The published version
    /// (<see cref="PublishedStatusParameter"/>) is the page's common data row in the visit's language, so links that exist only in a draft do
    /// not count. Tags are read from the item's language variant in the visit's language, else from its variant with the lowest metadata ID.
    /// A visit counts once per tag, however many paths reach it. Result sets: taxonomy options (all taxonomies, plus the selected one),
    /// then top tags of the taxonomy filter (<c>@TaxonomyID</c>, 0 = all) with the number of all such tags and of visits with any of them.
    /// </summary>
    public const string TagsQuery = """
        DECLARE @VisitTags TABLE ([ActivityID] int NOT NULL, [PageGUID] uniqueidentifier NOT NULL, [TagID] int NOT NULL, [TaxonomyID] int NOT NULL,
            PRIMARY KEY ([ActivityID], [TagID]));

        WITH [Visits] AS (
            SELECT X.[ActivityID], X.[PageGUID], X.[LanguageID]
            FROM @Acts X
            WHERE X.[IsCurrent] = 1 AND X.[Type] = @PageVisitType AND X.[PageGUID] IS NOT NULL AND X.[LanguageID] IS NOT NULL
        ),
        [PageItems] AS (
            SELECT DISTINCT V.[PageGUID], V.[LanguageID], P.[WebPageItemContentItemID] AS [ContentItemID]
            FROM [Visits] V
            INNER JOIN [CMS_WebPageItem] P ON P.[WebPageItemGUID] = V.[PageGUID]
        ),
        [Reached] AS (
            SELECT PI.[PageGUID], PI.[LanguageID], PI.[ContentItemID] AS [ItemID]
            FROM [PageItems] PI
            UNION
            SELECT PI.[PageGUID], PI.[LanguageID], R.[ContentItemReferenceTargetItemID]
            FROM [PageItems] PI
            INNER JOIN [CMS_ContentItemCommonData] D
                ON D.[ContentItemCommonDataContentItemID] = PI.[ContentItemID]
                AND D.[ContentItemCommonDataContentLanguageID] = PI.[LanguageID]
                AND D.[ContentItemCommonDataVersionStatus] = @PublishedStatus
            INNER JOIN [CMS_ContentItemReference] R ON R.[ContentItemReferenceSourceCommonDataID] = D.[ContentItemCommonDataID]
        ),
        [ReachedTags] AS (
            SELECT DISTINCT RC.[PageGUID], RC.[LanguageID], G.[TagID], G.[TagTaxonomyID]
            FROM [Reached] RC
            CROSS APPLY (
                SELECT TOP (1) M.[ContentItemLanguageMetadataID]
                FROM [CMS_ContentItemLanguageMetadata] M
                WHERE M.[ContentItemLanguageMetadataContentItemID] = RC.[ItemID]
                ORDER BY CASE WHEN M.[ContentItemLanguageMetadataContentLanguageID] = RC.[LanguageID] THEN 0 ELSE 1 END, M.[ContentItemLanguageMetadataID]
            ) M
            INNER JOIN [CMS_ContentItemTag] T ON T.[ContentItemTagContentItemLanguageMetadataID] = M.[ContentItemLanguageMetadataID]
            INNER JOIN [CMS_Tag] G ON G.[TagGUID] = T.[ContentItemTagTagGUID]
        )
        INSERT INTO @VisitTags ([ActivityID], [PageGUID], [TagID], [TaxonomyID])
        SELECT V.[ActivityID], V.[PageGUID], RT.[TagID], RT.[TagTaxonomyID]
        FROM [Visits] V
        INNER JOIN [ReachedTags] RT ON RT.[PageGUID] = V.[PageGUID] AND RT.[LanguageID] = V.[LanguageID];

        -- 9. Taxonomies with reached tags, plus the selected one.
        SELECT X.[TaxonomyID], X.[TaxonomyTitle], COUNT(DISTINCT VT.[TagID]) AS [Tags]
        FROM [CMS_Taxonomy] X
        LEFT JOIN @VisitTags VT ON VT.[TaxonomyID] = X.[TaxonomyID]
        WHERE VT.[TagID] IS NOT NULL OR X.[TaxonomyID] = @TaxonomyID
        GROUP BY X.[TaxonomyID], X.[TaxonomyTitle]
        ORDER BY X.[TaxonomyTitle], X.[TaxonomyID];

        -- 10. Top tags of the taxonomy filter.
        WITH [Filtered] AS (
            SELECT VT.[ActivityID], VT.[PageGUID], VT.[TagID]
            FROM @VisitTags VT
            WHERE @TaxonomyID = 0 OR VT.[TaxonomyID] = @TaxonomyID
        )
        SELECT TOP (@TagLimit)
            G.[TagID], G.[TagTitle], X.[TaxonomyTitle], COUNT(*) AS [Visits], COUNT(DISTINCT F.[PageGUID]) AS [Pages],
            COUNT(*) OVER () AS [GroupCount],
            (SELECT COUNT(DISTINCT F2.[ActivityID]) FROM [Filtered] F2) AS [TagVisits]
        FROM [Filtered] F
        INNER JOIN [CMS_Tag] G ON G.[TagID] = F.[TagID]
        INNER JOIN [CMS_Taxonomy] X ON X.[TaxonomyID] = G.[TagTaxonomyID]
        GROUP BY G.[TagID], G.[TagTitle], X.[TaxonomyTitle]
        ORDER BY COUNT(*) DESC, G.[TagTitle], G.[TagID];
        """;

    /// <summary>
    /// IDs of all taxonomies (to check the taxonomy filter).
    /// </summary>
    public const string TaxonomyIdsQuery = "SELECT X.[TaxonomyID] FROM [CMS_Taxonomy] X;";

    public const string PublishedStatusParameter = "@PublishedStatus";
    public const string TaxonomyParameter = "@TaxonomyID";
}
