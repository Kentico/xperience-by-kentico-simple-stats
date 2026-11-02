# Slice 1 – Email summary

Goal, research, definitions and decisions: `PLAN.md`.

## Seeding (local dev DB only, `.agent-resources/report-12-email-summary/seed-email-statistics.sql`, re-runnable)

The report needs ~12–20 sent Regular emails. Emails are channel content items (all the `CMS_Content*` tables), so **the seed does not create emails** (`PLAN.md` Decisions 3).

- **Reference send (user, first)**: send one Regular email to the DancingGoat recipient list through Mailpit, then open it and click links as several contacts. Before writing the seed, look at every row it created or changed:
  - `SendConfiguration`
  - `EmailMarketingRecipient`
  - hits: types, times, MailoutGUIDs, link IDs
  - `EmailLink`
  - `EmailStatistics` before and after the recalculation
  - email activities in `OM_Activity`
  The seed must write the same shape (columns, values, relations) at a larger scale. Never change these real rows.
- **What the real send showed (2026-10-01)**: the user cloned 13 newsletters (IDs 8–20; #12 = ID 20 is unsent; IDs 17 and 18 are both named "#10") and sent them to the DancingGoat list. They also clicked links in "Dancing Goat Regular" (ID 2) as several contacts.
  - A send sets `SendConfiguration` Status 3 + ScheduledTime = the send time. It also sets the content dates: metadata Created/Modified, CommonData First/LastPublished, and `EmailConfigurationLastModified`.
  - Each email got 145 sent hits (one per receiving member), unprocessed until recalculation. `EmailMarketingRecipient` rows are deleted after the send. There are 3 `EmailLink` rows per clone.
  - Clicks store `EmailLinkID`; opens have none.
  - `emailclick` activities: `ActivityItemID` = EmailConfigurationID, `ActivityItemDetailID` = EmailLinkID, `ActivityValue` = link URL, `ActivityChannelID` = email channel, title "Clicked link in email '<name>'".
  - There are no open activities.
- **Seed as built** (`.agent-resources/report-12-email-summary/seed-email-statistics.sql`, run 2026-10-01; this replaces the clone/link steps below):
  - It moves the send and content dates of the sent `(seed)` emails to every 14 days, the newest 7 days ago (Apr 23 – Sep 24). It shifts the real sent hits by the same amount.
  - On the real mailout GUIDs it generates opens (45–57%; lag 50% < 3 h, up to 7 days; some repeat opens), clicks (9–20%; 8% with no open hit) and 0–2 unsubscribes, plus 0–1 hard bounces as hits.
  - It deletes the seed emails' statistics rows and recalculates them with the product proc.
  - Result: open rate 48.6–59.3%, click rate 8.3–18.6%. Re-runs give identical results.
  - It does not seed `emailclick` activities (optional later).
  - Update 2026-10-01: real opens/clicks (user, on #10 ID 17 and #12 ID 20) have the same MailoutGUID as the email's sent hits, and one `emailclick` activity per click hit. Malformed links (`https://localhost/articles` in clones 8–19) are still tracked. Seeded hits are now marked by a time fraction of exactly 700 ns. Re-runs delete only marked hits, move real hits and `emailclick` activities with the send date, and skip mailouts that have real engagement. #12 (ID 20) is included (newest, Sep 25). #13 (ID 21) is unsent.
- **Manual step (user)**: clone "Dancing Goat Regular" ~12–16 times with the native Clone action. Name the clones `Newsletter #N (seed)` and publish them, but do not send them.
- The seed picks emails whose primary-language display name ends with ` (seed)`. With none, it prints a message and stops. Sort by N: newsletter n gets the send date now − 14 × (count − n) days.
- It updates the clone's existing `SendConfiguration` row: Status 3, ScheduledTime = send date, RecipientListID = the DancingGoat list. This is the only change to product-created rows, and only for `(seed)` emails. Check first how the row looks after the clone + publish. If there is no row, insert one with a fixed GUID.
- If the clone has no `EmailLink` rows, it inserts 2–3 with fixed GUIDs.
- Done when the native email list shows them as Sent with a send date, and the Statistics tab opens with the seeded numbers. If status 3 without a real send breaks the native UI, stop and report.
- Use fixed GUIDs (MD5 of `'SEED-EM|configGUID|step'`) for everything the seed inserts.
- Hits per newsletter, from contacts with an email, picked the same way as in `seed-recipient-lists.sql` (CHECKSUM):
  - sent: ~120–140 mailouts at the send time
  - opens: 45–57% of mailouts, 0–48 h later, some with 2–3 opens
  - clicks: 9–20% of mailouts, on one of the email's links (seed `EmailLink` rows)
  - unsubscribes: 0–2
  - hard bounces: 0–1
  - Keep a few clicks without an open hit, so the "click counts as open" rule is tested.
- Automation emails: no seed. The user's real Mailpit sends (`PLAN.md` Decisions 4) cover them.
- Statistics rows: compute them in the seed from the seeded hits, with the same rules as the proc. Insert the hits as processed.
  - Then run `EXEC Proc_EmailLibrary_EmailStatistics_RecalculateEmailStatistics @EmailConfigurationID = n` for one email, as a check that the numbers stay the same.
- Drop events after now. On re-run, delete the seeded hits, statistics rows and links of `(seed)` emails (hits and stats rows have no GUID; the target emails are seed-only), then insert them again. Never delete content items. Never put seed data in `src/`.

## Scope

### Server

1. **Page**: `EmailSummaryPage`, slug `email-summary`, name "Email summary", order before Recipient lists, icon `Icons.Graph` (check the enum member), template `@kentico/xperience-labs-simple-stats-admin/EmailSummary`, derived from `StatsReportPage<>`.
   - Permission `StatsPermissions.EMAIL_SUMMARY` = `SimpleStats.EmailSummary`, "Email summary".
   - `StatsNavigation` tests: only-this-permission lands on it; with both email permissions, Email summary comes first.
2. **Filter**: `StatsFilter` (range + grouping) plus an optional **email channel** filter, through the existing channel filter with email channels only. With one email channel, hide it. Do not change other reports' cache keys.
3. **Definitions** (UI hints + usage guide):
   - *Sent in range* (Regular emails): `SendConfigurationStatus` Sent or Sending, and `ScheduledTime` in the range. This sets the scope for KPIs, the email table and top/bottom.
   - *Email numbers* are the lifetime totals from the statistics table, the same as the native Statistics tab. Opens and clicks that come after the range still count toward an email sent in the range; say so.
   - *Open rate / click rate* = unique ÷ delivered. *Delivery rate* = delivered ÷ sent. *Bounce, unsubscribe and spam rate* = ÷ sent. When the denominator is 0, the rate is `null`. Aggregate rates use **sums** (Σ unique opens ÷ Σ delivered), not the average of the per-email rates. Change is shown in pp.
   - Soft bounces, hard bounces and spam can be NULL (not tracked by the provider). Show "-" and leave them out of the sums. If every row is NULL, hide that column or KPI.
   - *Activity over time*: from hits, all purposes. Count of sent hits per bucket; unique opens = distinct mailouts with an open or click hit in the bucket; same for clicks and unsubscribes. The buckets are when people acted, not cohorts by send date.
4. **Queries** (one round trip, parameterized, aggregated in SQL, current + previous period; if tables are missing, return an empty result + a hint):
   - Regular emails sent in the range and in the previous period: id, name, channel, language code, send date, recipient list, and the statistics columns.
   - Automated emails (every purpose except Regular): lifetime statistics + sent hits in the range. Show only emails with any statistics.
   - Daily activity from hits for the previous period + the range (channel filter through `EmailConfigurationEmailChannelID`).
   - Optional, only if cheap: top links by unique clicks in the range (`EmailLink` target + description). Skip it if it adds noise.
5. **Top / bottom performers**:
   - Rank Regular emails sent in the range by open rate and by click rate. Show the top 5 and bottom 5, using the existing ranked snapshot.
   - Leave out emails with fewer than N delivered (constant, e.g. 10), and say so in a hint.
6. **Result** (suggested): `EmailSummaryResult { From, To, Grouping, ChannelId, Periods, Totals { Emails, Sent, Delivered, DeliveryRate, OpenRate, ClickRate, HardBounces, SoftBounces, Unsubscribes, UnsubscribeRate, SpamReports } (each a StatsValueComparison), Activity (sent, opens, clicks, unsubscribes series), Emails (rows + StatisticsUrl), Automated (rows), TopByOpenRate, BottomByOpenRate, TopByClickRate, BottomByClickRate, ChannelOptions, EmailsAppUrl, Available, UpdatedAt }`.
7. **Service**: interface + `StatsCache`. Tests: builder, service, SQL. Cover:
   - no emails, no statistics, missing tables
   - NULL bounce/spam columns
   - rate null when delivered is 0
   - rate over 100%
   - sum vs average aggregation
   - Draft/Scheduled emails excluded
   - send date on the range edges
   - a click without an open hit counts as a unique open
   - the minimum-delivered threshold for top/bottom
   - channel filter; unknown channel → All
   - link parameters

### Client

- Template `email-summary/EmailSummaryTemplate.tsx`, exported in `entry.tsx` (with the export-permission wrapper).
- Filter bar: range + grouping (+ channel when there is more than one), refresh, updated at.
- KPI row: Emails sent (count of emails), Sent, Open rate, Click rate. Second row: Delivery rate, Hard bounces, Unsubscribe rate, Spam reports (hide when NULL). Reuse `ComparisonInfoCard`.
- Tile "Email activity over time": `ComboChart` lines (sent, unique opens, unique clicks) or `StackedColumnChart`, table toggle, CSV.
- Tile "Emails sent in range" (full width): `RankedTable` with name (link to Statistics), send date, sent, delivered, open rate, click rate, hard bounces, unsubscribes. Chart toggle: `RankedBarChart` of open rate. CSV.
- Tile "Top and bottom performers": open rate / click rate switch, top 5 + bottom 5.
- Tile "Automated emails": lifetime table (purpose, sent in range, lifetime sent, open rate, click rate), with a link to Statistics. Hint: these are lifetime numbers, not limited to the range.
- Link "Open emails" (the native email list for the channel).
- Hints / `DataRetentionNote`: the definitions; rates follow the Statistics tab (the email list uses sent); bounces and spam depend on the delivery provider; deleting an email deletes its statistics.
- Empty state when no Regular email was sent in the range.

## Look and feel, goal, done when

Same as `.agent-resources/report-08-customers/implementation-spec-1.md`, plus:

- For 3 seeded newsletters and 1 automation email, the numbers match the native Statistics tab.
- The links open the right Statistics tab. Check the generated path against `/admin/emails-1/en/list/{id}/statistics`.
- Run the SQL read-only against `Kentico.Community` once. The uniques must match its statistics table for the 5 emails with hits.
- `docs/Usage-Guide.md`: report section, permission row, navigation table.
- A real local send (Mailpit) shows up in the report after recalculation, and its numbers match the Statistics tab.
- Follow `dotnet format`: an uncovered switch case throws `ArgumentOutOfRangeException` or returns a handled error state, never a silent default.
- Do not commit.
