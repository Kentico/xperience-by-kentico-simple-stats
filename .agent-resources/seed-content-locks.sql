-- Content locks report (REPORT-14): back-dates 2 current locks so the "old lock" highlight (over 3 days) shows.
-- LOCAL DEV DB ONLY. Run after locking items by hand (Settings -> Content -> Content locking on, then start editing items).
-- Re-runnable: always picks the 2 locked language variants with the lowest metadata ID and sets their lock time
-- to 5 and 9 days ago. Touches only ContentItemLanguageMetadataLockedWhen of rows that are currently locked.
-- Times: SYSDATETIME() is the database server's clock. The app stores server local time, so if the DB container runs in UTC
-- the ages can be off by a few hours; that does not matter for days-old locks.
-- After running, select Refresh in the report (results are cached for 5 minutes).

SET NOCOUNT ON;

DECLARE @Now datetime2(7) = SYSDATETIME();

;WITH Locked AS (
    SELECT
        M.[ContentItemLanguageMetadataID],
        ROW_NUMBER() OVER (ORDER BY M.[ContentItemLanguageMetadataID]) AS [RowNumber]
    FROM [CMS_ContentItemLanguageMetadata] M
    WHERE M.[ContentItemLanguageMetadataLockedByUserID] IS NOT NULL
)
UPDATE M
SET M.[ContentItemLanguageMetadataLockedWhen] = DATEADD(day, CASE L.[RowNumber] WHEN 1 THEN -9 ELSE -5 END, @Now)
FROM [CMS_ContentItemLanguageMetadata] M
INNER JOIN Locked L ON L.[ContentItemLanguageMetadataID] = M.[ContentItemLanguageMetadataID]
WHERE L.[RowNumber] <= 2;

SELECT
    M.[ContentItemLanguageMetadataID],
    M.[ContentItemLanguageMetadataDisplayName],
    M.[ContentItemLanguageMetadataLockedByUserID],
    M.[ContentItemLanguageMetadataLockedWhen]
FROM [CMS_ContentItemLanguageMetadata] M
WHERE M.[ContentItemLanguageMetadataLockedByUserID] IS NOT NULL
ORDER BY M.[ContentItemLanguageMetadataLockedWhen];
