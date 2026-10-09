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

### SQL Editor

- VS Code with official [MSSQL extension](https://marketplace.visualstudio.com/items?itemName=ms-mssql.mssql)
- MS SQL Server Management Studio

## Sample Project

### Database Setup

Build the Admin client first (`dotnet: build` needs `Client/dist`): run the `npm: install - Admin/Client` and `npm: build:dev - Admin/Client` VS Code tasks, then start `npm: watch - Admin/Client` alongside `dotnet: watch DancingGoat`.

Running the sample project requires creating a new Xperience by Kentico database using the included template.

Change directory in your console to `./examples/DancingGoat` and follow the instructions in the Xperience
documentation on [creating a new database](https://docs.kentico.com/documentation/developers-and-admins/installation#create-the-project-database).

### Integration Test Database

The integration tests (`tests/Kentico.Xperience.Labs.SimpleStats.Admin.IntegrationTests`) need a separate, empty Xperience database
(not the DancingGoat one). Create it once with the `kentico-xperience-dbmanager` tool, from the repository root, and set
`SIMPLESTATS_INTEGRATION_SQL` to its connection string. See the test project's
[README](../tests/Kentico.Xperience.Labs.SimpleStats.Admin.IntegrationTests/README.md) for the commands. Without the variable, the
integration tests are skipped.

### Admin Customization

`examples/DancingGoat/appsettings.Development.json` runs the Admin customization in Proxy mode, so the admin loads the client from the `npm: watch - Admin/Client` dev server. Keep that task running while DancingGoat runs, or the Simple Stats (Labs) pages will not load. To use the built `Client/dist` bundle instead, remove this section (a rebuild and restart of DancingGoat is then needed to see client changes).

```json
"CMSAdminClientModuleSettings": {
  "kentico-xperience-admin-labs-simple-stats": {
    "Mode": "Proxy",
    "Port": 3009,
    "UseSSL": true
  }
}
```

### System Emails

To test report permissions with other admin users, the invitation emails must be delivered. In Development, DancingGoat sends [system emails](https://docs.kentico.com/x/JQwcCQ) over SMTP to `localhost:1025` (`SystemSmtpOptions` in `appsettings.Development.json`). Run a local SMTP catcher such as [Mailpit](https://mailpit.axllent.org/) and read the emails at <http://localhost:8025>:

```bash
docker run -d --name mailpit --restart unless-stopped -p 1025:1025 -p 8025:8025 axllent/mailpit
```

`SystemEmailOptions.ServiceDomain` is `localhost:48896` (required for invitations; the request host fallback is not used while `AllowedHosts` is `*`). Check delivery in **Email queue → Send test email**.

Emails from the `DancingGoatEmails` channel (newsletters, autoresponders) go to the same catcher, so local sends log real email statistics for the Email summary report.

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
