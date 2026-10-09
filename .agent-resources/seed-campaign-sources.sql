-- Local dev DB only (DancingGoat example). Never run on a real project.
-- Inserts synthetic landing page activities with UTM values (plus the page visit that the product logs with each landing) so the
-- global "Campaign sources" report (REPORT-21 Phase 2) has data: about 450 campaign landings from 6 sources over the last 90 days,
-- spread over 10 page variants with a different page mix per source and some trends, plus about 120 landings without UTM values.
--
-- Marker: every inserted row has ActivityComment = N'seed:campaign-sources'. Landing page and page visit activities never use
-- ActivityComment (checked 2026-10-08: 0 rows with a comment), so real rows are never matched.
-- Re-runnable: deletes the marked rows first, then inserts them again. Values depend only on the row number (MD5 hash), so every run
-- gives the same mix; dates are relative to the run date. The rows have URLs with utm_source, so seed-utm-landings.sql leaves them alone.
--
-- Run from Git Bash:
--   docker cp .agent-resources/seed-campaign-sources.sql mssql2022:/tmp/seed-campaign-sources.sql
--   MSYS_NO_PATHCONV=1 docker exec mssql2022 /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P '<password>' -d xperience-by-kentico-simple-stats -i /tmp/seed-campaign-sources.sql
-- Then use Refresh in the report (5 minute cache).

SET NOCOUNT ON;

DECLARE @Marker nvarchar(50) = N'seed:campaign-sources';
DECLARE @Host nvarchar(100) = N'http://localhost:48896';
DECLARE @ChannelID int = (SELECT [ChannelID] FROM [CMS_Channel] WHERE [ChannelName] = N'DancingGoatPages');
DECLARE @Today date = CAST(GETDATE() AS date);

IF @ChannelID IS NULL
BEGIN
    RAISERROR(N'Channel DancingGoatPages not found.', 16, 1);
    RETURN;
END;

DELETE FROM [OM_Activity] WHERE [ActivityComment] = @Marker;
PRINT CONCAT(N'Deleted marked rows: ', @@ROWCOUNT);

-- Page variants (real WebPageItemGUIDs of the DancingGoat site). Name = page name in the language (used in activity titles).
DECLARE @Pages TABLE ([PageKey] nvarchar(20) PRIMARY KEY, [PageGUID] uniqueidentifier NOT NULL, [LanguageName] nvarchar(10) NOT NULL, [Path] nvarchar(200) NOT NULL, [Name] nvarchar(200) NOT NULL);
INSERT INTO @Pages VALUES
    (N'home',       '9276D053-D581-4D48-80C6-953B088EC5AA', N'en', N'/',                                    N'Home'),
    (N'home-es',    '9276D053-D581-4D48-80C6-953B088EC5AA', N'es', N'/es/',                                 N'Inicio'),
    (N'samples',    '3D56A04B-4C61-4241-B103-625A1613340D', N'en', N'/coffee-samples',                      N'Coffee samples'),
    (N'store',      '888992C0-8702-4C4D-B50E-AC7C61F0A2FE', N'en', N'/store',                               N'Store'),
    (N'coffees',    '221B2DA3-9225-4730-B880-657AC4D35B51', N'en', N'/store/coffees',                       N'Coffees'),
    (N'brewers',    'C473A72C-60AD-484D-94DA-DC64B16B2B65', N'en', N'/store/brewers',                       N'Brewers'),
    (N'v60',        '496BB3FE-4AB7-4BC8-AC3E-ED058678D32D', N'en', N'/products/hario-v60',                  N'Hario V60'),
    (N'articles',   '4C572828-78AF-49CE-89E5-5A87A31EC2CD', N'en', N'/articles',                            N'Articles'),
    (N'beverages',  '5FDCF2ED-C672-46F1-B959-776142BB6A74', N'en', N'/articles/coffee-beverages-explained', N'Coffee Beverages Explained'),
    (N'brewing',    'CD4D9A49-E1D3-40A3-B41C-D9A7BF982F02', N'en', N'/articles/which-brewing-fits-you',     N'Which brewing fits you?');

-- Sources: number of landings, trend (up = more recent, down = older, spike = a campaign 20-35 days ago, flat), utm_medium.
DECLARE @Sources TABLE ([Source] nvarchar(50) PRIMARY KEY, [Landings] int NOT NULL, [Trend] nvarchar(10) NOT NULL, [Medium] nvarchar(20) NULL);
INSERT INTO @Sources VALUES
    (N'newsletter',   140, N'up',    N'email'),
    (N'google',       100, N'flat',  N'cpc'),
    (N'linkedin',      80, N'down',  N'social'),
    (N'facebook',      60, N'spike', N'social'),
    (N'partner-site',  40, N'flat',  N'referral'),
    (N'instagram',     30, N'up',    N'social'),
    -- Landings without UTM values (direct and organic visits), so the campaign share stays below 100%.
    (N'',             120, N'flat',  NULL);

