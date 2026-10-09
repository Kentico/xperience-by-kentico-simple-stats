-- Local dev DB only (DancingGoat example). Never run on a real project.
-- Back-dates ContentItemLanguageMetadataModifiedWhen on some language variants so the
-- "Content age", "Oldest content", "Unused reusable items" and "Action needed" tiles of the
-- content inventory report have data. Re-runnable: dates are relative to now.
-- Page freshness report: back-dates ModifiedWhen of 3 visited pages ("Stale but popular") and
-- ContentItemCommonDataFirstPublishedWhen of 3 never visited pages ("Published pages with no visits").
--
-- Run from Git Bash:
--   docker cp .agent-resources/seed-content-age.sql mssql2022:/tmp/seed-content-age.sql
--   MSYS_NO_PATHCONV=1 docker exec mssql2022 /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P '<password>' -d xperience-by-kentico-simple-stats -i /tmp/seed-content-age.sql

SET NOCOUNT ON;

DECLARE @Now datetime2 = SYSDATETIME();

-- Spread by item ID: some variants over 12 months, 6-12 months and 3-6 months old.
UPDATE M
SET M.[ContentItemLanguageMetadataModifiedWhen] =
    CASE
        WHEN I.[ContentItemID] % 10 = 1 THEN DATEADD(day, -(400 + I.[ContentItemID]), @Now)
        WHEN I.[ContentItemID] % 10 = 2 THEN DATEADD(day, -(220 + I.[ContentItemID]), @Now)
        WHEN I.[ContentItemID] % 10 = 3 THEN DATEADD(day, -(120 + I.[ContentItemID] % 50), @Now)
        ELSE M.[ContentItemLanguageMetadataModifiedWhen]
    END
FROM [CMS_ContentItemLanguageMetadata] M
INNER JOIN [CMS_ContentItem] I ON I.[ContentItemID] = M.[ContentItemLanguageMetadataContentItemID]
WHERE I.[ContentItemID] % 10 IN (1, 2, 3)
    AND M.[ContentItemLanguageMetadataContentWorkflowStepID] IS NULL;

-- Variants in a workflow step: waiting 30 days (over the 14 day threshold).
UPDATE [CMS_ContentItemLanguageMetadata]
SET [ContentItemLanguageMetadataModifiedWhen] = DATEADD(day, -30, @Now)
WHERE [ContentItemLanguageMetadataContentWorkflowStepID] IS NOT NULL;

-- Page freshness report.
-- Published page variants (website content types with a URL, not in a workflow step) with their all-time page visits
-- (matched like the report: ActivityWebPageItemGUID + ActivityLanguageID).
DECLARE @PageVariants TABLE (
    [VariantID] int PRIMARY KEY,
    [ContentItemID] int NOT NULL,
    [LanguageID] int NOT NULL,
    [Visits] int NOT NULL
);

INSERT INTO @PageVariants
SELECT
    M.[ContentItemLanguageMetadataID],
    M.[ContentItemLanguageMetadataContentItemID],
    M.[ContentItemLanguageMetadataContentLanguageID],
    (SELECT COUNT(*) FROM [OM_Activity] A
        WHERE A.[ActivityType] = N'pagevisit'
            AND A.[ActivityWebPageItemGUID] = P.[WebPageItemGUID]
            AND A.[ActivityLanguageID] = M.[ContentItemLanguageMetadataContentLanguageID])
FROM [CMS_ContentItemLanguageMetadata] M
INNER JOIN [CMS_ContentItem] I ON I.[ContentItemID] = M.[ContentItemLanguageMetadataContentItemID]
INNER JOIN [CMS_Class] C ON C.[ClassID] = I.[ContentItemContentTypeID]
INNER JOIN [CMS_WebPageItem] P ON P.[WebPageItemContentItemID] = I.[ContentItemID]
WHERE C.[ClassType] = N'Content'
    AND C.[ClassContentTypeType] = N'Website'
    AND C.[ClassWebPageHasUrl] = 1
    AND M.[ContentItemLanguageMetadataContentWorkflowStepID] IS NULL
    AND EXISTS (
        SELECT 1 FROM [CMS_ContentItemCommonData] D
        WHERE D.[ContentItemCommonDataContentItemID] = M.[ContentItemLanguageMetadataContentItemID]
            AND D.[ContentItemCommonDataContentLanguageID] = M.[ContentItemLanguageMetadataContentLanguageID]
            AND D.[ContentItemCommonDataVersionStatus] = 2 -- VersionStatus.Published
    );

-- "Stale but popular": the 2nd to 4th most visited variants (the home page keeps its date) last changed 14-16 months ago.
WITH [Visited] AS (
    SELECT [VariantID], ROW_NUMBER() OVER (ORDER BY [Visits] DESC, [VariantID]) AS [Rank]
    FROM @PageVariants
    WHERE [Visits] > 0
)
UPDATE M
SET M.[ContentItemLanguageMetadataModifiedWhen] = DATEADD(day, -(400 + V.[Rank] * 30), @Now)
FROM [CMS_ContentItemLanguageMetadata] M
INNER JOIN [Visited] V ON V.[VariantID] = M.[ContentItemLanguageMetadataID]
WHERE V.[Rank] BETWEEN 2 AND 4;

-- "Published pages with no visits": 3 never visited variants (lowest IDs, so the same ones on every run)
-- first published 200, 250 and 300 days ago (their published version rows only).
WITH [Unvisited] AS (
    SELECT TOP (3) [ContentItemID], [LanguageID], ROW_NUMBER() OVER (ORDER BY [VariantID]) AS [Rank]
    FROM @PageVariants
    WHERE [Visits] = 0
    ORDER BY [VariantID]
)
UPDATE D
SET D.[ContentItemCommonDataFirstPublishedWhen] = DATEADD(day, -(150 + U.[Rank] * 50), @Now)
FROM [CMS_ContentItemCommonData] D
INNER JOIN [Unvisited] U
    ON U.[ContentItemID] = D.[ContentItemCommonDataContentItemID]
    AND U.[LanguageID] = D.[ContentItemCommonDataContentLanguageID]
WHERE D.[ContentItemCommonDataVersionStatus] = 2; -- VersionStatus.Published

SELECT
    SUM(CASE WHEN [ContentItemLanguageMetadataModifiedWhen] < DATEADD(month, -12, @Now) THEN 1 ELSE 0 END) AS [Over12Months],
    SUM(CASE WHEN [ContentItemLanguageMetadataModifiedWhen] < DATEADD(month, -6, @Now)
        AND [ContentItemLanguageMetadataModifiedWhen] >= DATEADD(month, -12, @Now) THEN 1 ELSE 0 END) AS [Months6To12],
    SUM(CASE WHEN [ContentItemLanguageMetadataModifiedWhen] < DATEADD(month, -3, @Now)
        AND [ContentItemLanguageMetadataModifiedWhen] >= DATEADD(month, -6, @Now) THEN 1 ELSE 0 END) AS [Months3To6]
FROM [CMS_ContentItemLanguageMetadata];
