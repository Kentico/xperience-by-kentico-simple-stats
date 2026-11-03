-- Seeds recipient list subscriptions for visual testing of a recipient lists report.
-- LOCAL DEV DB ONLY (DancingGoat example). Never run on a real project.
--
-- Mirrors the product (31.9):
--   OM_ContactGroupMember                       -> current membership only (no dates), type 0 = contact
--   EmailLibrary_EmailSubscriptionConfirmation  -> event history: Confirm = approved row, Revoke = unapproved row
--   Unsubscribe keeps the member row. Double opt-in: member row first, approved row after the link click.
--   EmailLibrary_EmailBounce                    -> per email address: hard bounce, or soft bounce count (limit 5 by default)
--
-- Lists:
--   DancingGoat.RecipientList (existing, matched by code name; skipped when missing)
--   SeedProductNews "Product news (seed)" (created once with a fixed GUID, plus its RecipientListSettings row)
--
-- Per contact with an email and list (deterministic, CHECKSUM of an MD5 of the contact GUID; plain CHECKSUM(guid, 'a') and (guid, 'b') are correlated):
--   ~40% (DancingGoat list) / ~20% (seed list) subscribe 0-20 days after the contact was created: member row + approved row
--   ~5% have a member row only (not confirmed, pending double opt-in)
--   ~15% of subscribers unsubscribe 1-40 days later (unapproved row, member row kept); ~25% of those subscribe again 5-30 days after that
--   Events after now are dropped.
-- Bounces: ~3% of subscribed contacts get an EmailBounce row: hard bounce, soft bounce at the limit (5), or soft bounce below it (2).
--
-- Real data is never changed: contact + list pairs that already have a member row or a confirmation row that is not seeded are skipped,
-- and bounce rows are added only for emails without one.
--
-- Re-run: seeded confirmation rows have deterministic GUIDs (MD5 of 'SEED-RL|contact|list|step') and are deleted first.
-- Member rows (no GUID) are deleted only for planned contact + list pairs without any non-seeded confirmation row,
-- and bounce rows only for the planned bounce emails.
--
-- Run (Git Bash):
--   MSYS_NO_PATHCONV=1 docker exec -i mssql2022 /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P '<password>' \
--     -d xperience-by-kentico-simple-stats -b < .agent-resources/report-11-recipient-lists/seed-recipient-lists.sql

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

DECLARE @Now datetime2 = SYSDATETIME();
DECLARE @SeedListGuid uniqueidentifier = '5EED0000-0000-4000-8000-0000000000A1';
DECLARE @SeedSettingsGuid uniqueidentifier = '5EED0000-0000-4000-8000-0000000000A2';

BEGIN TRANSACTION;

-- 0. Seed list (created once, kept on re-run so its ID stays stable).
IF NOT EXISTS (SELECT 1 FROM [OM_ContactGroup] WHERE [ContactGroupGUID] = @SeedListGuid)
    INSERT INTO [OM_ContactGroup] ([ContactGroupName], [ContactGroupDisplayName], [ContactGroupDescription], [ContactGroupEnabled],
        [ContactGroupLastModified], [ContactGroupGUID], [ContactGroupIsRecipientList], [ContactGroupIsSegment])
    VALUES (N'SeedProductNews', N'Product news (seed)', N'SEED recipient list for the simple stats report.', 1, @Now, @SeedListGuid, 1, 0);

IF NOT EXISTS (SELECT 1 FROM [EmailLibrary_RecipientListSettings] WHERE [RecipientListSettingsGUID] = @SeedSettingsGuid)
    INSERT INTO [EmailLibrary_RecipientListSettings] ([RecipientListSettingsRecipientListID], [RecipientListSettingsSendUnsubscriptionConfirmationEmail],
        [RecipientListSettingsSendSubscriptionConfirmationEmail], [RecipientListSettingsGUID])
    SELECT G.[ContactGroupID], 0, 0, @SeedSettingsGuid FROM [OM_ContactGroup] G WHERE G.[ContactGroupGUID] = @SeedListGuid;

