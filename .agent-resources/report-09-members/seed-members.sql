-- Seeds members for visual testing of a member registrations report.
-- LOCAL DEV DB ONLY (DancingGoat example). Never run on a real project.
--
-- Every seeded member is an existing contact (sites with membership usually require tracking consent):
--   member email / name = contact email, first + last name are not stored on CMS_Member.
--   * Contacts that already have 'memberregistration' activities: member created at their first one.
--   * ~30% of other contacts with an email: member created 0-72 hours after the contact,
--     plus one new 'memberregistration' activity at that time (like the product logs on registration).
-- ~8% of members are disabled, ~12% are external (external sign-in). Passwords are not set, so seeded
-- members cannot sign in. Emails that already belong to a member are skipped. Members created after now are dropped.
-- OM_ProfileReference is left empty: profiles are a preview feature, enabled in code, not enabled in DancingGoat.
--
-- Seeded members get deterministic GUIDs (MD5 of 'SEED-MEMBER|contact'); seeded activities have
-- ActivityValue 'SEED-MEMBER'. Re-running deletes both (and role memberships / customer links of seeded members) first.
--
-- Run (Git Bash):
--   MSYS_NO_PATHCONV=1 docker exec -i mssql2022 /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P '<password>' \
--     -d xperience-by-kentico-simple-stats -b < .agent-resources/report-09-members/seed-members.sql

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

DECLARE @Now datetime2 = SYSDATETIME();
DECLARE @Marker nvarchar(20) = N'SEED-MEMBER';

BEGIN TRANSACTION;

-- 1. Remove previously seeded data.
DECLARE @Old TABLE ([MemberID] int PRIMARY KEY);
INSERT INTO @Old
SELECT M.[MemberID]
FROM [CMS_Member] M
JOIN [OM_Contact] C ON M.[MemberGuid] = CAST(HASHBYTES('MD5', CONCAT(@Marker, N'|', C.[ContactID])) AS uniqueidentifier);

UPDATE [Commerce_Customer] SET [CustomerMemberID] = NULL WHERE [CustomerMemberID] IN (SELECT [MemberID] FROM @Old);
UPDATE [Commerce_ShoppingCart] SET [ShoppingCartMemberID] = NULL WHERE [ShoppingCartMemberID] IN (SELECT [MemberID] FROM @Old);
UPDATE [OM_ProfileReference] SET [ProfileReferenceMemberID] = NULL WHERE [ProfileReferenceMemberID] IN (SELECT [MemberID] FROM @Old);
DELETE FROM [CMS_MemberRoleMember] WHERE [MemberRoleMemberMemberID] IN (SELECT [MemberID] FROM @Old);
DELETE FROM [CMS_Member] WHERE [MemberID] IN (SELECT [MemberID] FROM @Old);
DELETE FROM [OM_Activity] WHERE [ActivityType] = N'memberregistration' AND [ActivityValue] = @Marker;

-- 2. Plan members: one per distinct email, lowest contact ID wins.
DECLARE @Plan TABLE ([ContactID] int PRIMARY KEY, [Email] nvarchar(254), [Created] datetime2, [NewActivity] bit, [Seed] int);

WITH Emails AS (
    SELECT C.[ContactID], C.[ContactGUID], C.[ContactCreated], LTRIM(RTRIM(C.[ContactEmail])) AS [Email],
        ROW_NUMBER() OVER (PARTITION BY LOWER(LTRIM(RTRIM(C.[ContactEmail]))) ORDER BY C.[ContactID]) AS [EmailRank]
    FROM [OM_Contact] C
    WHERE NULLIF(LTRIM(RTRIM(C.[ContactEmail])), N'') IS NOT NULL
),
Registered AS (
    SELECT A.[ActivityContactID] AS [ContactID], MIN(A.[ActivityCreated]) AS [FirstRegistration]
    FROM [OM_Activity] A
    WHERE A.[ActivityType] = N'memberregistration'
    GROUP BY A.[ActivityContactID]
),
Candidates AS (
    SELECT E.[ContactID], E.[Email], R.[FirstRegistration], E.[ContactCreated],
        ABS(CHECKSUM(E.[ContactGUID], 'member')) AS [Seed]
    FROM Emails E
    LEFT JOIN Registered R ON R.[ContactID] = E.[ContactID]
    WHERE E.[EmailRank] = 1
        AND NOT EXISTS (SELECT 1 FROM [CMS_Member] M WHERE LOWER(M.[MemberEmail]) = LOWER(E.[Email]) OR LOWER(M.[MemberName]) = LOWER(E.[Email]))
)
INSERT INTO @Plan
SELECT X.[ContactID], X.[Email], X.[Created], X.[NewActivity], X.[Seed]
FROM (
    SELECT C.[ContactID], C.[Email], C.[Seed],
        COALESCE(C.[FirstRegistration], DATEADD(minute, C.[Seed] % (72 * 60), C.[ContactCreated])) AS [Created],
        CASE WHEN C.[FirstRegistration] IS NULL THEN 1 ELSE 0 END AS [NewActivity]
    FROM Candidates C
    WHERE C.[FirstRegistration] IS NOT NULL OR C.[Seed] % 100 < 30
) X
WHERE X.[Created] <= @Now;

-- 3. Members.
INSERT INTO [CMS_Member] ([MemberEmail], [MemberEnabled], [MemberCreated], [MemberGuid], [MemberName], [MemberPassword], [MemberIsExternal], [MemberSecurityStamp])
SELECT P.[Email],
    CASE WHEN (P.[Seed] / 100) % 100 < 8 THEN 0 ELSE 1 END,
    P.[Created],
    CAST(HASHBYTES('MD5', CONCAT(@Marker, N'|', P.[ContactID])) AS uniqueidentifier),
    P.[Email],
    NULL,
    CASE WHEN (P.[Seed] / 10000) % 100 < 12 THEN 1 ELSE 0 END,
    CONVERT(nvarchar(36), NEWID())
FROM @Plan P;

-- 4. Registration activities for members that had none.
INSERT INTO [OM_Activity] ([ActivityContactID], [ActivityType], [ActivityTitle], [ActivityValue], [ActivityCreated])
SELECT P.[ContactID], N'memberregistration', N'Member registration', @Marker, P.[Created]
FROM @Plan P
WHERE P.[NewActivity] = 1;

COMMIT TRANSACTION;

-- Summary.
SELECT COUNT(*) AS [Members],
    SUM(CASE WHEN [MemberEnabled] = 0 THEN 1 ELSE 0 END) AS [Disabled],
    SUM(CASE WHEN [MemberIsExternal] = 1 THEN 1 ELSE 0 END) AS [External],
    MIN([MemberCreated]) AS [First], MAX([MemberCreated]) AS [Last]
FROM [CMS_Member];
SELECT COUNT(*) AS [RegistrationActivities], SUM(CASE WHEN [ActivityValue] = @Marker THEN 1 ELSE 0 END) AS [Seeded]
FROM [OM_Activity] WHERE [ActivityType] = N'memberregistration';