-- Page mix per source (weights) and contents per source (weights; empty = no utm_content).
DECLARE @SourcePages TABLE ([Source] nvarchar(50) NOT NULL, [PageKey] nvarchar(20) NOT NULL, [Weight] int NOT NULL);
INSERT INTO @SourcePages VALUES
    (N'newsletter', N'samples', 45), (N'newsletter', N'store', 25), (N'newsletter', N'coffees', 20), (N'newsletter', N'home', 10),
    (N'google', N'home', 35), (N'google', N'brewers', 25), (N'google', N'v60', 25), (N'google', N'store', 15),
    (N'linkedin', N'beverages', 50), (N'linkedin', N'brewing', 30), (N'linkedin', N'articles', 20),
    (N'facebook', N'samples', 55), (N'facebook', N'coffees', 30), (N'facebook', N'home-es', 15),
    (N'partner-site', N'v60', 50), (N'partner-site', N'brewers', 30), (N'partner-site', N'articles', 20),
    (N'instagram', N'home-es', 60), (N'instagram', N'samples', 40),
    (N'', N'home', 50), (N'', N'store', 20), (N'', N'articles', 15), (N'', N'samples', 15);

DECLARE @SourceContents TABLE ([Source] nvarchar(50) NOT NULL, [Content] nvarchar(50) NOT NULL, [Weight] int NOT NULL);
INSERT INTO @SourceContents VALUES
    (N'newsletter', N'hero-banner', 50), (N'newsletter', N'footer', 20), (N'newsletter', N'october-issue', 30),
    (N'google', N'brand-search', 50), (N'google', N'cpc-brewers', 30), (N'google', N'', 20),
    (N'linkedin', N'sponsored-post', 70), (N'linkedin', N'company-page', 30),
    (N'facebook', N'spring-promo', 75), (N'facebook', N'carousel', 25),
    (N'partner-site', N'coffee-blog', 70), (N'partner-site', N'', 30),
    (N'instagram', N'story', 60), (N'instagram', N'reel', 40),
    (N'', N'', 100);

-- Cumulative weight ranges, so a number 0-99 picks a page or content.
DECLARE @PageRanges TABLE ([Source] nvarchar(50) NOT NULL, [PageKey] nvarchar(20) NOT NULL, [From] int NOT NULL, [To] int NOT NULL);
INSERT INTO @PageRanges
SELECT [Source], [PageKey],
    SUM([Weight]) OVER (PARTITION BY [Source] ORDER BY [PageKey] ROWS UNBOUNDED PRECEDING) - [Weight],
    SUM([Weight]) OVER (PARTITION BY [Source] ORDER BY [PageKey] ROWS UNBOUNDED PRECEDING)
FROM @SourcePages;

DECLARE @ContentRanges TABLE ([Source] nvarchar(50) NOT NULL, [Content] nvarchar(50) NOT NULL, [From] int NOT NULL, [To] int NOT NULL);
INSERT INTO @ContentRanges
SELECT [Source], [Content],
    SUM([Weight]) OVER (PARTITION BY [Source] ORDER BY [Content] ROWS UNBOUNDED PRECEDING) - [Weight],
    SUM([Weight]) OVER (PARTITION BY [Source] ORDER BY [Content] ROWS UNBOUNDED PRECEDING)
FROM @SourceContents;

-- Existing contacts, numbered.
DECLARE @Contacts TABLE ([Number] int PRIMARY KEY, [ContactID] int NOT NULL);
INSERT INTO @Contacts SELECT ROW_NUMBER() OVER (ORDER BY [ContactID]) - 1, [ContactID] FROM [OM_Contact];
DECLARE @ContactCount int = (SELECT COUNT(*) FROM @Contacts);

-- One row per landing: source row number N, four pseudo-random numbers (0-9999) from an MD5 hash.
DECLARE @Landings TABLE (
    [Source] nvarchar(50) NOT NULL,
    [N] int NOT NULL,
    [R1] int NOT NULL,
    [R2] int NOT NULL,
    [R3] int NOT NULL,
    [R4] int NOT NULL
);
WITH [Numbers] AS (
    SELECT TOP (1000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS [N] FROM sys.all_objects
)
INSERT INTO @Landings
SELECT S.[Source], X.[N],
    ABS(CAST(HASHBYTES('MD5', CONCAT(S.[Source], N'|', X.[N], N'|day')) AS int) % 10000),
    ABS(CAST(HASHBYTES('MD5', CONCAT(S.[Source], N'|', X.[N], N'|page')) AS int) % 10000),
    ABS(CAST(HASHBYTES('MD5', CONCAT(S.[Source], N'|', X.[N], N'|content')) AS int) % 10000),
    ABS(CAST(HASHBYTES('MD5', CONCAT(S.[Source], N'|', X.[N], N'|contact')) AS int) % 10000)
FROM @Sources S
INNER JOIN [Numbers] X ON X.[N] <= S.[Landings];

DECLARE @Rows TABLE (
    [Created] datetime2(7) NOT NULL,
    [ContactID] int NOT NULL,
    [PageGUID] uniqueidentifier NOT NULL,
    [LanguageID] int NOT NULL,
    [Url] nvarchar(500) NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Source] nvarchar(50) NULL,
    [Content] nvarchar(50) NULL
);

