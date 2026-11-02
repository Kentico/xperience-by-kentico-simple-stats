-- Local dev DB only (DancingGoat example). Never run on a real project.
-- Spreads the "Newsletter #N (seed)" emails (cloned in the UI and really sent through Mailpit)
-- over ~5 months and adds realistic engagement for the email summary report.
--
-- Targets: Regular emails whose primary-language display name ends with " (seed)" and whose
-- send configuration is Sent. Real hits and email click activities are kept and only moved in time
-- with the send. Seeded hits have a time fraction of exactly 700 ns; only those are deleted and
-- generated again on every run, and only for mailouts with no real opens or clicks.
-- Statistics are calculated by the product procedure from the hits, so they match the native rules.
-- Re-runnable: dates are relative to now.
--
-- Run from Git Bash:
--   docker cp .agent-resources/report-12-email-summary/seed-email-statistics.sql mssql2022:/tmp/seed-email-statistics.sql
--   MSYS_NO_PATHCONV=1 docker exec mssql2022 /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P '<password>' -d xperience-by-kentico-simple-stats -i /tmp/seed-email-statistics.sql

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Now datetime2 = SYSDATETIME();

-- Seed emails, oldest first. The newest is sent 7 days ago, the others every 14 days before it, at 9:00-10:59.
DECLARE @Emails TABLE (
    EmailConfigurationID int PRIMARY KEY,
    EmailConfigurationGUID uniqueidentifier,
    ContentItemID int,
    SendConfigurationID int,
    OldSendTime datetime2,
    NewSendTime datetime2,
    OpenRate int,      -- per mille
    ClickRate int,     -- per mille
    Unsubscribes int,
    HardBounces int);

INSERT INTO @Emails (EmailConfigurationID, EmailConfigurationGUID, ContentItemID, SendConfigurationID, OldSendTime, NewSendTime, OpenRate, ClickRate, Unsubscribes, HardBounces)
SELECT
    E.EmailConfigurationID,
    E.EmailConfigurationGUID,
    E.EmailConfigurationContentItemID,
    S.SendConfigurationID,
    S.SendConfigurationScheduledTime,
    DATEADD(minute, ABS(CHECKSUM(E.EmailConfigurationGUID)) % 120,
        DATEADD(hour, 9, CAST(CAST(DATEADD(day, -7 - 14 * (COUNT(*) OVER () - ROW_NUMBER() OVER (ORDER BY E.EmailConfigurationID)), @Now) AS date) AS datetime2))),
    450 + ABS(CHECKSUM(E.EmailConfigurationGUID, 'open')) % 121,
    90 + ABS(CHECKSUM(E.EmailConfigurationGUID, 'click')) % 111,
    ABS(CHECKSUM(E.EmailConfigurationGUID, 'unsub')) % 3,
    ABS(CHECKSUM(E.EmailConfigurationGUID, 'bounce')) % 2
FROM [EmailLibrary_EmailConfiguration] E
INNER JOIN [EmailLibrary_SendConfiguration] S ON S.[SendConfigurationEmailConfigurationID] = E.[EmailConfigurationID]
INNER JOIN [EmailLibrary_EmailChannel] C ON C.[EmailChannelID] = E.[EmailConfigurationEmailChannelID]
INNER JOIN [CMS_ContentItemLanguageMetadata] M ON M.[ContentItemLanguageMetadataContentItemID] = E.[EmailConfigurationContentItemID]
    AND M.[ContentItemLanguageMetadataContentLanguageID] = C.[EmailChannelPrimaryContentLanguageID]
WHERE E.[EmailConfigurationPurpose] = N'Regular'
    AND S.[SendConfigurationStatus] = 3
    AND S.[SendConfigurationScheduledTime] IS NOT NULL
    AND M.[ContentItemLanguageMetadataDisplayName] LIKE N'% (seed)';

IF NOT EXISTS (SELECT 1 FROM @Emails)
BEGIN
    PRINT 'No sent "(seed)" emails found. Clone and send "Newsletter #N (seed)" emails in the admin first.';
    RETURN;
END

BEGIN TRANSACTION;

-- 1. Move the send and publish dates.
UPDATE S SET S.[SendConfigurationScheduledTime] = X.NewSendTime
FROM [EmailLibrary_SendConfiguration] S INNER JOIN @Emails X ON X.SendConfigurationID = S.[SendConfigurationID];