DECLARE @Lists TABLE ([ListID] int PRIMARY KEY, [Kind] char(1), [SubscribePercent] int);
INSERT INTO @Lists
SELECT [ContactGroupID], 'D', 40 FROM [OM_ContactGroup] WHERE [ContactGroupName] = N'DancingGoat.RecipientList' AND [ContactGroupIsRecipientList] = 1
UNION ALL
SELECT [ContactGroupID], 'S', 20 FROM [OM_ContactGroup] WHERE [ContactGroupGUID] = @SeedListGuid;

-- 1. Plan per contact with an email + list (deterministic).
DECLARE @Plan TABLE (
    [ContactID] int, [ContactGuid] uniqueidentifier, [Email] nvarchar(254), [ListID] int,
    [Pending] bit, [SubscribeTime] datetime2 NULL, [UnsubscribeTime] datetime2 NULL, [ResubscribeTime] datetime2 NULL,
    PRIMARY KEY ([ContactID], [ListID]));

WITH R AS (
    SELECT C.[ContactID], C.[ContactGUID], C.[ContactEmail], C.[ContactCreated], L.[ListID], L.[Kind], L.[SubscribePercent],
        ABS(CHECKSUM(HASHBYTES('MD5', CONCAT(C.[ContactGUID], L.[Kind], 'a')))) % 100 AS [PSubscribe],
        ABS(CHECKSUM(HASHBYTES('MD5', CONCAT(C.[ContactGUID], L.[Kind], 'b')))) % 100 AS [PUnsubscribe],
        ABS(CHECKSUM(HASHBYTES('MD5', CONCAT(C.[ContactGUID], L.[Kind], 'c')))) % 100 AS [PResubscribe],
        ABS(CHECKSUM(HASHBYTES('MD5', CONCAT(C.[ContactGUID], L.[Kind], 'd')))) AS [Seed]
    FROM [OM_Contact] C
    CROSS JOIN @Lists L
    WHERE NULLIF(C.[ContactEmail], N'') IS NOT NULL
),
S AS (
    SELECT R.*,
        CASE WHEN R.[PSubscribe] < R.[SubscribePercent] THEN DATEADD(minute, R.[Seed] % (20 * 24 * 60), R.[ContactCreated]) END AS [SubscribeTime],
        CASE WHEN R.[PSubscribe] >= R.[SubscribePercent] AND R.[PSubscribe] < R.[SubscribePercent] + 5 THEN 1 ELSE 0 END AS [Pending]
    FROM R
),
U AS (
    SELECT S.*,
        CASE WHEN S.[SubscribeTime] IS NOT NULL AND S.[PUnsubscribe] < 15
            THEN DATEADD(minute, (1 * 24 * 60) + S.[Seed] % (39 * 24 * 60), S.[SubscribeTime]) END AS [UnsubscribeTime]
    FROM S
)
INSERT INTO @Plan
SELECT U.[ContactID], U.[ContactGUID], U.[ContactEmail], U.[ListID], U.[Pending],
    CASE WHEN U.[SubscribeTime] <= @Now THEN U.[SubscribeTime] END,
    CASE WHEN U.[SubscribeTime] <= @Now AND U.[UnsubscribeTime] <= @Now THEN U.[UnsubscribeTime] END,
    CASE WHEN U.[SubscribeTime] <= @Now AND U.[UnsubscribeTime] <= @Now AND U.[PResubscribe] < 25
        AND DATEADD(minute, (5 * 24 * 60) + U.[Seed] % (25 * 24 * 60), U.[UnsubscribeTime]) <= @Now
        THEN DATEADD(minute, (5 * 24 * 60) + U.[Seed] % (25 * 24 * 60), U.[UnsubscribeTime]) END
FROM U
WHERE U.[Pending] = 1 OR U.[SubscribeTime] <= @Now;

