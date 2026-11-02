# Plan: Report 11 – Recipient lists

## Goal

Eleventh report. Audience: email marketers. simple-stats PLAN "Later candidates → Emails → Recipient list growth and unsubscribes over time". Time-range report. Read `.agent-resources/simple-stats/PLAN.md`, `.agent-resources/report-10-consents/` (closest: event-history table, latest-row-per-contact state, point-in-time series via `LAG` + `BuildCumulativeSeries`), `.agent-resources/report-09-members/` (DB access) and `.agent-resources/nav-sections/` first. Build on existing code, mostly the consents report; do not duplicate. Keep complexity low.

## Research

### Product model (decompiled 31.9, checked 2026-10-01)

Recipient lists are static contact groups: `OM_ContactGroup` with `ContactGroupIsRecipientList = 1` (`ContactGroupInfo`, `CMS.ContactManagement`). Two tables hold the state:

- `OM_ContactGroupMember` (`ContactGroupMemberInfo`): current membership only, no timestamps. Use `ContactGroupMemberType = 0` (contact) only. The product does **not** delete the member row on unsubscribe.
- `EmailLibrary_EmailSubscriptionConfirmation` (`EmailSubscriptionConfirmationInfo`, `CMS.EmailMarketing`): `...ContactID` (FK OM_Contact), `...RecipientListID` (FK OM_ContactGroup), `...IsApproved` bit, `...Date` datetime2 NOT NULL, `...GUID`. Indexes on ContactID and RecipientListID. `EmailSubscriptionConfirmationService` (internal) `Confirm` inserts a row (IsApproved 1, date now) and `Revoke` inserts a row (IsApproved 0). `IsConfirmed` = latest row by date for contact + list is approved. **An event history, like `CMS_ConsentAgreement`.**
- Unsubscribe: `KenticoEmailUnsubscriptionController` calls `Revoke` only if confirmed, and logs an `EmailStatisticsHits` row of type `Unsubscribe` (5) tied to the email, with no contact. The member row stays.
- Double opt-in (verified with a real form subscription on 2026-10-01, contact `dana@kentico.local`): the form adds the member row first, and the confirmation link click calls `Confirm`. Between those two steps the member has **no** confirmation row.
- Native list statistics (`RecipientListStatisticsService`, `RecipientListOverview`):
  - **Receiving** = members whose latest row is approved and who are not bounced.
  - **Bounced** = members whose latest row is approved and who are bounced.
  - **Unsubscribed** = members whose latest row is not approved.
  - **Bounced email**: `EmailLibrary_EmailBounce` (`EmailBounceInfo`) joined on `ContactEmail = EmailBounceEmailAddress`, with `EmailBounceIsHardBounce = 1` or `EmailBounceSoftBounceCount >= SoftBounceLimit`. The limit is `BouncedEmailsGlobalOptions.SoftBounceLimit`, default 5, set in app options (read it through `IOptionsMonitor<BouncedEmailsGlobalOptions>` and pass it as a parameter; verify the type is public).
  - **Match the native numbers** for current state so the report and the native overview agree. Members with no confirmation row are counted in none of the three native statuses; report them as "Not confirmed".
- Contact deletion: `Proc_OM_Contact_MassDelete` deletes confirmation rows and member rows (checked). Contact merge (`EmailSubscriptionMergeService`) moves confirmation rows to the target contact. Deleted contacts' history disappears; say so.
- Custom code can bypass the history. Example: the Community Portal `RecipientListManager` (github.com/Kentico/community-portal, `src/Kentico.Community.Portal.Core/Membership/RecipientListManager.cs`) inserts the member + an approved row on subscribe, but **deletes** both on unsubscribe. Those unsubscribes leave no trace. Add a hint/usage-guide note: subscriptions and unsubscriptions count only what is stored in `EmailSubscriptionConfirmation`.
- Native app: Recipient lists, `Kentico.Xperience.Application.RecipientLists`, slug `recipient-lists`, icon `xp-user-checkbox`. Per list: `RecipientListList` (`list`) → `:` `RecipientListEditSection` → `overview` (`RecipientListOverview`, default) / `subscribers` (`RecipientListSubscriberList`). The user confirmed the URL `/admin/recipient-lists/list/2`, where 2 is the contact group ID. Page types (user-confirmed, all `public sealed` in `Kentico.Xperience.Admin.DigitalMarketing.UIPages`): `RecipientListList` is the overall listing ("Open recipient lists" link), and `RecipientListEditSection` (`EditSectionPage<ContactGroupInfo>`) is one list, with the contact group ID as its parameter. Build both links in `IStatsAdminLinks` with `IPageLinkGenerator` (same approach as the existing links; check how consents passes the section parameter).

### Local data (checked 2026-10-01)

DB access as in `.agent-resources/report-09-members/PLAN.md` (Research).

- `OM_ContactGroup`: one recipient list, `DancingGoat.RecipientList` (ID 2, "Dancing goat recipient list"). The other groups are not lists (`ContactGroupIsRecipientList` 0/NULL). `EmailLibrary_RecipientListSettings` has one row for it.
- One real subscriber: contact 454 `dana@kentico.local`, member row + one approved confirmation row 2026-10-01. **Never delete or change real rows** in seeding.
- `EmailLibrary_EmailBounce`, `EmailStatistics`, `EmailStatisticsHits`: 0 rows. 453 contacts (401 with email).

## Slices

1. Recipient lists (`implementation-spec-1.md`)

## Slice log

- 2026-10-01 – Slice 1: Recipient lists report + Emails section (`ebda9c2`; spec `4a99e44`).
