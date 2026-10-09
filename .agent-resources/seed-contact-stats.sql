-- Local dev DB only (DancingGoat example). Never run on a real project.
-- Inserts about 150 synthetic activities for ONE existing contact (contact 4, Alexander Thompson) over the last 120 days, so the
-- contact "Stats (Labs)" tab (REPORT-22) has a longer trend: browsing sessions (a landing page activity, some with UTM values, plus
-- page visits), form submissions and email clicks. Activity is busier in recent weeks and mostly on weekday evenings.
--
-- Marker: every inserted row has ActivityComment = N'seed:contact-stats'. Re-runnable: deletes the marked rows first.
-- Values depend only on the row number (MD5 hash), so every run gives the same mix; dates are relative to the run date.
--
-- Run from Git Bash:
--   docker cp .agent-resources/seed-contact-stats.sql mssql2022:/tmp/seed-contact-stats.sql
--   MSYS_NO_PATHCONV=1 docker exec mssql2022 /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P '<password>' -d xperience-by-kentico-simple-stats -i /tmp/seed-contact-stats.sql
-- Then use Refresh in the tab (5 minute cache).

SET NOCOUNT ON;

DECLARE @Marker nvarchar(50) = N'seed:contact-stats';
DECLARE @ContactID int = 4;
DECLARE @Host nvarchar(100) = N'http://localhost:48896';
DECLARE @ChannelID int = (SELECT [ChannelID] FROM [CMS_Channel] WHERE [ChannelName] = N'DancingGoatPages');
DECLARE @EmailChannelID int = (SELECT [ChannelID] FROM [CMS_Channel] WHERE [ChannelName] = N'DancingGoatEmails');
DECLARE @LanguageID int = (SELECT [ContentLanguageID] FROM [CMS_ContentLanguage] WHERE [ContentLanguageName] = N'en');
DECLARE @Today date = CAST(GETDATE() AS date);

IF @ChannelID IS NULL OR @LanguageID IS NULL OR NOT EXISTS (SELECT 1 FROM [OM_Contact] WHERE [ContactID] = @ContactID)
BEGIN
    RAISERROR(N'Channel DancingGoatPages, language en or contact 4 not found.', 16, 1);
    RETURN;
END;

DELETE FROM [OM_Activity] WHERE [ActivityComment] = @Marker;
PRINT CONCAT(N'Deleted marked rows: ', @@ROWCOUNT);

-- Pages (real WebPageItemGUIDs of the DancingGoat site, English).
DECLARE @Pages TABLE ([PageNo] int PRIMARY KEY, [PageGUID] uniqueidentifier NOT NULL, [Path] nvarchar(200) NOT NULL, [Name] nvarchar(200) NOT NULL);
INSERT INTO @Pages VALUES
    (0, '9276D053-D581-4D48-80C6-953B088EC5AA', N'/',                                    N'Home'),
    (1, '3D56A04B-4C61-4241-B103-625A1613340D', N'/coffee-samples',                      N'Coffee samples'),
    (2, '888992C0-8702-4C4D-B50E-AC7C61F0A2FE', N'/store',                               N'Store'),
    (3, '221B2DA3-9225-4730-B880-657AC4D35B51', N'/store/coffees',                       N'Coffees'),
    (4, '496BB3FE-4AB7-4BC8-AC3E-ED058678D32D', N'/products/hario-v60',                  N'Hario V60'),
    (5, '4C572828-78AF-49CE-89E5-5A87A31EC2CD', N'/articles',                            N'Articles'),
    (6, '5FDCF2ED-C672-46F1-B959-776142BB6A74', N'/articles/coffee-beverages-explained', N'Coffee Beverages Explained'),
    (7, 'CD4D9A49-E1D3-40A3-B41C-D9A7BF982F02', N'/articles/which-brewing-fits-you',     N'Which brewing fits you?');

-- 45 sessions. Hash bytes pick the day (squared, so recent days are more likely), hour, pages and UTM values.
DECLARE @Sessions TABLE ([SessionNo] int PRIMARY KEY, [Start] datetime2 NOT NULL, [LandingPage] int NOT NULL, [NextPage] int NOT NULL,
    [Source] nvarchar(50) NULL, [Content] nvarchar(50) NULL);

WITH [N] AS (
    SELECT TOP (45) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS [No] FROM sys.all_objects
),
[H] AS (
    SELECT [No], HASHBYTES('MD5', CONCAT(N'SEED-CS|', [No])) AS [Hash] FROM [N]
),
[V] AS (
    SELECT [No],
        CAST(SUBSTRING([Hash], 1, 1) AS int) AS [B1],
        CAST(SUBSTRING([Hash], 2, 1) AS int) AS [B2],
        CAST(SUBSTRING([Hash], 3, 1) AS int) AS [B3],
        CAST(SUBSTRING([Hash], 4, 1) AS int) AS [B4],
        CAST(SUBSTRING([Hash], 5, 1) AS int) AS [B5],
        CAST(SUBSTRING([Hash], 6, 1) AS int) AS [B6]
    FROM [H]
)
INSERT INTO @Sessions
SELECT
    [No],
    -- Days ago 1-119, recent days more likely; hour mostly 18-22, some 8-12.
    DATEADD(minute, [B4] % 60,
        DATEADD(hour, CASE WHEN [B3] % 4 = 0 THEN 8 + [B3] % 5 ELSE 18 + [B3] % 5 END,
            CAST(DATEADD(day, -(1 + (119 * ([B1] / 255.0) * ([B1] / 255.0))), @Today) AS datetime2))),
    [B2] % 8,
    ([B2] + 1 + [B5] % 7) % 8,
    CASE [B6] % 5 WHEN 0 THEN N'newsletter' WHEN 1 THEN N'newsletter' WHEN 2 THEN N'linkedin' WHEN 3 THEN N'google' ELSE NULL END,
    CASE [B6] % 5 WHEN 0 THEN N'october-issue' WHEN 1 THEN N'hero-banner' WHEN 2 THEN N'sponsored-post' ELSE NULL END