-- 2. Remove previously seeded rows.
DECLARE @SeededGuids TABLE ([Guid] uniqueidentifier PRIMARY KEY, [ContactID] int, [ListID] int);
INSERT INTO @SeededGuids
SELECT CAST(HASHBYTES('MD5', CONCAT(N'SEED-RL|', C.[ContactID], N'|', L.[ListID], N'|', Step.N)) AS uniqueidentifier), C.[ContactID], L.[ListID]
FROM [OM_Contact] C
CROSS JOIN @Lists L
CROSS JOIN (VALUES (1), (2), (3)) Step(N);

DELETE M FROM [OM_ContactGroupMember] M
INNER JOIN @Plan P ON P.[ContactID] = M.[ContactGroupMemberRelatedID] AND P.[ListID] = M.[ContactGroupMemberContactGroupID]
WHERE M.[ContactGroupMemberType] = 0
    AND NOT EXISTS (
        SELECT 1 FROM [EmailLibrary_EmailSubscriptionConfirmation] E
        WHERE E.[EmailSubscriptionConfirmationContactID] = P.[ContactID]
            AND E.[EmailSubscriptionConfirmationRecipientListID] = P.[ListID]
            AND E.[EmailSubscriptionConfirmationGUID] NOT IN (SELECT G.[Guid] FROM @SeededGuids G));

DELETE E FROM [EmailLibrary_EmailSubscriptionConfirmation] E
WHERE E.[EmailSubscriptionConfirmationGUID] IN (SELECT G.[Guid] FROM @SeededGuids G);

-- Planned bounce emails: ~3% of contacts subscribed to any list.
DECLARE @Bounces TABLE ([Email] nvarchar(254) PRIMARY KEY, [Hard] bit, [SoftCount] int);
INSERT INTO @Bounces
SELECT B.[Email],
    CASE WHEN B.[Type] = 0 THEN 1 ELSE 0 END,
    CASE B.[Type] WHEN 0 THEN 0 WHEN 1 THEN 5 ELSE 2 END
FROM (
    SELECT P.[Email], MIN(ABS(CHECKSUM(HASHBYTES('MD5', CONCAT(P.[ContactGuid], 'bounce-type')))) % 3) AS [Type]
    FROM @Plan P
    WHERE P.[SubscribeTime] IS NOT NULL AND ABS(CHECKSUM(HASHBYTES('MD5', CONCAT(P.[ContactGuid], 'bounce')))) % 100 < 3
    GROUP BY P.[Email]) B;

DELETE FROM [EmailLibrary_EmailBounce] WHERE [EmailBounceEmailAddress] IN (SELECT B.[Email] FROM @Bounces B);

-- 3. Skip real data: pairs that still have a member row or any confirmation row are not seeded.
DELETE P FROM @Plan P
WHERE EXISTS (
        SELECT 1 FROM [OM_ContactGroupMember] M
        WHERE M.[ContactGroupMemberType] = 0 AND M.[ContactGroupMemberRelatedID] = P.[ContactID] AND M.[ContactGroupMemberContactGroupID] = P.[ListID])
    OR EXISTS (
        SELECT 1 FROM [EmailLibrary_EmailSubscriptionConfirmation] E
        WHERE E.[EmailSubscriptionConfirmationContactID] = P.[ContactID] AND E.[EmailSubscriptionConfirmationRecipientListID] = P.[ListID]);

-- 4. Insert member rows (also kept for unsubscribed contacts, as the product does) and confirmation rows.
INSERT INTO [OM_ContactGroupMember] ([ContactGroupMemberContactGroupID], [ContactGroupMemberType], [ContactGroupMemberRelatedID], [ContactGroupMemberFromManual])
SELECT P.[ListID], 0, P.[ContactID], 1 FROM @Plan P;

INSERT INTO [EmailLibrary_EmailSubscriptionConfirmation]
    ([EmailSubscriptionConfirmationContactID], [EmailSubscriptionConfirmationRecipientListID], [EmailSubscriptionConfirmationIsApproved],
     [EmailSubscriptionConfirmationDate], [EmailSubscriptionConfirmationGUID])
