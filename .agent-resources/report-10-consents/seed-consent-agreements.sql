-- Seeds consent agreements for visual testing of a consents report.
-- LOCAL DEV DB ONLY (DancingGoat example). Never run on a real project.
--
-- Mirrors CMS.DataProtection.ConsentAgreementService (31.9):
--   Agree  -> new row, Revoked = 0, ConsentHash = current consent hash
--   Revoke -> new row, Revoked = 1, ConsentHash = NULL
--   Current state of a contact + consent = its latest row.
-- Revoke event handlers (e.g. data erasure) do not run for seeded rows.
--
-- Per existing contact (deterministic, CHECKSUM of the contact GUID):
--   Tracking consent       : ~70% agree shortly after the contact was created
--   Coffee sample consent  : ~30% of contacts with an email agree 0-20 days after creation
--   ~15% / ~10% of those revoke 1-40 days later, ~35% of revokers agree again 3-30 days after that
-- Events after now are dropped, so recent contacts have fewer revocations.
-- Consents are matched by code name; a missing consent is skipped.
--
-- Seeded rows get deterministic GUIDs (MD5 of 'SEED-CONSENT|contact|consent|step'); re-running deletes them first.
--
-- Older consent text: one CMS_ConsentArchive row for the tracking consent (fixed GUID, an older text + its hash, replaced on re-run),
-- and ~25% of tracking agreements older than 45 days are given to that older text (its hash instead of the current one).
--
-- Run (Git Bash):
--   MSYS_NO_PATHCONV=1 docker exec -i mssql2022 /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P '<password>' \
--     -d xperience-by-kentico-simple-stats -b < .agent-resources/report-10-consents/seed-consent-agreements.sql

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

DECLARE @Now datetime2 = SYSDATETIME();
DECLARE @ArchiveGuid uniqueidentifier = 'E5C0A1B2-5EED-4C0A-9A11-000000000001';
DECLARE @OlderText nvarchar(max) = N'<?xml version="1.0" encoding="utf-16"?><ConsentContent xmlns:i="http://www.w3.org/2001/XMLSchema-instance"><ConsentLanguageVersions><ConsentLanguageVersion><FullText>&lt;p&gt;SEED older consent text (tracking), replaced by the current text.&lt;/p&gt;</FullText><LanguageName>en</LanguageName><ShortText>&lt;p&gt;SEED older consent text.&lt;/p&gt;</ShortText></ConsentLanguageVersion></ConsentLanguageVersions></ConsentContent>';
DECLARE @OlderHash nvarchar(100) = LOWER(CONVERT(nvarchar(64), HASHBYTES('SHA2_256', @OlderText), 2));

DECLARE @Consents TABLE ([ConsentID] int PRIMARY KEY, [ConsentHash] nvarchar(100), [Kind] char(1));
INSERT INTO @Consents
SELECT [ConsentID], [ConsentHash], 'T' FROM [CMS_Consent] WHERE [ConsentName] = N'DancingGoatTracking'
UNION ALL
SELECT [ConsentID], [ConsentHash], 'C' FROM [CMS_Consent] WHERE [ConsentName] = N'DancingGoatCoffeeSampleListForm';

BEGIN TRANSACTION;

-- 1. Remove previously seeded rows.
DELETE A FROM [CMS_ConsentAgreement] A
WHERE A.[ConsentAgreementGuid] IN (
    SELECT CAST(HASHBYTES('MD5', CONCAT(N'SEED-CONSENT|', C.[ContactID], N'|', S.[ConsentID], N'|', Step.N)) AS uniqueidentifier)
    FROM [OM_Contact] C
    CROSS JOIN @Consents S
    CROSS JOIN (VALUES (1), (2), (3)) Step(N));

-- 1b. Older text of the tracking consent (archive row, replaced on re-run).
DELETE FROM [CMS_ConsentArchive] WHERE [ConsentArchiveGuid] = @ArchiveGuid;
INSERT INTO [CMS_ConsentArchive] ([ConsentArchiveGuid], [ConsentArchiveLastModified], [ConsentArchiveConsentID], [ConsentArchiveHash], [ConsentArchiveContent])
SELECT @ArchiveGuid, DATEADD(day, -45, @Now), S.[ConsentID], @OlderHash, @OlderText FROM @Consents S WHERE S.[Kind] = 'T';

-- 2. Plan events per contact + consent.
DECLARE @Plan TABLE (
    [ContactID] int, [ConsentID] int, [ConsentHash] nvarchar(100), [Kind] char(1),
    [AgreeTime] datetime2, [RevokeTime] datetime2 NULL, [ReagreeTime] datetime2 NULL);