FROM [V];

-- Landing page activity (with UTM values when the session has a source).
INSERT INTO [OM_Activity] ([ActivityContactID], [ActivityCreated], [ActivityType], [ActivityItemID], [ActivityItemDetailID], [ActivityURL],
    [ActivityTitle], [ActivityComment], [ActivityURLReferrer], [ActivityUTMSource], [ActivityUTMContent], [ActivityTrackedWebsiteID],
    [ActivityWebPageItemGUID], [ActivityLanguageID], [ActivityChannelID])
SELECT @ContactID, S.[Start], N'landingpage', 0, 0,
    @Host + P.[Path] + CASE WHEN S.[Source] IS NULL THEN N'' ELSE N'?utm_source=' + S.[Source] + ISNULL(N'&utm_content=' + S.[Content], N'') END,
    N'Landing page ''' + P.[Name] + N'''', @Marker, N'', S.[Source], S.[Content], 0, P.[PageGUID], @LanguageID, @ChannelID
FROM @Sessions S
INNER JOIN @Pages P ON P.[PageNo] = S.[LandingPage];

-- Page visit of the landing page, then of a second page a few minutes later.
INSERT INTO [OM_Activity] ([ActivityContactID], [ActivityCreated], [ActivityType], [ActivityItemID], [ActivityItemDetailID], [ActivityURL],
    [ActivityTitle], [ActivityComment], [ActivityTrackedWebsiteID], [ActivityWebPageItemGUID], [ActivityLanguageID], [ActivityChannelID])
SELECT @ContactID, DATEADD(millisecond, 2 + 180000 * X.[Step], S.[Start]), N'pagevisit', 0, 0, @Host + P.[Path],
    N'Page visit ''' + P.[Name] + N'''', @Marker, 0, P.[PageGUID], @LanguageID, @ChannelID
FROM @Sessions S
CROSS APPLY (VALUES (0, S.[LandingPage]), (1, S.[NextPage])) X ([Step], [PageNo])
INNER JOIN @Pages P ON P.[PageNo] = X.[PageNo];

-- Form submissions in every 8th session (Coffee sample list on the samples page, else Contact Us), 10 minutes after the start.
INSERT INTO [OM_Activity] ([ActivityContactID], [ActivityCreated], [ActivityType], [ActivityItemID], [ActivityURL], [ActivityTitle],
    [ActivityComment], [ActivityChannelID])
SELECT @ContactID, DATEADD(minute, 10, S.[Start]), N'bizformsubmit', F.[FormID], @Host + P.[Path],
    N'Form submitted ''' + F.[FormDisplayName] + N'''', @Marker, @ChannelID
FROM @Sessions S
INNER JOIN @Pages P ON P.[PageNo] = S.[LandingPage]
INNER JOIN [CMS_Form] F ON F.[FormName] = CASE WHEN S.[SessionNo] % 16 = 0 THEN N'DancingGoatContactUs' ELSE N'DancingGoatCoffeeSampleList' END
WHERE S.[SessionNo] % 8 = 0;

-- Email clicks: the newsletter sessions with "october-issue" start from a click in the regular email (EmailConfigurationID of the first
-- regular email-builder email), one minute before the landing.
INSERT INTO [OM_Activity] ([ActivityContactID], [ActivityCreated], [ActivityType], [ActivityItemID], [ActivityValue], [ActivityTitle],
    [ActivityComment], [ActivityLanguageID], [ActivityChannelID])
SELECT @ContactID, DATEADD(minute, -1, S.[Start]), N'emailclick', E.[EmailConfigurationID], P.[Path],
    N'Clicked link in email ''Dancing Goat Regular (Email Builder)''', @Marker, @LanguageID, @EmailChannelID
FROM @Sessions S
INNER JOIN @Pages P ON P.[PageNo] = S.[LandingPage]
CROSS APPLY (
    SELECT TOP (1) EC.[EmailConfigurationID]
    FROM [EmailLibrary_EmailConfiguration] EC
    WHERE EC.[EmailConfigurationName] LIKE N'DancingGoatRegular[_]EmailBuilder%'
    ORDER BY EC.[EmailConfigurationID]
) E
WHERE S.[Content] = N'october-issue';

SELECT [ActivityType], COUNT(*) AS [Inserted], MIN([ActivityCreated]) AS [First], MAX([ActivityCreated]) AS [Last]
FROM [OM_Activity]
WHERE [ActivityComment] = @Marker
GROUP BY [ActivityType];
