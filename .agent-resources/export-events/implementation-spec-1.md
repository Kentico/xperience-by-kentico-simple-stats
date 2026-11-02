# Slice 1 – Export events

Goal, research, definitions and decisions: `PLAN.md`.

## Current client export

The CSV is built and downloaded in the browser only:

- `Client/src/shared/csv.ts`: `toCsv` / `toRankedCsv` / `toTimeSeriesCsv` / `toShareCsv` / `toCoverageCsv` / `toAgedCsv` build the text, and `downloadCsv(fileName, csv)` saves it.
- `Client/src/shared/StatsTile.tsx`: the "Export CSV" button calls the template's `onExportCsv`. It is shown only when `onExportCsv` is set and `useCanExport()` is true (`shared/exportPermission.tsx`, set from `StatsReportClientProperties.CanExport`).
- There are 30 `downloadCsv` calls in 10 templates (`rg -n "downloadCsv\(" Client/src`).
- Commands go through `shared/useStatsCommand.ts` (`usePageCommandProvider().executeCommand`).

## Scope

### Server

1. **Event**: `AfterExportStatsEvent : AsyncEvent<StatsExportEventData>` (public, sealed) in the library's root or `Admin` namespace (pick one, and match the namespace the docs use). Library code can construct it, so give it a public constructor that takes the data. Copy the product's shape so one handler class can handle both events:
   - `ReportPageTypeName` (string): full type name of the report page, for example `Kentico.Xperience.Labs.SimpleStats.Admin.UIPages.ConsentsPage` (like `ListingPageTypeName`).
   - `UserID` (int): the current admin user, or 0 if unknown. Use the same source the product uses (`IAuthenticatedUserAccessor`; verify it is public, else use a public alternative).
   - `Timestamp` (DateTime): server local time, from `TimeProvider` (already injected in pages).
   - `ExportName` (string): a stable ID of the tile / file, for example `consents-events`. The client sends it. Validate it as a short slug (max length, `[a-z0-9-]`) and reject anything else.
   - `FileName` (string): the downloaded file name, validated (max length, no path characters).
   - `RowCount` (int): data rows, not counting the header; must be ≥ 0.
   - Optional: `Filter` (the report's normalized filter as a read-only string → string dictionary, or the serialized filter JSON) so audits can show what was exported. Include it only if each page can provide it cheaply and generically (for example, the client sends the filter it used and the page normalizes it the same way as `LOAD`). Otherwise skip it and say why.
2. **Command**: one shared `LOG_EXPORT` page command in `StatsReportPage<>`, so every report gets it with no per-page code. It requires `StatsPermissions.EXPORT`.
   - Verify that the admin discovers `[PageCommand]` methods declared on a base class. If it does not, find the smallest shared alternative (for example, a protected helper plus a one-line command per page) and say which.
   - The command's report check: a user with Export but without the report permission must not be able to log (or reach) exports for that page. `UIEvaluatePermission` on the page should already cover this; verify it, and add a test if the page-command test setup allows.
   - The command invokes the registered handlers: resolve `IEnumerable<IAsyncEventHandler<AfterExportStatsEvent>>` and await each `HandleAsync`, the same way the product does. Check whether `CMS.Base` has a public publisher / `InvokeAsync` helper and use it if so. A handler exception is logged (`ILogger`) and does not fail the command.
   - Return an empty success response; the client ignores the result.
3. **Registration**: nothing needed for the event type. Developers register handlers with `AddEventHandler<AfterExportStatsEvent, THandler>()` (verify it works with a non-product event type).
4. **Tests**: the command raises the event with the right page type, user, timestamp, export name, file name and row count; invalid input is rejected; a handler exception does not fail the command; no handlers = no error; the EXPORT permission is required on the command (attribute test, like existing permission tests).

### Client

1. Change the shared export path so every export also calls `LOG_EXPORT`. Pick the smallest change that needs no per-template copy-paste. Suggested: `downloadCsv` stays a pure helper, and a new shared hook (for example `useCsvExport()` in `shared/`) returns `exportCsv(exportName, fileName, csv, rowCount)`. It downloads first, then fires the command without awaiting it (fire-and-forget; a failure only logs to the console and never blocks or shows an error toast). Rows: count them from the builder input, or have the builders return `{ csv, rowCount }`. Pick one, keep it simple, and say which.
2. Update all 30 call sites. `exportName` is the file-name prefix without dates (for example `consents-events`), so it stays stable across ranges.
3. Do not log when the button is hidden (no Export permission); the server rejects it anyway.

### Docs

- `docs/Usage-Guide.md`: add a section "Audit CSV exports" with:
  - the event, its data and a handler example (like the product docs example, `ILogger` with `EventId(0, "EXPORT")`), and registration;
  - a note that one handler class can implement both `IAsyncEventHandler<AfterExportListingEvent>` and `IAsyncEventHandler<AfterExportStatsEvent>`;
  - the best-effort caveat (client-built CSV; a user who can see a report can copy its data);
  - an `ExportName` list per report (a table, or "the file-name prefix").
- README: one line under features if it lists them.

## Done when

- `npm run typecheck` and `npm run build` pass in `Client`.
- `dotnet build` and `dotnet test` pass on `Kentico.Xperience.Labs.SimpleStats.slnx`, with the new tests.
- `dotnet format --verify-no-changes` passes. An uncovered switch case throws `ArgumentOutOfRangeException` or returns a handled error state, never a silent default.
- DancingGoat check, if the app can run: add a temporary handler in `examples/DancingGoat` only (or keep a small sample handler there if it helps developers, and say so) that logs to the event log. Export from 2–3 reports and confirm the event log rows. DancingGoat uses Proxy mode (3050): `npm run watch` must run, else rebuild + restart. If it can't run, say "not checked".
- Nothing DancingGoat-specific in `src/`. Do not commit.

Report back concise: files, event shape, command placement (base class worked or not), how row count is computed, filter included or why not, checks, not checked, open questions.
