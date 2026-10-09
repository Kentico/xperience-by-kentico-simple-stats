# Integration tests

Run the real report SQL (repository classes and SQL builders) against SQL Server with small fixed datasets, and check the returned
data and report builder output. Unit tests (`Kentico.Xperience.Labs.SimpleStats.Admin.Tests`) stay fast and need no database.

Covered reports: Campaign sources, Contact stats (activity type filter, tags, heatmap), Publishing activity (first publish vs update),
Web page stats (form URL and host match), Publishing calendar (window, kind and channel filters).

## Run locally

1. Create an empty Xperience database with the `kentico-xperience-dbmanager` tool, **from the repository root**. In a project folder the
   tool also installs sample data (`Data/Template.zip`) and rewrites that project's `appsettings.json` connection string and salt, as it
   would in `examples/DancingGoat`:

   ```powershell
   dotnet tool restore
   dotnet kentico-xperience-dbmanager -- `
     --server-name "localhost,1433" --username sa --password "<sa password>" `
     --admin-password "<any admin password>" `
     --database-name simple-stats-integration-tests --recreate-existing-database
   ```

   This is a setup step: run it once, and again after a product upgrade (the tool version in `.config/dotnet-tools.json`
   follows the Xperience package version). No license key is needed.

1. Point the tests at it and run them:

   ```powershell
   $env:SIMPLESTATS_INTEGRATION_SQL = "Data Source=localhost,1433;Initial Catalog=simple-stats-integration-tests;User ID=sa;Password=<sa password>;Encrypt=False"
   dotnet test tests/Kentico.Xperience.Labs.SimpleStats.Admin.IntegrationTests
   ```

Without `SIMPLESTATS_INTEGRATION_SQL`, every test is skipped (not failed). All tests have the NUnit category `Integration`, so
`dotnet test --filter "TestCategory!=Integration"` leaves them out. CI creates the database in a SQL Server service container and runs them.

## Design

- **Real schema.** The database is created by the product tool, so table and column types, constraints and foreign keys match the
  Xperience version in use. Seeds must satisfy them (see `SeedBuilder`).
- **Seeding.** Each fixture inserts its own rows once (`IntegrationFixture.CreateSeed`) with explicit IDs and fixed dates (never the
  current time), so expected numbers can be written in the test. Only the columns the reports read are set; required unique columns
  get generated code names or `NEWID()`.
- **Cleanup.** Rows are deleted before seeding (in case a run was stopped) and when the fixture ends: all rows of the data tables
  (activities, contacts, channels, content items and their data, tags, forms), and only seeded rows (IDs from 900000) in the tables a
  new database already fills (classes, content folders, workspaces, languages). Transaction rollback is not used: the repositories open
  their own connections through Xperience's `ConnectionHelper`.
- **Safety.** Because whole tables are cleaned, the tests fail fast unless the database name ends with `integration-tests`. Never point
  them at the DancingGoat database.
- **Xperience runtime.** `ConnectionHelper` needs Xperience's service container, so the setup fixture calls `CMSApplication.PreInit`
  with the connection string. The application is not initialized (no modules, no license check).
- **Not covered here.** Email names and scheduled sends: the email tables are left empty, so those branches return no rows.