SELECT P.[ContactID], P.[ListID], E.[Approved], E.[Time],
    CAST(HASHBYTES('MD5', CONCAT(N'SEED-RL|', P.[ContactID], N'|', P.[ListID], N'|', E.[Step])) AS uniqueidentifier)
FROM @Plan P
CROSS APPLY (VALUES
    (1, CAST(1 AS bit), P.[SubscribeTime]),
    (2, CAST(0 AS bit), P.[UnsubscribeTime]),
    (3, CAST(1 AS bit), P.[ResubscribeTime])) E([Step], [Approved], [Time])
WHERE E.[Time] IS NOT NULL;

INSERT INTO [EmailLibrary_EmailBounce] ([EmailBounceEmailAddress], [EmailBounceIsHardBounce], [EmailBounceSoftBounceCount])
SELECT B.[Email], B.[Hard], B.[SoftCount] FROM @Bounces B
WHERE NOT EXISTS (SELECT 1 FROM [EmailLibrary_EmailBounce] X WHERE X.[EmailBounceEmailAddress] = B.[Email]);

COMMIT TRANSACTION;

-- Summary (native overview rules, soft bounce limit 5).
WITH Latest AS (
    SELECT E.*, ROW_NUMBER() OVER (
        PARTITION BY E.[EmailSubscriptionConfirmationContactID], E.[EmailSubscriptionConfirmationRecipientListID]
        ORDER BY E.[EmailSubscriptionConfirmationDate] DESC, E.[EmailSubscriptionConfirmationID] DESC) AS [Rn]
    FROM [EmailLibrary_EmailSubscriptionConfirmation] E
)
SELECT G.[ContactGroupID], G.[ContactGroupDisplayName],
    COUNT(*) AS [Members],
    SUM(CASE WHEN L.[EmailSubscriptionConfirmationIsApproved] = 1 AND B.[EmailBounceID] IS NULL THEN 1 ELSE 0 END) AS [Receiving],
    SUM(CASE WHEN L.[EmailSubscriptionConfirmationIsApproved] = 1 AND B.[EmailBounceID] IS NOT NULL THEN 1 ELSE 0 END) AS [Bounced],
    SUM(CASE WHEN L.[EmailSubscriptionConfirmationIsApproved] = 0 THEN 1 ELSE 0 END) AS [Unsubscribed],
    SUM(CASE WHEN L.[EmailSubscriptionConfirmationID] IS NULL THEN 1 ELSE 0 END) AS [NotConfirmed]
FROM [OM_ContactGroup] G
INNER JOIN [OM_ContactGroupMember] M ON M.[ContactGroupMemberContactGroupID] = G.[ContactGroupID] AND M.[ContactGroupMemberType] = 0
INNER JOIN [OM_Contact] C ON C.[ContactID] = M.[ContactGroupMemberRelatedID]
LEFT JOIN Latest L ON L.[EmailSubscriptionConfirmationContactID] = M.[ContactGroupMemberRelatedID]
    AND L.[EmailSubscriptionConfirmationRecipientListID] = G.[ContactGroupID] AND L.[Rn] = 1
LEFT JOIN [EmailLibrary_EmailBounce] B ON B.[EmailBounceEmailAddress] = C.[ContactEmail]
    AND (B.[EmailBounceIsHardBounce] = 1 OR B.[EmailBounceSoftBounceCount] >= 5)
WHERE G.[ContactGroupIsRecipientList] = 1
GROUP BY G.[ContactGroupID], G.[ContactGroupDisplayName];

SELECT COUNT(*) AS [ConfirmationRows], MIN([EmailSubscriptionConfirmationDate]) AS [First], MAX([EmailSubscriptionConfirmationDate]) AS [Last]
FROM [EmailLibrary_EmailSubscriptionConfirmation];
SELECT [EmailBounceIsHardBounce], [EmailBounceSoftBounceCount], COUNT(*) AS [Rows] FROM [EmailLibrary_EmailBounce] GROUP BY [EmailBounceIsHardBounce], [EmailBounceSoftBounceCount];