UPDATE E SET E.[EmailConfigurationLastModified] = DATEADD(minute, -5, X.NewSendTime)
FROM [EmailLibrary_EmailConfiguration] E INNER JOIN @Emails X ON X.EmailConfigurationID = E.[EmailConfigurationID];

UPDATE M SET
    M.[ContentItemLanguageMetadataCreatedWhen] = DATEADD(day, -3, X.NewSendTime),
    M.[ContentItemLanguageMetadataModifiedWhen] = X.NewSendTime
FROM [CMS_ContentItemLanguageMetadata] M INNER JOIN @Emails X ON X.ContentItemID = M.[ContentItemLanguageMetadataContentItemID];

UPDATE D SET
    D.[ContentItemCommonDataFirstPublishedWhen] = X.NewSendTime,
    D.[ContentItemCommonDataLastPublishedWhen] = X.NewSendTime
FROM [CMS_ContentItemCommonData] D INNER JOIN @Emails X ON X.ContentItemID = D.[ContentItemCommonDataContentItemID]
WHERE D.[ContentItemCommonDataFirstPublishedWhen] IS NOT NULL;

-- 2. Remove earlier seed engagement and statistics. Seeded hits are marked by a time fraction of exactly 700 ns.
DELETE H FROM [EmailLibrary_EmailStatisticsHits] H INNER JOIN @Emails X ON X.EmailConfigurationID = H.[EmailStatisticsHitsEmailConfigurationID]
WHERE H.[EmailStatisticsHitsType] <> 0
    AND DATEPART(nanosecond, H.[EmailStatisticsHitsTime]) = 700;

DELETE S FROM [EmailLibrary_EmailStatistics] S INNER JOIN @Emails X ON X.EmailConfigurationID = S.[EmailStatisticsEmailConfigurationID];

-- 3. Real hits and email click activities: shift by the same amount as the send, so they stay after it.
UPDATE H SET H.[EmailStatisticsHitsTime] = DATEADD(second, DATEDIFF(second, X.OldSendTime, X.NewSendTime), H.[EmailStatisticsHitsTime])
FROM [EmailLibrary_EmailStatisticsHits] H INNER JOIN @Emails X ON X.EmailConfigurationID = H.[EmailStatisticsHitsEmailConfigurationID];

UPDATE A SET A.[ActivityCreated] = DATEADD(second, DATEDIFF(second, X.OldSendTime, X.NewSendTime), A.[ActivityCreated])
FROM [OM_Activity] A INNER JOIN @Emails X ON X.EmailConfigurationID = A.[ActivityItemID]
WHERE A.[ActivityType] = N'emailclick';

-- 4. Engagement on the real mailouts. Each mailout gets stable random numbers from its GUID.
DECLARE @Mailouts TABLE (
    EmailConfigurationID int,
    MailoutGUID uniqueidentifier PRIMARY KEY,
    SentTime datetime2,
    R int,           -- 0-999: who opens / clicks
    LagMinutes int,  -- first open after the send
    ExtraOpens int,  -- 0-2
    LinkPick int,    -- 0-99
    NoOpenHit bit,   -- click without an open hit (counts as an open)
    RankInEmail int);