WITH R AS (
    SELECT C.[ContactID], C.[ContactCreated], C.[ContactEmail], S.[ConsentID], S.[ConsentHash], S.[Kind],
        ABS(CHECKSUM(C.[ContactGUID], S.[Kind], 'a')) % 100 AS [PAgree],
        ABS(CHECKSUM(C.[ContactGUID], S.[Kind], 'b')) % 100 AS [PRevoke],
        ABS(CHECKSUM(C.[ContactGUID], S.[Kind], 'c')) % 100 AS [PReagree],
        ABS(CHECKSUM(C.[ContactGUID], S.[Kind], 'd')) AS [Seed]
    FROM [OM_Contact] C
    CROSS JOIN @Consents S
),
A AS (
    SELECT R.*,
        CASE R.[Kind]
            WHEN 'T' THEN DATEADD(minute, R.[Seed] % 120, R.[ContactCreated])
            ELSE DATEADD(minute, R.[Seed] % (20 * 24 * 60), R.[ContactCreated])
        END AS [AgreeTime]
    FROM R
    WHERE (R.[Kind] = 'T' AND R.[PAgree] < 70)
       OR (R.[Kind] = 'C' AND R.[PAgree] < 30 AND NULLIF(R.[ContactEmail], N'') IS NOT NULL)
),
V AS (
    SELECT A.*,
        CASE WHEN A.[PRevoke] < CASE A.[Kind] WHEN 'T' THEN 15 ELSE 10 END
            THEN DATEADD(minute, (1 * 24 * 60) + A.[Seed] % (39 * 24 * 60), A.[AgreeTime]) END AS [RevokeTime]
    FROM A
)
INSERT INTO @Plan
SELECT V.[ContactID], V.[ConsentID], V.[ConsentHash], V.[Kind], V.[AgreeTime],
    CASE WHEN V.[RevokeTime] <= @Now THEN V.[RevokeTime] END,
    CASE WHEN V.[RevokeTime] <= @Now AND V.[PReagree] < 35
        AND DATEADD(minute, (3 * 24 * 60) + V.[Seed] % (27 * 24 * 60), V.[RevokeTime]) <= @Now
        THEN DATEADD(minute, (3 * 24 * 60) + V.[Seed] % (27 * 24 * 60), V.[RevokeTime]) END
FROM V
WHERE V.[AgreeTime] <= @Now;

-- 3. Insert agree / revoke / re-agree rows.
INSERT INTO [CMS_ConsentAgreement]
    ([ConsentAgreementGuid], [ConsentAgreementRevoked], [ConsentAgreementContactID], [ConsentAgreementConsentID], [ConsentAgreementConsentHash], [ConsentAgreementTime])
SELECT CAST(HASHBYTES('MD5', CONCAT(N'SEED-CONSENT|', P.[ContactID], N'|', P.[ConsentID], N'|', E.[Step])) AS uniqueidentifier),
    E.[Revoked], P.[ContactID], P.[ConsentID],
    CASE
        WHEN E.[Revoked] = 1 THEN NULL
        -- ~25% of tracking agreements older than 45 days were given to the older text.
        WHEN P.[Kind] = 'T' AND E.[Time] < DATEADD(day, -45, @Now) AND ABS(CHECKSUM(HASHBYTES('MD5', CONCAT(N'older|', P.[ContactID], N'|', E.[Step])))) % 4 = 0 THEN @OlderHash
        ELSE P.[ConsentHash]
    END,
    E.[Time]
FROM @Plan P
CROSS APPLY (VALUES
    (1, CAST(0 AS bit), P.[AgreeTime]),
    (2, CAST(1 AS bit), P.[RevokeTime]),
    (3, CAST(0 AS bit), P.[ReagreeTime])) E([Step], [Revoked], [Time])
WHERE E.[Time] IS NOT NULL;

COMMIT TRANSACTION;

-- Summary.
SELECT C.[ConsentDisplayName],
    SUM(CASE WHEN A.[ConsentAgreementRevoked] = 0 THEN 1 ELSE 0 END) AS [Agreements],
    SUM(CASE WHEN A.[ConsentAgreementRevoked] = 1 THEN 1 ELSE 0 END) AS [Revocations],
    COUNT(DISTINCT A.[ConsentAgreementContactID]) AS [Contacts],
    SUM(CASE WHEN A.[ConsentAgreementRevoked] = 0 AND A.[ConsentAgreementConsentHash] <> C.[ConsentHash] THEN 1 ELSE 0 END) AS [OlderTextAgreements],
    MIN(A.[ConsentAgreementTime]) AS [First], MAX(A.[ConsentAgreementTime]) AS [Last]
FROM [CMS_ConsentAgreement] A
JOIN [CMS_Consent] C ON C.[ConsentID] = A.[ConsentAgreementConsentID]
GROUP BY C.[ConsentDisplayName];
