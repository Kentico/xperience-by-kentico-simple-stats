-- Editor contributions report (REPORT-19): spreads "created by" and "last modified by" of recent language variants over the existing
-- administration users, so the per-user list and the created over time chart have more than one user.
-- LOCAL DEV DB ONLY. Never put this in src/.
-- Re-runnable: always picks the same variants (the 30 most recently created and the 30 most recently modified, by date and metadata ID)
-- and assigns them in turn to the existing users (all CMS_User rows except the public user, by user ID), with a different rotation for
-- "modified by" so created and modified are by different users. Touches only ContentItemLanguageMetadataCreatedByUserID and
-- ContentItemLanguageMetadataModifiedByUserID of those rows; dates are not changed. The original user IDs are not kept.
-- After running, select Refresh in the report (results are cached for 5 minutes). Use a 12-month range: most picked variants are older than 30 days.

SET NOCOUNT ON;

DECLARE @Users TABLE ([Number] int NOT NULL PRIMARY KEY, [UserID] int NOT NULL);

INSERT INTO @Users ([Number], [UserID])
SELECT ROW_NUMBER() OVER (ORDER BY [UserID]) - 1, [UserID]
FROM [CMS_User]
WHERE [UserName] <> N'public';

DECLARE @UserCount int = (SELECT COUNT(*) FROM @Users);

IF @UserCount = 0
BEGIN
    RAISERROR(N'No administration users found.', 16, 1);
    RETURN;
END;

-- Created by: of every 6 rows, 3 go to the first user, 2 to the second and 1 to the third (uneven, so the chart has a clear order).
;WITH Created AS (
    SELECT
        [ContentItemLanguageMetadataID],
        ROW_NUMBER() OVER (ORDER BY [ContentItemLanguageMetadataCreatedWhen] DESC, [ContentItemLanguageMetadataID] DESC) - 1 AS [RowNumber]
    FROM [CMS_ContentItemLanguageMetadata]
)
UPDATE M
SET M.[ContentItemLanguageMetadataCreatedByUserID] = U.[UserID]
FROM [CMS_ContentItemLanguageMetadata] M
INNER JOIN Created X ON X.[ContentItemLanguageMetadataID] = M.[ContentItemLanguageMetadataID]
INNER JOIN @Users U ON U.[Number] = (CASE WHEN X.[RowNumber] % 6 < 3 THEN 0 WHEN X.[RowNumber] % 6 < 5 THEN 1 ELSE 2 END) % @UserCount
WHERE X.[RowNumber] < 30;

-- Modified by: shifted by one user, so most variants are last modified by another user than the one who created them.
;WITH Modified AS (
    SELECT
        [ContentItemLanguageMetadataID],
        ROW_NUMBER() OVER (ORDER BY [ContentItemLanguageMetadataModifiedWhen] DESC, [ContentItemLanguageMetadataID] DESC) - 1 AS [RowNumber]
    FROM [CMS_ContentItemLanguageMetadata]
)
UPDATE M
SET M.[ContentItemLanguageMetadataModifiedByUserID] = U.[UserID]
FROM [CMS_ContentItemLanguageMetadata] M
INNER JOIN Modified X ON X.[ContentItemLanguageMetadataID] = M.[ContentItemLanguageMetadataID]
INNER JOIN @Users U ON U.[Number] = (CASE WHEN X.[RowNumber] % 6 < 3 THEN 1 WHEN X.[RowNumber] % 6 < 5 THEN 2 ELSE 0 END) % @UserCount
WHERE X.[RowNumber] < 30;

-- Created and last modified per user in the last 12 months (all content, no filter).
SELECT
    U.[UserID],
    U.[UserName],
    SUM(CASE WHEN M.[ContentItemLanguageMetadataCreatedByUserID] = U.[UserID]
        AND M.[ContentItemLanguageMetadataCreatedWhen] >= DATEADD(month, -12, CAST(GETDATE() AS date)) THEN 1 ELSE 0 END) AS [Created],
    SUM(CASE WHEN M.[ContentItemLanguageMetadataModifiedByUserID] = U.[UserID]
        AND M.[ContentItemLanguageMetadataModifiedWhen] >= DATEADD(month, -12, CAST(GETDATE() AS date)) THEN 1 ELSE 0 END) AS [LastModified]
FROM [CMS_User] U
CROSS JOIN [CMS_ContentItemLanguageMetadata] M
WHERE U.[UserName] <> N'public'
GROUP BY U.[UserID], U.[UserName]
ORDER BY U.[UserID];
