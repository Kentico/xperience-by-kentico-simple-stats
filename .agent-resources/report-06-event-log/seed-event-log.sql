-- Seeds CMS_EventLog with past events for visual testing of the "Event log" report.
-- LOCAL DEV DB ONLY (DancingGoat example). Never run on a real project.
--
-- Copies existing (not seeded) events to the 120 days before the latest real event day:
-- uneven daily volume, a few days with no events, error spikes on some days,
-- system events (no user) and user events kept as in the copied rows.
-- Seeded rows are marked with EventMachineName = 'simple-stats-seed', so re-running replaces them.
-- Stays well under the "Event log size" setting (CMSLogSize), so the product does not trim the log.
--
-- Run (Git Bash):
--   MSYS_NO_PATHCONV=1 docker exec -i mssql2022 /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P '<password>' \
--     -d xperience-by-kentico-simple-stats < .agent-resources/report-06-event-log/seed-event-log.sql

SET NOCOUNT ON;

DECLARE @Marker nvarchar(100) = N'simple-stats-seed';

DELETE FROM [CMS_EventLog] WHERE [EventMachineName] = @Marker;

DECLARE @LogSize int = TRY_CAST((SELECT [KeyValue] FROM [CMS_SettingsKey] WHERE [KeyName] = N'CMSLogSize') AS int);
DECLARE @Existing int = (SELECT COUNT(*) FROM [CMS_EventLog]);
DECLARE @Anchor date = (SELECT CAST(MAX([EventTime]) AS date) FROM [CMS_EventLog]);

IF @Anchor IS NULL
BEGIN
    PRINT 'CMS_EventLog is empty; nothing to copy.';
    RETURN;
END;

-- Real events to copy from.
IF OBJECT_ID('tempdb..#Templates') IS NOT NULL DROP TABLE #Templates;
SELECT [EventType], [Source], [EventCode], [UserID], [UserName], [IPAddress], [EventDescription], [EventUrl], [EventUserAgent], [EventUrlReferrer]
INTO #Templates
FROM [CMS_EventLog];

-- Days 1..120 before the anchor, with a planned number of events per day.
IF OBJECT_ID('tempdb..#Days') IS NOT NULL DROP TABLE #Days;
WITH [N] AS (
    SELECT TOP (120) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS [DaysBack]
    FROM sys.all_objects
)
SELECT
    DATEADD(day, -[DaysBack], @Anchor) AS [Day],
    [DaysBack],
    CASE
        -- A few days with no events.
        WHEN [DaysBack] IN (9, 10, 33, 61, 62, 63, 97) THEN 0
        -- Weekends are quieter.
        WHEN DATEPART(weekday, DATEADD(day, -[DaysBack], @Anchor)) IN (1, 7) THEN 3 + ABS(CHECKSUM(NEWID())) % 8
        -- Busier recent weeks, quieter older ones.
        WHEN [DaysBack] <= 30 THEN 25 + ABS(CHECKSUM(NEWID())) % 35
        ELSE 10 + ABS(CHECKSUM(NEWID())) % 30
    END AS [EventCount],
    -- Error spikes (for example a failing integration).
    CASE WHEN [DaysBack] IN (4, 5, 22, 47, 86) THEN 15 + ABS(CHECKSUM(NEWID())) % 20 ELSE 0 END AS [ErrorSpike]
INTO #Days
FROM [N];

INSERT INTO [CMS_EventLog]
    ([EventType], [EventTime], [Source], [EventCode], [UserID], [UserName], [IPAddress], [EventDescription], [EventUrl], [EventMachineName], [EventUserAgent], [EventUrlReferrer])
SELECT
    T.[EventType],
    DATEADD(second, ABS(CHECKSUM(NEWID())) % 86400, CAST(D.[Day] AS datetime2)),
    T.[Source], T.[EventCode], T.[UserID], T.[UserName], T.[IPAddress], T.[EventDescription], T.[EventUrl],
    @Marker,
    T.[EventUserAgent], T.[EventUrlReferrer]
FROM #Days D
CROSS APPLY (
    SELECT TOP (D.[EventCount]) * FROM #Templates ORDER BY NEWID()
) T
UNION ALL
SELECT
    T.[EventType],
    DATEADD(second, ABS(CHECKSUM(NEWID())) % 86400, CAST(D.[Day] AS datetime2)),
    T.[Source], T.[EventCode], T.[UserID], T.[UserName], T.[IPAddress], T.[EventDescription], T.[EventUrl],
    @Marker,
    T.[EventUserAgent], T.[EventUrlReferrer]
FROM #Days D
CROSS APPLY (
    -- Errors repeat, so pick with replacement from the error templates.
    SELECT TOP (D.[ErrorSpike]) E.*
    FROM #Templates E
    CROSS JOIN (SELECT TOP (10) 1 AS [X] FROM sys.all_objects) R
    WHERE E.[EventType] = N'E'
    ORDER BY NEWID()
) T;

DECLARE @Seeded int = (SELECT COUNT(*) FROM [CMS_EventLog] WHERE [EventMachineName] = @Marker);
PRINT CONCAT('Seeded ', @Seeded, ' events. Log now has ', @Existing + @Seeded, ' events (CMSLogSize = ', ISNULL(CAST(@LogSize AS nvarchar(20)), 'n/a'), ').');

IF @LogSize IS NOT NULL AND @Existing + @Seeded > @LogSize
    PRINT 'Warning: the log is over CMSLogSize, the product will delete the oldest events when the next event is logged.';

SELECT CAST([EventTime] AS date) AS [Day], [EventType], COUNT(*) AS [Events]
FROM [CMS_EventLog]
WHERE [EventMachineName] = @Marker AND [EventTime] >= DATEADD(day, -7, @Anchor)
GROUP BY CAST([EventTime] AS date), [EventType]
ORDER BY [Day], [EventType];
