-- Seeds form submissions into the local DancingGoat dev DB for the form submissions report.
-- Dev DB only. Re-runnable: removes earlier seed rows (emails ending in @seed.example) first.
--
-- Result (uneven per form, spread over the last 90 days):
--   Coffee sample list  : copies of bizformsubmit activities (last 30 days) + 45 older rows (31-90 days ago)
--   Subscription        : copies of bizformsubmit activities (last 30 days) + 120 older rows (31-90 days ago)
--   Contact Us          : 24 rows, only 61-90 days ago -> 0 in the default 30-day range ("least used")
--
-- Run: docker exec mssql2022 /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "Pass@12345" -d xperience-by-kentico-simple-stats -i /tmp/seed-form-submissions.sql
-- (copy the file first: docker cp .agent-resources/report-04-form-submissions/seed-form-submissions.sql mssql2022:/tmp/seed-form-submissions.sql)

SET NOCOUNT ON;

DECLARE @CoffeeFormID int = (SELECT FormID FROM CMS_Form WHERE FormName = N'DancingGoatCoffeeSampleList');
DECLARE @SubscriptionFormID int = (SELECT FormID FROM CMS_Form WHERE FormName = N'DancingGoatSubscription');
DECLARE @Now datetime2 = SYSDATETIME();

DELETE FROM [Form_DancingGoat_CoffeeSampleList] WHERE [Email] LIKE N'%@seed.example';
DELETE FROM [Form_Form_2023_09_12_17_45] WHERE [UserEmail] LIKE N'%@seed.example';
DELETE FROM [Form_Form_2023_09_15_10_28] WHERE [Email] LIKE N'%@seed.example';

-- Numbers 1..200 for generated rows.
DECLARE @N TABLE (N int PRIMARY KEY);
INSERT INTO @N (N)
SELECT TOP (200) ROW_NUMBER() OVER (ORDER BY (SELECT NULL))
FROM sys.all_objects;

-- Coffee sample list: one row per activity + older rows.
INSERT INTO [Form_DancingGoat_CoffeeSampleList] ([FormInserted], [FormUpdated], [FirstName], [LastName], [Email], [Address], [City], [ZIPCode], [Country])
SELECT A.[ActivityCreated], A.[ActivityCreated], N'Seed', N'Coffee', CONCAT(N'coffee-', A.[ActivityID], N'@seed.example'), N'1 Seed St', N'Seedville', N'00000', N'USA'
FROM [OM_Activity] A
WHERE A.[ActivityType] = N'bizformsubmit' AND A.[ActivityItemID] = @CoffeeFormID;

INSERT INTO [Form_DancingGoat_CoffeeSampleList] ([FormInserted], [FormUpdated], [FirstName], [LastName], [Email], [Address], [City], [ZIPCode], [Country])
SELECT D.[At], D.[At], N'Seed', N'Coffee', CONCAT(N'coffee-old-', N.N, N'@seed.example'), N'1 Seed St', N'Seedville', N'00000', N'USA'
FROM @N N
CROSS APPLY (SELECT DATEADD(minute, -((N.N * 37) % 1440), DATEADD(day, -(31 + (N.N * 7) % 60), @Now)) AS [At]) D
WHERE N.N <= 45;

-- Subscription: one row per activity + older rows.
INSERT INTO [Form_Form_2023_09_15_10_28] ([FormInserted], [FormUpdated], [Email])
SELECT A.[ActivityCreated], A.[ActivityCreated], CONCAT(N'subscription-', A.[ActivityID], N'@seed.example')
FROM [OM_Activity] A
WHERE A.[ActivityType] = N'bizformsubmit' AND A.[ActivityItemID] = @SubscriptionFormID;

INSERT INTO [Form_Form_2023_09_15_10_28] ([FormInserted], [FormUpdated], [Email])
SELECT D.[At], D.[At], CONCAT(N'subscription-old-', N.N, N'@seed.example')
FROM @N N
CROSS APPLY (SELECT DATEADD(minute, -((N.N * 53) % 1440), DATEADD(day, -(31 + (N.N * 11) % 60), @Now)) AS [At]) D
WHERE N.N <= 120;

-- Contact Us: few rows, only 61-90 days ago.
INSERT INTO [Form_Form_2023_09_12_17_45] ([FormInserted], [FormUpdated], [UserFirstName], [UserLastName], [UserEmail], [UserMessage])
SELECT D.[At], D.[At], N'Seed', N'Contact', CONCAT(N'contact-', N.N, N'@seed.example'), N'Seeded message'
FROM @N N
CROSS APPLY (SELECT DATEADD(minute, -((N.N * 71) % 1440), DATEADD(day, -(61 + (N.N * 13) % 30), @Now)) AS [At]) D
WHERE N.N <= 24;

SELECT N'Coffee sample list' AS [Form], COUNT(*) AS [Rows], MIN([FormInserted]) AS [First], MAX([FormInserted]) AS [Last] FROM [Form_DancingGoat_CoffeeSampleList]
UNION ALL
SELECT N'Contact Us', COUNT(*), MIN([FormInserted]), MAX([FormInserted]) FROM [Form_Form_2023_09_12_17_45]
UNION ALL
SELECT N'Subscription', COUNT(*), MIN([FormInserted]), MAX([FormInserted]) FROM [Form_Form_2023_09_15_10_28];