INSERT INTO @Mailouts
SELECT
    H.[EmailStatisticsHitsEmailConfigurationID],
    H.[EmailStatisticsHitsMailoutGUID],
    H.[EmailStatisticsHitsTime],
    ABS(CHECKSUM(H.[EmailStatisticsHitsMailoutGUID], 'r')) % 1000,
    -- 50% within 3 h, 30% 3-24 h, 15% 1-3 days, 5% 3-7 days
    CASE
        WHEN ABS(CHECKSUM(H.[EmailStatisticsHitsMailoutGUID], 'lag')) % 100 < 50 THEN 2 + ABS(CHECKSUM(H.[EmailStatisticsHitsMailoutGUID], 'm')) % 178
        WHEN ABS(CHECKSUM(H.[EmailStatisticsHitsMailoutGUID], 'lag')) % 100 < 80 THEN 180 + ABS(CHECKSUM(H.[EmailStatisticsHitsMailoutGUID], 'm')) % 1260
        WHEN ABS(CHECKSUM(H.[EmailStatisticsHitsMailoutGUID], 'lag')) % 100 < 95 THEN 1440 + ABS(CHECKSUM(H.[EmailStatisticsHitsMailoutGUID], 'm')) % 2880
        ELSE 4320 + ABS(CHECKSUM(H.[EmailStatisticsHitsMailoutGUID], 'm')) % 5760
    END,
    CASE WHEN ABS(CHECKSUM(H.[EmailStatisticsHitsMailoutGUID], 'x')) % 100 < 70 THEN 0 ELSE 1 + ABS(CHECKSUM(H.[EmailStatisticsHitsMailoutGUID], 'x2')) % 2 END,
    ABS(CHECKSUM(H.[EmailStatisticsHitsMailoutGUID], 'link')) % 100,
    CASE WHEN ABS(CHECKSUM(H.[EmailStatisticsHitsMailoutGUID], 'noopen')) % 100 < 8 THEN 1 ELSE 0 END,
    ROW_NUMBER() OVER (PARTITION BY H.[EmailStatisticsHitsEmailConfigurationID] ORDER BY CHECKSUM(H.[EmailStatisticsHitsMailoutGUID], 'rank'))
FROM [EmailLibrary_EmailStatisticsHits] H
INNER JOIN @Emails X ON X.EmailConfigurationID = H.[EmailStatisticsHitsEmailConfigurationID]
WHERE H.[EmailStatisticsHitsType] = 0
    -- Mailouts with real engagement (someone really opened or clicked) keep only their real hits.
    AND NOT EXISTS (
        SELECT 1 FROM [EmailLibrary_EmailStatisticsHits] R
        WHERE R.[EmailStatisticsHitsMailoutGUID] = H.[EmailStatisticsHitsMailoutGUID]
            AND R.[EmailStatisticsHitsType] <> 0);

-- Links of each email, in ID order; the first link gets ~50% of clicks.
DECLARE @Links TABLE (EmailConfigurationID int, EmailLinkID int, LinkRank int, LinkCount int);
INSERT INTO @Links
SELECT L.[EmailLinkEmailConfigurationID], L.[EmailLinkID],
    ROW_NUMBER() OVER (PARTITION BY L.[EmailLinkEmailConfigurationID] ORDER BY L.[EmailLinkID]),
    COUNT(*) OVER (PARTITION BY L.[EmailLinkEmailConfigurationID])
FROM [EmailLibrary_EmailLink] L INNER JOIN @Emails X ON X.EmailConfigurationID = L.[EmailLinkEmailConfigurationID];

DECLARE @Hits TABLE (EmailConfigurationID int, HitType int, HitTime datetime2, MailoutGUID uniqueidentifier, EmailLinkID int NULL);

-- Opens (1 + extra opens a few hours apart). Clickers in the "no open hit" group get no open hit.
INSERT INTO @Hits
SELECT M.EmailConfigurationID, 1, DATEADD(minute, M.LagMinutes + N.n * (60 + M.LinkPick * 7), M.SentTime), M.MailoutGUID, NULL
FROM @Mailouts M
INNER JOIN @Emails X ON X.EmailConfigurationID = M.EmailConfigurationID
CROSS APPLY (VALUES (0), (1), (2)) N(n)
WHERE M.R < X.OpenRate
    AND N.n <= M.ExtraOpens
    AND NOT (M.R < X.ClickRate AND M.NoOpenHit = 1);

-- Clicks: a subset of openers, 1-10 minutes after the first open, on one of the email's links.
INSERT INTO @Hits
SELECT M.EmailConfigurationID, 2, DATEADD(minute, M.LagMinutes + 1 + M.LinkPick % 10, M.SentTime), M.MailoutGUID, L.EmailLinkID
FROM @Mailouts M
INNER JOIN @Emails X ON X.EmailConfigurationID = M.EmailConfigurationID
INNER JOIN @Links L ON L.EmailConfigurationID = M.EmailConfigurationID
    AND L.LinkRank = CASE WHEN M.LinkPick < 50 OR L.LinkCount = 1 THEN 1 ELSE 2 + M.LinkPick % (L.LinkCount - 1) END
WHERE M.R < X.ClickRate;

