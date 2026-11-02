-- Seeds member roles for visual testing of the member registrations report ("Members by role" tile).
-- LOCAL DEV DB ONLY (DancingGoat example). Never run on a real project.
-- Run AFTER seed-members.sql (re-running seed-members.sql removes the role memberships of seeded members; run this again then).
--
-- Creates 4 roles (code names prefixed 'Seed') and puts ~60% of the seeded members in 1-2 roles, uneven sizes:
--   Premium ~30%, Partners ~15%, Newsletter only ~10% (+ ~1/3 of Premium also get it), Beta testers: a few Partners.
-- Seeded members are those with the deterministic GUIDs of seed-members.sql (MD5 of 'SEED-MEMBER|contact').
-- Re-running deletes the 'Seed%' roles (with their memberships and content item role links) first.
--
-- Run (Git Bash):
--   MSYS_NO_PATHCONV=1 docker exec -i mssql2022 /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P '<password>' \
--     -d xperience-by-kentico-simple-stats -b < .agent-resources/report-09-members/seed-member-roles.sql

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

DECLARE @Marker nvarchar(20) = N'SEED-MEMBER';

BEGIN TRANSACTION;

-- 1. Remove previously seeded roles.
DECLARE @OldRoles TABLE ([MemberRoleID] int PRIMARY KEY);
INSERT INTO @OldRoles SELECT [MemberRoleID] FROM [CMS_MemberRole] WHERE [MemberRoleName] LIKE N'Seed%';

DELETE FROM [CMS_MemberRoleMember] WHERE [MemberRoleMemberMemberRoleID] IN (SELECT [MemberRoleID] FROM @OldRoles);
DELETE FROM [CMS_ContentItemMemberRole] WHERE [ContentItemMemberRoleMemberRoleID] IN (SELECT [MemberRoleID] FROM @OldRoles);
DELETE FROM [CMS_MemberRole] WHERE [MemberRoleID] IN (SELECT [MemberRoleID] FROM @OldRoles);

-- 2. Roles.
INSERT INTO [CMS_MemberRole] ([MemberRoleGUID], [MemberRoleName], [MemberRoleDisplayName], [MemberRoleDescription])
VALUES
    (NEWID(), N'SeedPremium', N'Premium', N'Seeded role: paying subscribers.'),
    (NEWID(), N'SeedPartners', N'Partners', N'Seeded role: partner company staff.'),
    (NEWID(), N'SeedNewsletterOnly', N'Newsletter only', N'Seeded role: newsletter subscribers without premium content.'),
    (NEWID(), N'SeedBetaTesters', N'Beta testers', N'Seeded role: a few partners testing new features.');

DECLARE @Premium int = (SELECT [MemberRoleID] FROM [CMS_MemberRole] WHERE [MemberRoleName] = N'SeedPremium');
DECLARE @Partners int = (SELECT [MemberRoleID] FROM [CMS_MemberRole] WHERE [MemberRoleName] = N'SeedPartners');
DECLARE @Newsletter int = (SELECT [MemberRoleID] FROM [CMS_MemberRole] WHERE [MemberRoleName] = N'SeedNewsletterOnly');
DECLARE @Beta int = (SELECT [MemberRoleID] FROM [CMS_MemberRole] WHERE [MemberRoleName] = N'SeedBetaTesters');

-- 3. Deterministic buckets per seeded member (0-99), plus a second value for the second role.
DECLARE @Seeded TABLE ([MemberID] int PRIMARY KEY, [Bucket] int, [Second] int);
INSERT INTO @Seeded
SELECT M.[MemberID],
    ABS(CHECKSUM(M.[MemberGuid], N'role')) % 100,
    ABS(CHECKSUM(M.[MemberGuid], N'second-role')) % 100
FROM [CMS_Member] M
JOIN [OM_Contact] C ON M.[MemberGuid] = CAST(HASHBYTES('MD5', CONCAT(@Marker, N'|', C.[ContactID])) AS uniqueidentifier);

-- Buckets 0-44: no role.
INSERT INTO [CMS_MemberRoleMember] ([MemberRoleMemberMemberID], [MemberRoleMemberMemberRoleID])
SELECT S.[MemberID], R.[RoleID]
FROM @Seeded S
CROSS APPLY (
    SELECT @Premium AS [RoleID] WHERE S.[Bucket] BETWEEN 45 AND 74
    UNION ALL SELECT @Partners WHERE S.[Bucket] BETWEEN 75 AND 89
    UNION ALL SELECT @Newsletter WHERE S.[Bucket] >= 90
    -- Second roles.
    UNION ALL SELECT @Newsletter WHERE S.[Bucket] BETWEEN 45 AND 74 AND S.[Second] < 33
    UNION ALL SELECT @Beta WHERE S.[Bucket] BETWEEN 75 AND 89 AND S.[Second] < 40
) R;

COMMIT TRANSACTION;

SELECT R.[MemberRoleDisplayName], COUNT(RM.[MemberRoleMemberID]) AS [Members]
FROM [CMS_MemberRole] R
LEFT JOIN [CMS_MemberRoleMember] RM ON RM.[MemberRoleMemberMemberRoleID] = R.[MemberRoleID]
WHERE R.[MemberRoleName] LIKE N'Seed%'
GROUP BY R.[MemberRoleDisplayName]
ORDER BY [Members] DESC;

SELECT COUNT(*) AS [MembersWithoutRole]
FROM [CMS_Member] M
WHERE NOT EXISTS (SELECT 1 FROM [CMS_MemberRoleMember] RM WHERE RM.[MemberRoleMemberMemberID] = M.[MemberID]);