INSERT INTO @Rows
SELECT
    -- Days ago (2-89; GETDATE() is UTC in the container, so nothing is in the future in western time zones) by trend, plus a time of day from 7:00 to 22:59.
    DATEADD(second, 7 * 3600 + (L.[R1] * 7 + L.[R4]) % (16 * 3600), CAST(DATEADD(day, -D.[DaysAgo], @Today) AS datetime2(7))),
    C.[ContactID],
    P.[PageGUID],
    CL.[ContentLanguageID],
    @Host + P.[Path] + CASE WHEN L.[Source] = N'' THEN N''
        ELSE N'?utm_source=' + L.[Source]
            + ISNULL(N'&utm_medium=' + S.[Medium], N'')
            + CASE WHEN CR.[Content] = N'' THEN N'' ELSE N'&utm_content=' + CR.[Content] END
        END,
    P.[Name],
    NULLIF(L.[Source], N''),
    NULLIF(CR.[Content], N'')
FROM @Landings L
INNER JOIN @Sources S ON S.[Source] = L.[Source]
CROSS APPLY (SELECT CAST(L.[R1] AS float) / 10000 AS [U]) R
CROSS APPLY (SELECT CASE S.[Trend]
    WHEN N'up' THEN 2 + CAST(FLOOR(87.99 * (1 - SQRT(R.[U]))) AS int)
    WHEN N'down' THEN 2 + CAST(FLOOR(87.99 * SQRT(R.[U])) AS int)
    WHEN N'spike' THEN CASE WHEN L.[N] % 10 < 7 THEN 20 + L.[R1] % 16 ELSE 2 + L.[R1] % 88 END
    ELSE 2 + L.[R1] % 88
    END AS [DaysAgo]) D
INNER JOIN @PageRanges PR ON PR.[Source] = L.[Source] AND L.[R2] % 100 >= PR.[From] AND L.[R2] % 100 < PR.[To]
INNER JOIN @Pages P ON P.[PageKey] = PR.[PageKey]
INNER JOIN [CMS_ContentLanguage] CL ON CL.[ContentLanguageName] = P.[LanguageName]
INNER JOIN @ContentRanges CR ON CR.[Source] = L.[Source] AND L.[R3] % 100 >= CR.[From] AND L.[R3] % 100 < CR.[To]
INNER JOIN @Contacts C ON C.[Number] = L.[R4] % @ContactCount
-- Pages that no longer exist are skipped.
WHERE EXISTS (SELECT 1 FROM [CMS_WebPageItem] W WHERE W.[WebPageItemGUID] = P.[PageGUID]);

-- Landing page activity, then the page visit the product logs right after it (same URL, no UTM values).
INSERT INTO [OM_Activity] (
    [ActivityContactID], [ActivityCreated], [ActivityType], [ActivityItemID], [ActivityItemDetailID], [ActivityValue], [ActivityURL],
    [ActivityTitle], [ActivityComment], [ActivityURLReferrer], [ActivityUTMSource], [ActivityUTMContent], [ActivityTrackedWebsiteID],
    [ActivityWebPageItemGUID], [ActivityLanguageID], [ActivityChannelID])
SELECT [ContactID], [Created], N'landingpage', 0, 0, NULL, [Url],
    N'Landing page ''' + [Name] + N'''', @Marker, N'', [Source], [Content], 0,
    [PageGUID], [LanguageID], @ChannelID
FROM @Rows
UNION ALL
SELECT [ContactID], DATEADD(millisecond, 2, [Created]), N'pagevisit', 0, 0, NULL, [Url],
    N'Page visit ''' + [Name] + N'''', @Marker, N'', NULL, NULL, 0,
    [PageGUID], [LanguageID], @ChannelID
FROM @Rows;

PRINT CONCAT(N'Inserted marked rows: ', @@ROWCOUNT);

-- Result: marked landings per source and per page.
SELECT ISNULL([ActivityUTMSource], N'(no UTM)') AS [Source], COUNT(*) AS [Landings], COUNT(DISTINCT [ActivityContactID]) AS [Visitors],
    MIN(CAST([ActivityCreated] AS date)) AS [First], MAX(CAST([ActivityCreated] AS date)) AS [Last]
FROM [OM_Activity]
WHERE [ActivityComment] = @Marker AND [ActivityType] = N'landingpage'
GROUP BY [ActivityUTMSource]
ORDER BY [Landings] DESC;

SELECT LEFT([ActivityTitle], 60) AS [Page], [ActivityLanguageID] AS [LanguageID], COUNT(*) AS [Landings], COUNT([ActivityUTMSource]) AS [CampaignLandings]
FROM [OM_Activity]
WHERE [ActivityComment] = @Marker AND [ActivityType] = N'landingpage'
GROUP BY [ActivityTitle], [ActivityLanguageID]
ORDER BY [Landings] DESC;
