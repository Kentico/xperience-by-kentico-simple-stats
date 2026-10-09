-- Local dev DB only (DancingGoat example). Never run on a real project.
-- Sets ActivityUTMSource / ActivityUTMContent on existing landing page activities so the campaign sources section of the
-- web page "Stats (Labs)" tab has data (REPORT-21). Xperience does not fill these columns; the DancingGoat UTM capture
-- sample does for new landings.
--
-- Re-runnable: values depend only on the activity's position (by ActivityID) among the managed rows, so every run gives
-- the same result. Managed rows are landing page activities whose URL has no utm_source parameter. Real captures (URL
-- with utm_source, for example activity 5331 on /store) are never changed. About 1 in 4 managed rows is left without
-- UTM values, so campaign share stays below 100%.
--
-- Run from Git Bash:
--   docker cp .agent-resources/seed-utm-landings.sql mssql2022:/tmp/seed-utm-landings.sql
--   MSYS_NO_PATHCONV=1 docker exec mssql2022 /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P '<password>' -d xperience-by-kentico-simple-stats -i /tmp/seed-utm-landings.sql

SET NOCOUNT ON;

WITH [Managed] AS (
    SELECT
        A.[ActivityID],
        A.[ActivityUTMSource],
        A.[ActivityUTMContent],
        ROW_NUMBER() OVER (ORDER BY A.[ActivityID]) AS [RowNumber]
    FROM [OM_Activity] A
    WHERE A.[ActivityType] = N'landingpage'
        AND ISNULL(A.[ActivityURL], N'') NOT LIKE N'%utm[_]source=%'
)
UPDATE M
SET
    M.[ActivityUTMSource] = CASE M.[RowNumber] % 8
        WHEN 0 THEN N'newsletter'
        WHEN 1 THEN N'newsletter'
        WHEN 2 THEN N'linkedin'
        WHEN 3 THEN N'google'
        WHEN 4 THEN N'newsletter'
        WHEN 5 THEN NULL
        WHEN 6 THEN N'linkedin'
        ELSE NULL
    END,
    M.[ActivityUTMContent] = CASE M.[RowNumber] % 8
        WHEN 0 THEN N'hero-banner'
        WHEN 1 THEN N'footer'
        WHEN 2 THEN N'sponsored-post'
        WHEN 3 THEN NULL
        WHEN 4 THEN N'hero-banner'
        WHEN 5 THEN NULL
        WHEN 6 THEN NULL
        ELSE NULL
    END
FROM [Managed] M;

-- Result per page (all time).
SELECT
    MIN(A.[ActivityURL]) AS [Url],
    A.[ActivityLanguageID],
    COUNT(*) AS [Landings],
    COUNT(NULLIF(A.[ActivityUTMSource], N'')) AS [CampaignLandings]
FROM [OM_Activity] A
WHERE A.[ActivityType] = N'landingpage'
GROUP BY A.[ActivityWebPageItemGUID], A.[ActivityLanguageID]
ORDER BY [Landings] DESC;

SELECT
    A.[ActivityUTMSource] AS [Source],
    ISNULL(A.[ActivityUTMContent], N'(none)') AS [Content],
    COUNT(*) AS [Landings]
FROM [OM_Activity] A
WHERE A.[ActivityType] = N'landingpage' AND A.[ActivityUTMSource] <> N''
GROUP BY A.[ActivityUTMSource], A.[ActivityUTMContent]
ORDER BY [Landings] DESC;
