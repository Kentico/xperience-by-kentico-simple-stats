-- Translation status report (REPORT-18): back-dates 4 non-default language variants so they are behind the default language variant.
-- LOCAL DEV DB ONLY. Never put this in src/.
-- Re-runnable: always picks the same variants (rows 1, 3, 5 and 6 of the non-default variants that have a default variant, by metadata ID)
-- and sets their last change to 200, 45, 12 and 3 days before the default variant's last change. Touches only
-- ContentItemLanguageMetadataModifiedWhen of those rows. Saving one of these variants in the admin makes it up to date again
-- (then the next re-run picks the same rows and back-dates them again).
-- After running, select Refresh in the report (results are cached for 5 minutes).

SET NOCOUNT ON;

DECLARE @DefaultLanguageID int = (
    SELECT TOP (1) [ContentLanguageID]
    FROM [CMS_ContentLanguage]
    WHERE [ContentLanguageIsDefault] = 1
    ORDER BY [ContentLanguageID]);

;WITH Variants AS (
    SELECT
        M.[ContentItemLanguageMetadataID],
        D.[ContentItemLanguageMetadataModifiedWhen] AS [DefaultModifiedWhen],
        ROW_NUMBER() OVER (ORDER BY M.[ContentItemLanguageMetadataID]) AS [RowNumber]
    FROM [CMS_ContentItemLanguageMetadata] M
    INNER JOIN [CMS_ContentItemLanguageMetadata] D
        ON D.[ContentItemLanguageMetadataContentItemID] = M.[ContentItemLanguageMetadataContentItemID]
        AND D.[ContentItemLanguageMetadataContentLanguageID] = @DefaultLanguageID
    WHERE M.[ContentItemLanguageMetadataContentLanguageID] <> @DefaultLanguageID
)
UPDATE M
SET M.[ContentItemLanguageMetadataModifiedWhen] = DATEADD(day,
    CASE V.[RowNumber] WHEN 1 THEN -200 WHEN 3 THEN -45 WHEN 5 THEN -12 ELSE -3 END,
    V.[DefaultModifiedWhen])
FROM [CMS_ContentItemLanguageMetadata] M
INNER JOIN Variants V ON V.[ContentItemLanguageMetadataID] = M.[ContentItemLanguageMetadataID]
WHERE V.[RowNumber] IN (1, 3, 5, 6);

-- Non-default variants behind their default variant by more than 1 hour (the report's tolerance).
SELECT
    M.[ContentItemLanguageMetadataID],
    M.[ContentItemLanguageMetadataDisplayName],
    D.[ContentItemLanguageMetadataModifiedWhen] AS [DefaultModifiedWhen],
    M.[ContentItemLanguageMetadataModifiedWhen] AS [ModifiedWhen],
    DATEDIFF(day, M.[ContentItemLanguageMetadataModifiedWhen], D.[ContentItemLanguageMetadataModifiedWhen]) AS [DaysBehind]
FROM [CMS_ContentItemLanguageMetadata] M
INNER JOIN [CMS_ContentItemLanguageMetadata] D
    ON D.[ContentItemLanguageMetadataContentItemID] = M.[ContentItemLanguageMetadataContentItemID]
    AND D.[ContentItemLanguageMetadataContentLanguageID] = @DefaultLanguageID
WHERE M.[ContentItemLanguageMetadataContentLanguageID] <> @DefaultLanguageID
    AND M.[ContentItemLanguageMetadataModifiedWhen] < DATEADD(minute, -60, D.[ContentItemLanguageMetadataModifiedWhen])
ORDER BY [DaysBehind] DESC;
