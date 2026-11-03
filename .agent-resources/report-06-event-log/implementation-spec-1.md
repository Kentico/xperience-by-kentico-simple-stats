# Slice 1 – Event log

Goal, research, definitions and decisions: `PLAN.md`.

## Uncommitted work in progress (keep it)

Per-report permissions are done but uncommitted: `UIPages/StatsPermissions.cs`, `[UIPermission]` on `StatsApplicationPage`, `[UIEvaluatePermission]` + `PageCommand.Permission` on each page, `docs/Usage-Guide.md` permission table. Add `EVENT_LOG` (`SimpleStats.EventLog`, "Event log") the same way. Do not revert or rework the other pages.

## Scope

### Server (C#)

1. **Page** `EventLogPage`, slug `event-log`, name "Event log", order 600, icon (verify in `Icons`), template `@kentico/xperience-labs-simple-stats-admin/EventLog`. Permission `StatsPermissions.EVENT_LOG`. No channel → normalize channel to null for the cache key.
2. **Filter**: reuse `StatsFilter` range + grouping. Add event type filter (All / Information / Warning / Error). Pick the cleanest way without changing reports 01–05 behavior or cache keys: e.g. an `EventLogFilter`/load request that wraps `StatsFilter` + `EventType?`, normalized (unknown → all). Say why.
3. **Queries** (repository), parameterized SQL, aggregate in SQL, one round trip (multiple result sets OK), current + previous period (`StatsComparison.GetPreviousRange`):
   - Daily counts by `EventType` in range → time series (fixed series I/W/E in stable order, stable colors: error/warning/info). Reuse `StatsTimeSeriesBuilder.BuildFixed` / shared time series.
   - Totals per type, current vs previous → `StatsComparison` per type.
   - Top sources (`TOP N`, with previous period count → change), top event codes (same). Respects type filter.
   - Top users by event count; system events (no `UserID` / empty `UserName`) as their own "System" row. Respects type filter.
   - Verify column names/types/nullability (`EventType` char? `EventTime` datetime2? `UserID` nullable?). Map type codes via a product constant/enum if one exists (e.g. `EventType` class in `CMS.Core`); say what you found.
   - Add an index note only if relevant (don't create indexes). Cap range via `StatsFilter.MaxRangeDays` as usual.
4. **Shared ranked + change**: sources/codes need "change vs previous". Check if anything fits; else add an opt-in field on `StatsRankedEntry`/`StatsRankedItem` (e.g. `PreviousValue` + `Change`) or a small shared helper, without changing reports 01–05 output. Reusable later (email summary top/bottom performers). Unit tests.
5. **Admin links** via `IStatsAdminLinks` only: link to the native Event log application (and, if simple, to a filtered listing or an event detail). If no public page type is usable, skip and say why. Optional: user row → user edit page if simple.
6. **Result** (suggested): `EventLogResult { From, To, Grouping, EventType, Periods, Series, Totals (per type comparison), TopSources (ranked+change), TopCodes (ranked+change), TopUsers (ranked), Total, LogSizeLimit, UpdatedAt }`.
7. **Service**: interface + `StatsCache`, same rules (5 min, refresh drops key, key by normalized filter). Builder + service tests (empty log, only system events, type filter, previous period math, change null when previous 0). Reports 01–05 tests still pass.

### Client (React/TS)

- Template `event-log/EventLogTemplate.tsx`, export in `entry.tsx`.
- Filter bar: range + grouping + event type toggle (reuse the shared option-toggle from report 05 if it fits; else generalize it in `shared/`), refresh, updated at.
- KPI row: total events, errors, warnings, information — each with change vs previous period (e.g. "Errors +40% vs previous 30 days"). For errors/warnings an increase is bad: show direction neutrally or with a semantic color if the design system has one; don't invent colors. Put a small reusable KPI-with-change piece in `shared/` if none exists.
- Tile "Events over time": `StackedColumnChart` (I/W/E stacked, fixed colors from palette/tokens: error, warning, info) / `TimeSeriesTable`, CSV.
- Tile "Top sources" and "Top event codes": `RankedBarChart` / `RankedTable` with a change column, CSV. Each on its own full-width row (user feedback: side by side too dense).
- Tile "Top users": `RankedTable` (System row included), CSV.
- Link/button to the native Event log app if the server gives a path.
- `DataRetentionNote`: event log is trimmed at `CMSLogSize` rows (value from server), so old events disappear and long-range trends drop.

## Look and feel — must feel native

Same rules as earlier reports. References: `../xperience-by-kentico-admin-design-components` (`AGENTS.md`, `src/components/Charts/*`, `src/components/Table`, `src/templates/DashboardTemplate`, `previews/*.png`, `src/styles/tokens.css`), `../community-portal/src/Kentico.Community.Portal.Admin/Client/src/features/reports/` (amCharts examples only, not a spec). Prefer `@kentico/xperience-admin-components` exports; verify props in `node_modules/@kentico/xperience-admin-components/dist/*.d.ts`. CSS only via design tokens. Keep admin `Table` cells plain single-line (earlier CSS overrides looked wrong). Tile layout like other reports.

## Goal

Shareable across customer projects: nothing DancingGoat-specific in `src/`, no hardcoded IDs/URLs/source names, values parameterized, works with an empty log, only system events, one type.

## Done when

- `npm run typecheck` + `npm run build` pass in `src/Kentico.Xperience.Labs.SimpleStats.Admin/Client`.
- `dotnet build` of `Kentico.Xperience.Labs.SimpleStats.slnx` + `dotnet test` pass; new tests.
- DancingGoat admin: nav shows 6 reports; event log report renders seeded data; type filter, toggles, CSV, links work; reports 01–05 unchanged. Admin client uses Proxy mode (port 3050): `npm run watch` in Client must run, or rebuild + restart for client changes. Visual check if the app can be run (`docs/Contributing-Setup.md`, `.vscode/tasks.json`); otherwise say not checked.
- `docs/Usage-Guide.md`: report section + permission table row.
- Do not commit. Report: files changed, SQL shape, column/constant findings, filter/request decision, ranked-change approach, admin link approach (or why skipped), seeding done, what was / was not visually checked.
