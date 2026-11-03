# Slice 1 – Member registrations

Goal, research, definitions and decisions: `PLAN.md`.

## Seeding (local dev DB only, `.agent-resources/report-09-members/`)

Extend `seed-members.sql` (keep it re-runnable) or add `seed-member-roles.sql` run after it: 3–4 member roles (e.g. "Premium", "Partners", "Newsletter only" — code names prefixed `Seed`), ~60% of seeded members in 1–2 roles, uneven sizes. Check NOT NULL columns + FKs first. Never in `src/`.

## Scope

### Server

1. **Page** `MembersPage`, slug `members`, name "Member registrations", icon `Icons.UserFrame` (or the enum member matching `xp-user-frame`, verify), template `@kentico/xperience-labs-simple-stats-admin/Members`. Parent: **Contacts** section (`StatsContactsSection`), order after existing Contacts reports. Derive from `StatsReportPage<>` (Export flag). Permission `StatsPermissions.MEMBERS` = `SimpleStats.Members`, "Member registrations", same pattern (`UIPermission` on app, `UIEvaluatePermission` on page, `PageCommand.Permission`). Nav filtering in `StatsNavigation` must pick it up without special cases (add to its tests).
2. **Filter**: shared `StatsFilter` range + grouping; channel normalized to null (members are global). No extra filters. Do not change other reports' cache keys.
3. **Definitions** (UI hints + usage guide):
   - *New member*: `MemberCreated` in range.
   - *Total members*: members created on or before a date that still exist (deleted members disappear; say so).
   - *External*: `MemberIsExternal` (signed up through an external sign-in provider).
   - *Disabled*: `MemberEnabled = 0` now (current state, not historical).
4. **Queries** (one round trip, parameterized, aggregate in SQL, current + previous period via `StatsComparison.GetPreviousRange`; missing table → empty result + hint, reuse the availability-check pattern):
   - Daily new members, previous period + range, split internal / external; count of members created before the range start (cumulative start).
   - Totals current vs previous: new members, total members at period end, external share of new members (ratio, `null` when 0 new), disabled members (current snapshot, no comparison or comparison omitted — say which).
   - Members by role (current snapshot, TOP N, plus "No role" row), with new-in-range count per role.
5. **Shared infra**: reuse `BuildCumulativeSeries`, `StatsValueComparison` (Count / Ratio with pp change), ranked, combo. Add nothing new unless needed; if you do, make it shared.
6. **Result** (suggested): `MembersResult { From, To, Grouping, Periods, NewMembers (series, internal + external), TotalMembers (cumulative), Totals { NewMembers, TotalMembers, ExternalShare, DisabledMembers }, ByRole, MembersAppUrl, Available, UpdatedAt }`.
7. **Service**: interface + `StatsCache` (5 min, refresh drops key). Tests: builder, service, SQL (0 members, missing table, all external, previous 0 → change null, cumulative start, members without roles, ratio null).

### Client

- Template `members/MembersTemplate.tsx`, export in `entry.tsx` (wrapped with the export permission like the others).
- Filter bar: range + grouping, refresh, updated at.
- KPI row (4): New members, Total members, External sign-ups (%), Disabled members.
- Tile "Member growth": `ComboChart` (stacked or plain columns = new members; if stacking internal/external in the combo is not already supported, use plain columns and put the split in the table — don't grow `ComboChart` for this) + line = total members; `TimeSeriesTable` toggle; CSV.
- Tile "New members by sign-in type": `DonutChart` / `ShareTable` (internal vs external in range), CSV.
- Tile "Members by role": `RankedBarChart` / `RankedTable` (current members per role, new in range as secondary value), CSV. Hint: roles are current state.
- Link "Open members" to native Members app.
- `DataRetentionNote` / hints: definitions above; deleted members disappear; disabled is current state.
- Empty state when there are no members.

## Look and feel, goal, done when

Same as `.agent-resources/report-08-customers/implementation-spec-1.md` ("Look and feel — must feel native", "Goal", "Done when"), with: nav shows the report under Contacts; a role with only this permission lands on it; Export flag hides CSV. `docs/Usage-Guide.md`: report section, permission row, navigation table. Follow `dotnet format`; an inserted switch throw means an uncovered case → keep a throw (`ArgumentOutOfRangeException`) or a handled error state, never a silent default. Do not commit. Report back concise: files, SQL shape, definitions, shared changes, links (or why skipped), seeding, checks, not checked, open questions.
