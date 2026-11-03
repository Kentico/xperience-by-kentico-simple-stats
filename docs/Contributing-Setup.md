# Contributing Setup

## Required Software

The requirements to setup, develop, and build this project are listed below.

### .NET Runtime

.NET SDK 10.0 or newer

- <https://dotnet.microsoft.com/en-us/download/dotnet/10.0>
- See `global.json` file for specific SDK requirements

### Node.js Runtime

- [Node.js](https://nodejs.org/en/download) LTS or newer
- [NVM for Windows](https://github.com/coreybutler/nvm-windows) or [NVM for macOS](https://github.com/nvm-sh/nvm) to manage multiple installed versions of Node.js
- Node.js 24 or newer; see `engines` in `src/Kentico.Xperience.Labs.SimpleStats.Admin/Client/package.json`

### C# Editor

- VS Code/VS
- Cursor
- Rider

### Database

SQL Server 2019 or newer compatible database

- [SQL Server Linux](https://learn.microsoft.com/en-us/sql/linux/install-upgrade/setup?view=sql-server-ver15)

### Aspire

Optional; runs the sample project and its dependencies together (see [Run with Aspire](#run-with-aspire)).

- [Aspire CLI](https://aspire.dev/get-started/install-cli/)
- [Docker](https://docs.docker.com/get-started/get-docker/) for the SQL Server and Mailpit containers

### Email

[Mailpit](https://mailpit.axllent.org) catches DancingGoat's emails on `localhost:1025` (SMTP), with the inbox at <http://localhost:8025>. One container serves every project on the machine:

```powershell
docker run -d --name mailpit --restart unless-stopped -p 1025:1025 -p 8025:8025 axllent/mailpit
```

In Development, DancingGoat sends [system emails](https://docs.kentico.com/x/JQwcCQ) and every email channel without its own configuration over SMTP to Mailpit (`SmtpOptions` in `examples/DancingGoat/appsettings.Development.json`). For Simple Stats this matters in two places:

- Testing report permissions with other admin users needs their invitation emails. `SystemEmailOptions.ServiceDomain` is `localhost:48896` (required for invitations; the request host fallback is not used while `AllowedHosts` is `*`). Check delivery in **Email queue → Send test email**.
- Emails from the `DancingGoatEmails` channel (newsletters, autoresponders) go to Mailpit too, so local sends log real email statistics for the Email summary report.

### SQL Editor

- VS Code with official [MSSQL extension](https://marketplace.visualstudio.com/items?itemName=ms-mssql.mssql)
- MS SQL Server Management Studio

## Sample Project

### Database Setup

Build the Admin client first (`dotnet: build` needs `Client/dist`): run the `npm: install - admin client` and `npm: build:dev - admin client` VS Code tasks, then start `npm: watch - admin client` alongside `dotnet: watch DancingGoat`.

Running the sample project requires an Xperience by Kentico database. AI agents can do the whole local setup (SQL Server, database, dependencies, verification) with the `setup-local` skill (`.claude/skills/setup-local/SKILL.md`). To create the database by hand, run from the repository root:

```powershell
./scripts/Initialize-Database.ps1
```

- Server, database (`xperience-by-kentico-simple-stats`), and SQL login come from `ConnectionStrings` in `examples/DancingGoat/appsettings.json`. The default targets a Docker SQL Server on `localhost,1433` with the `sa` login; for a locally installed SQL Server pass `-ServerName` (and `-Username ""` for Windows authentication).
- The `administrator` account password is `Pass@12345`, or the `XPERIENCE_ADMIN_PASSWORD` environment variable.
- Pass `-LicenseFile <path>` (or set `XPERIENCE_LICENSE_FILE`) to install your Xperience license. Without it, add the license when you first sign in to the administration.
- `-Recreate` drops and recreates an existing database.
- The script runs [`kentico-xperience-dbmanager`](https://docs.kentico.com/documentation/developers-and-admins/installation#create-the-project-database), which writes the connection string and a new `CMSHashStringSalt` to `examples/DancingGoat/appsettings.json`. The script then moves both to DancingGoat's user secrets and restores `appsettings.json`, so nothing local gets committed. A `CMSHashStringSalt` already in user secrets is reused. `-SaveTo` changes where they go; see the script's help.

After creating the database, set the channel domains as in step 7 of the `setup-local` skill so emails link to the local site.

### Run with Aspire

[Aspire](https://aspire.dev) starts DancingGoat and the Admin client dev server (Proxy mode) together, with a link to the Mailpit inbox. From the repository root:

```powershell
aspire run
```

The dashboard links the live site and the administration. By default DancingGoat uses the database from [Database Setup](#database-setup). To use a SQL Server container instead (Docker required; the database is created on first run), set `Sql:UseExistingServer` to `false` in `examples/AppHost` user secrets:

```powershell
dotnet user-secrets set "Sql:UseExistingServer" false --project examples/AppHost
```

Set `XPERIENCE_LICENSE_FILE` before `aspire run` to install the license into a new container database. After its first run, set the channel domains as in step 7 of the `setup-local` skill (`.claude/skills/setup-local/SKILL.md`) so emails link to the local site.

By default DancingGoat sends emails to the machine-wide Mailpit from [Email](#email), and the dashboard shows it as unhealthy when it isn't running. To start a Mailpit container for the run instead, set `Mail:UseExistingServer` to `false`:

```powershell
dotnet user-secrets set "Mail:UseExistingServer" false --project examples/AppHost
```

The root `.mcp.json` registers the Aspire MCP server (`aspire agent mcp`), so AI agents can list resources, read logs, and restart them. `aspire agent init` can also install Aspire's agent skills; note that it adds Aspire telemetry hooks to your global agent settings unless `ASPIRE_CLI_TELEMETRY_OPTOUT=true` is set.

### Integration Test Database

The integration tests (`tests/Kentico.Xperience.Labs.SimpleStats.Admin.IntegrationTests`) need a separate, empty Xperience database
(not the DancingGoat one). Create it once with the `kentico-xperience-dbmanager` tool, from the repository root, and set
`SIMPLESTATS_INTEGRATION_SQL` to its connection string. See the test project's
[README](../tests/Kentico.Xperience.Labs.SimpleStats.Admin.IntegrationTests/README.md) for the commands. Without the variable, the
integration tests are skipped.

### Admin Customization

`examples/DancingGoat/appsettings.Development.json` runs the Admin customization in Proxy mode, so the admin loads the client from the `npm: watch - admin client` dev server. Keep that task running while DancingGoat runs, or the Simple Stats (Labs) pages will not load. To use the built `Client/dist` bundle instead, remove this section (a rebuild and restart of DancingGoat is then needed to see client changes).

```json
"CMSAdminClientModuleSettings": {
  "kentico-xperience-labs-simple-stats-admin": {
    "Mode": "Proxy",
    "Port": 3050,
    "UseSSL": true
  }
}
```

### End-to-end tests

Playwright tests for the live site and the administration live in `tests/e2e`; they need a licensed database. See [`tests/e2e/README.md`](../tests/e2e/README.md).

## Development Workflow

1. Create a new branch with one of the following prefixes
   - `feat/` - for new functionality
   - `refactor/` - for restructuring of existing features
   - `fix/` - for bugfixes

1. Run `dotnet format` against the `Kentico.Xperience.Labs.SimpleStats` solution

   > use `dotnet: format` VS Code task.

1. Commit changes, with a commit message preferably following the [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/#summary) convention.

1. Once ready, create a PR on GitHub. The PR will need to have all comments resolved and all tests passing before it will be merged.
   - The PR should have a helpful description of the scope of changes being contributed.
   - Include screenshots or video to reflect UX or UI updates
   - Indicate if new settings need to be applied when the changes are merged - locally or in other environments
   - Use the `populate-pr-template` agent skill (`.claude/skills/populate-pr-template/SKILL.md`, works with
     both Copilot and Claude) to fill in `.github/PULL_REQUEST_TEMPLATE.md` from git diff and CI evidence
     instead of writing the description by hand.

1. This repository uses `lf` line endings for text files. EditorConfig and Git enforce this on all platforms; no Git line-ending override is needed.
