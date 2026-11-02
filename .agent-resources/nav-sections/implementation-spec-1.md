# Slice 1 – Navigation sections

Goal, research, definitions and decisions: `PLAN.md`.

## Requirements

- New section pages in `src/Kentico.Xperience.Labs.SimpleStats.Admin/UIPages/` (e.g. `StatsContactsSection`, ... or one file `StatsSections.cs` — follow existing one-page-per-file style). Report pages change `parentType` only; slugs, names, templates, permissions unchanged.
- The application page's default route should land on the first section → its first report (verify it works two levels deep).
- **Permissions**: sections have no permission of their own; per-report permissions unchanged. Verify in the app/by reading the product code:
  1. Role with only e.g. `Customers` permission: nav shows only Commerce > Customers; opening the app / Commerce section lands on Customers, not a 403 on Orders and revenue. If `GetDefaultRoute` receives unfiltered routes, override in a shared base section class that picks the first route the user may open (`IUIPermissionEvaluator`), no hardcoded slugs.
  2. Sections whose children are all denied are hidden (or explain what the product does).
- Links: all internal links must still resolve — `IStatsAdminLinks`, `IPageLinkGenerator.GetPath<T>()`, client `PagePath`, any hardcoded `simple-stats/...` paths in client, tests or docs. Grep for them.
- Tests: update any tests touching page registration/paths; add tests for any permission-aware default route logic.
- `docs/Usage-Guide.md`: describe sections + which report is where. README if it mentions nav.
- Follow `dotnet format`. If it inserts a throw into a switch, a case is uncovered: keep a throw (`ArgumentOutOfRangeException`, see `Shared/StatsPeriods.cs`) or return a handled error state — never a silent default.
- Nothing DancingGoat-specific in `src/`. Do not commit.

## Done when

- `dotnet build` (library + tests), `dotnet test`, `dotnet format --verify-no-changes`, client `npm run typecheck` + `npm run build` pass.
- Visual check in DancingGoat admin if possible (the app runs via `dotnet watch`; server changes may need a restart — say if you couldn't). Otherwise say not checked.
- Report back concise: files changed, whether a default-route override was needed and why, permission verification results, link changes, what was / was not checked.