-- Unsubscribes: 0-2 openers who did not click, a few minutes after opening.
INSERT INTO @Hits
SELECT U.EmailConfigurationID, 5, DATEADD(minute, U.LagMinutes + 3, U.SentTime), U.MailoutGUID, NULL
FROM (
    SELECT M.*, X.Unsubscribes, ROW_NUMBER() OVER (PARTITION BY M.EmailConfigurationID ORDER BY M.RankInEmail) AS Pick
    FROM @Mailouts M
    INNER JOIN @Emails X ON X.EmailConfigurationID = M.EmailConfigurationID
    WHERE M.R >= X.ClickRate AND M.R < X.OpenRate
) U
WHERE U.Pick <= U.Unsubscribes;

-- Hard bounces: 0-1 mailouts that did not open, a few minutes after the send.
INSERT INTO @Hits
SELECT B.EmailConfigurationID, 4, DATEADD(minute, 2 + B.LinkPick % 10, B.SentTime), B.MailoutGUID, NULL
FROM (
    SELECT M.*, X.HardBounces, ROW_NUMBER() OVER (PARTITION BY M.EmailConfigurationID ORDER BY M.RankInEmail) AS Pick
    FROM @Mailouts M
    INNER JOIN @Emails X ON X.EmailConfigurationID = M.EmailConfigurationID
    WHERE M.R >= X.OpenRate
) B
WHERE B.Pick <= B.HardBounces;

INSERT INTO [EmailLibrary_EmailStatisticsHits]
    ([EmailStatisticsHitsEmailConfigurationID], [EmailStatisticsHitsType], [EmailStatisticsHitsTime], [EmailStatisticsHitsMailoutGUID], [EmailStatisticsHitsEmailLinkID], [EmailStatisticsHitsIsProcessed])
SELECT EmailConfigurationID, HitType, DATEADD(nanosecond, 700, CAST(CAST(HitTime AS datetime2(0)) AS datetime2)), MailoutGUID, EmailLinkID, 0
FROM @Hits
WHERE HitTime <= @Now;

-- 5. Recalculate the statistics from all hits of the seed emails with the product procedure.
UPDATE H SET H.[EmailStatisticsHitsIsProcessed] = 0
FROM [EmailLibrary_EmailStatisticsHits] H INNER JOIN @Emails X ON X.EmailConfigurationID = H.[EmailStatisticsHitsEmailConfigurationID];

DECLARE @ID int = (SELECT MIN(EmailConfigurationID) FROM @Emails);
WHILE @ID IS NOT NULL
BEGIN
    EXEC [Proc_EmailLibrary_EmailStatistics_RecalculateEmailStatistics] @EmailConfigurationID = @ID;
    SET @ID = (SELECT MIN(EmailConfigurationID) FROM @Emails WHERE EmailConfigurationID > @ID);
END

COMMIT TRANSACTION;

-- Summary.
SELECT
    X.EmailConfigurationID,
    LEFT(M.[ContentItemLanguageMetadataDisplayName], 30) AS [Name],
    X.NewSendTime AS [SendTime],
    S.[EmailStatisticsTotalSent] AS [Sent],
    S.[EmailStatisticsEmailsDelivered] AS [Delivered],
    S.[EmailStatisticsEmailUniqueOpens] AS [UniqueOpens],
    S.[EmailStatisticsEmailUniqueClicks] AS [UniqueClicks],
    S.[EmailStatisticsEmailHardBounces] AS [HardBounces],
    S.[EmailStatisticsUniqueUnsubscribes] AS [Unsubscribes],
    CAST(100.0 * S.[EmailStatisticsEmailUniqueOpens] / NULLIF(S.[EmailStatisticsEmailsDelivered], 0) AS decimal(5, 1)) AS [OpenRate],
    CAST(100.0 * S.[EmailStatisticsEmailUniqueClicks] / NULLIF(S.[EmailStatisticsEmailsDelivered], 0) AS decimal(5, 1)) AS [ClickRate]
FROM @Emails X
INNER JOIN [EmailLibrary_EmailStatistics] S ON S.[EmailStatisticsEmailConfigurationID] = X.EmailConfigurationID
INNER JOIN [CMS_ContentItemLanguageMetadata] M ON M.[ContentItemLanguageMetadataContentItemID] = X.ContentItemID
    AND M.[ContentItemLanguageMetadataContentLanguageID] = 1
ORDER BY X.NewSendTime;
