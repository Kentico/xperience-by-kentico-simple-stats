# Slice 1 – Recipient lists

Goal, research, definitions and decisions: `PLAN.md`.

## Seeding (local dev DB only, `.agent-resources/report-11-recipient-lists/seed-recipient-lists.sql`, re-runnable)

- Add one more recipient list, e.g. `SeedProductNews` / "Product news (seed)". Insert an `OM_ContactGroup` row (`ContactGroupIsRecipientList = 1`, `ContactGroupIsSegment = 0`, fixed GUID) plus a `RecipientListSettings` row (fixed GUID). Check NOT NULL columns + FKs first, and how the native app creates lists (`RecipientListCreate`). The goal is that the native app opens it.
- For contacts with an email (deterministic, CHECKSUM of the contact GUID, as in `seed-consent-agreements.sql`), across both lists:
  - ~40% (DancingGoat list) / ~20% (seed list) subscribe 0–20 days after the contact was created: member row + approved row.
  - ~5% have a member row only (not confirmed / pending double opt-in).
  - ~15% of subscribers unsubscribe 1–40 days later (unapproved row, member row kept); ~25% of those subscribe again 5–30 days after that.
  - Drop events after now.
- Bounces: ~3% of subscribed contacts get an `EmailBounce` row, with a mix of hard bounces and soft bounces at and below the limit of 5.
- Identify seeded rows by deterministic GUIDs (MD5 of `'SEED-RL|contact|list|step'`). For member rows (no GUID) and bounce rows, delete only members of seeded contacts whose rows were all seeded, and bounces whose emails belong to seeded contacts. Delete these first on re-run. Never put seed data in `src/`.

## Scope

### Server

1. **Section + page.** Add a new **Emails** section (`emails`, order 150, between Contacts and Content). The simple-stats PLAN's email summary report will join it later. Follow `.agent-resources/nav-sections/` and the existing section classes, and update the nav tables. Add page `RecipientListsPage`, slug `recipient-lists`, name "Recipient lists", icon matching `xp-user-checkbox` (verify the enum member), template `@kentico/xperience-labs-simple-stats-admin/RecipientLists`, derived from `StatsReportPage<>`. Add permission `StatsPermissions.RECIPIENT_LISTS` = `SimpleStats.RecipientLists`, "Recipient lists", using the same pattern. `StatsNavigation` must pick up the new section and page with no special cases; add tests (only-this-permission lands on it; the section is hidden without it).
2. **Filter.** Wrap `StatsFilter` (range + grouping) and add an optional **recipient list** filter (All or one list ID; options from `OM_ContactGroup` where it is a list, by display name; an unknown ID means All). Reuse the consents filter / `IdSelect` pattern. Normalize channel to null. Do not change other reports' cache keys.
3. **Definitions** (UI hints + usage guide):
   - *Subscription* / *unsubscription*: an approved / unapproved confirmation row in the range. Re-subscribing counts again.
   - *Subscribers* at date D: contacts whose latest row for the list on or before D is approved and who are still members now. Membership has no history, so it is applied as current state; say so. Bounce state is current only, so it is not part of the time series. With All lists: count distinct contacts subscribed to at least one list, as the consents report does for "agreed contacts".
   - *Receiving / Bounced / Unsubscribed / Not confirmed* (current state, per list, at now): the native rules above, plus not confirmed = member with no row. These are the "now" numbers, not range-end numbers. They should equal the native overview.
   - *Unsubscribe rate*: unsubscriptions ÷ subscriptions in the range (ratio, `null` when there are 0 subscriptions; change in pp).
4. **Queries** (one round trip, parameterized, aggregated in SQL, current + previous period; missing tables mean an empty result + hint). Reuse the consents SQL shape where possible; extract shared SQL helpers only if the code would otherwise be copied:
   - Daily subscriptions and unsubscriptions for the previous period + the range (list filter applied).
   - Daily net change in subscribers (`LAG` over (contact, list) ordered by date then ID: not subscribed → subscribed = +1, subscribed → unsubscribed = −1, otherwise 0), restricted to current members, plus the count at range start. This gives the point-in-time series via `BuildCumulativeSeries`. Totals at range end and at previous period end.
   - Per list (ignores the list filter so the table always compares lists; say so in a hint): receiving, bounced, unsubscribed, not confirmed (now), subscriptions + unsubscriptions in the range, subscribers at range end vs previous period end.
   - Status breakdown now (receiving / bounced / unsubscribed / not confirmed, list filter applied; with All lists, count contact + list pairs and say so).
   - Optional, only if cheap: unsubscribes per email in the range from `EmailStatisticsHits` (type 5) joined to the email name. Skip it if it adds noise; the email summary report can own it later.
5. **Shared infra**: reuse what the consents report used (`BuildCumulativeSeries`, `StatsValueComparison` Count / Ratio, ranked snapshot, `IdSelect`, `StackedColumnChart`, `ComboChart` line-only, `DonutChart` / `ShareTable` or `CoverageBarChart` for statuses).
6. **Result** (suggested): `RecipientListsResult { From, To, Grouping, RecipientListId, Periods, Events (subscriptions, unsubscriptions), Subscribers (point-in-time), Totals { Subscriptions, Unsubscriptions, UnsubscribeRate, Subscribers }, ByList, Statuses, ListOptions, RecipientListsAppUrl, Available, UpdatedAt }`.
7. **Service**: interface + `StatsCache`. Tests: builder, service, SQL. Cover:
   - no lists, no rows, missing tables
   - subscribe → unsubscribe → subscribe
   - subscribe twice = one subscriber; unsubscribe without a prior subscribe = no negative
   - member with no rows = not confirmed
   - non-member with an approved row is excluded
   - bounce rules (hard; soft at / below the limit)
   - list filter; unknown list ID → All
   - ratio null
   - non-list contact groups are ignored

### Client

- Template `recipient-lists/RecipientListsTemplate.tsx`, exported in `entry.tsx` (with the export-permission wrapper).
- Filter bar: range + grouping + list select, refresh, updated at.
- KPI row (4): Subscriptions, Unsubscriptions, Unsubscribe rate (pp), Subscribers (at range end vs previous end).
- Tile "Subscriptions and unsubscriptions": `StackedColumnChart`, table toggle, CSV.
- Tile "Subscribers over time": line, table (no total column), CSV.
- Tile "Recipient lists" (full width): `RankedTable` (receiving, bounced, unsubscribed, not confirmed, subscriptions, unsubscriptions), chart toggle (`RankedBarChart` of receiving), a link per list, CSV.
- Tile "Subscriber status": donut / share table of the current statuses. Hint: matches the native overview; not confirmed = double opt-in pending or added without confirmation.
- Link "Open recipient lists" to the native app.
- Hints / `DataRetentionNote`: the definitions; deleted contacts' history disappears; custom code that deletes confirmation rows hides unsubscribes; membership and bounces are current state.
- Empty state when there are no recipient lists or no subscriptions.

## Look and feel, goal, done when

Same as `.agent-resources/report-08-customers/implementation-spec-1.md` ("Look and feel — must feel native", "Goal", "Done when"), with these additions:

- The nav shows the new Emails section with this report; a role with only this permission lands on it; the Export flag hides CSV.
- The current statuses match the native Recipient lists overview for both lists.
- `docs/Usage-Guide.md`: report section, permission row, navigation table (new section).
- Follow `dotnet format`: an uncovered switch case throws `ArgumentOutOfRangeException` or returns a handled error state, never a silent default.
- Do not commit.

Report back concise: files, SQL shape, definitions chosen, shared changes, links (or why skipped), seeding, checks, not checked, open questions.
