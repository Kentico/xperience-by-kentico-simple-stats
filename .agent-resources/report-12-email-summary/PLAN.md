# Plan: Report 12 – Email summary

## Goal

Twelfth report. Audience: email marketers. simple-stats PLAN "Email summary across all emails": sends, open rate, click rate, bounces, unsubscribes, top and bottom performers. Time-range report in the **Emails** section, next to Recipient lists. Read `.agent-resources/simple-stats/PLAN.md`, `.agent-resources/report-11-recipient-lists/` (closest: same section, DB access, seeding rules), `.agent-resources/report-09-members/` (DB access) and `.agent-resources/nav-sections/` first. Build on existing code; do not duplicate. Keep complexity low.

## Research

### Product model (decompiled 31.9, checked 2026-10-01)

Public Info classes: `EmailConfigurationInfo`, `EmailStatisticsInfo`, `EmailStatisticsHitsInfo`, `EmailLinkInfo`, `EmailChannelInfo` (`CMS.EmailLibrary`), `SendConfigurationInfo` (`CMS.EmailMarketing`). Reference them in SQL doc comments like earlier reports.

- **`EmailLibrary_EmailConfiguration`**: one row per email. `...Name` (code name), `...Purpose` nvarchar (`Regular`, `Automation`, `FormAutoresponder`, `Confirmation`, `CommerceOrderStatusChange`, maybe more; read the `EmailPurpose` values instead of hardcoding this list), `...EmailChannelID`, `...ContentItemID`, `...LastModified`.
- **Display name**: the native list joins `CMS_ContentItemLanguageMetadata` on the content item and shows `ContentItemLanguageMetadataDisplayName`. Use the email channel's primary language (`EmailChannelPrimaryContentLanguageID`), and fall back to any language, then to the code name.
- **`EmailLibrary_SendConfiguration`** (Regular emails only): `...Status` (`SendConfigurationStatus`: 0 Draft, 1 Scheduled, 2 Sending, 3 Sent), `...ScheduledTime` (the native **Send date** column), `...RecipientListID`.
- **`EmailLibrary_EmailStatistics`**: one row per email, **lifetime totals with no timestamps**: `TotalSent`, `EmailsDelivered`, `EmailOpens`, `EmailUniqueOpens`, `EmailClicks`, `EmailUniqueClicks`, `EmailSoftBounces` (NULL), `EmailHardBounces` (NULL), `UniqueUnsubscribes`, `SpamReports` (NULL). These are the native numbers.
- **`EmailLibrary_EmailStatisticsHits`**: raw events with time. `...Type` (`EmailStatisticsHitsType`: 0 Sent, 1 Open, 2 Click, 3 SoftBounce, 4 HardBounce, 5 Unsubscribe), `...Time` (server local time, `DateTime.Now`), `...MailoutGUID` (one per recipient send), `...EmailLinkID` (clicks), `...IsProcessed`. No product code deletes hits (checked in `CMS.EmailMarketing` only; check the cleanup tasks as well).
- **Recalculation** (`Proc_EmailLibrary_EmailStatistics_RecalculateEmailStatistics`, run by the scheduled task `EmailStatisticsCalculatorTask` and by the Statistics tab "Refresh" button). It works in two ways:
  - Totals (sent, opens, clicks, bounces) are **added up step by step** from unprocessed hits, which are then marked processed.
  - Uniques are **recalculated from all hits**, per mailout: an open = any open **or click** hit, a click = any click hit, an unsubscribe = any type 5 hit.
  - Delivered = sent − soft − hard bounces.
  - So hits can rebuild uniques exactly, but not always totals.
- **Bounces, spam and delivery providers**: the SMTP fallback calculator counts bounces from hits (written by `BounceChecker`). Other providers (`ISpecificEmailStatisticsCalculatorCreator`, for example SendGrid on the Community Portal in production, where spam reports show 0 and not "-") can write bounces, spam and delivered directly into the statistics table without hits. **Use the statistics table for bounces, spam and delivered. Never use hits for them.**
- **Rates differ inside the product**:
  - Email list (`EmailStatisticsFormatters.FormatStatisticsRate`): unique ÷ **TotalSent**.
  - Statistics tab: open/click rate = unique ÷ **Delivered**. Delivery rate = delivered ÷ sent. Hard/soft bounces, spam and unsubscribe rate are each ÷ sent.
  - Example from production, Newsletter #41: list 55.1% (75/136), tab 55.6% (75/135).
  - Follow the Statistics tab (Decisions 1) and say in a hint that the email list uses sent.
- **Native links** (user-confirmed URLs, 2026-10-01): list `/admin/emails-1/en/list` (`EmailList`), email `/admin/emails-1/en/list/3/content` (`EmailContentTab`). Statistics = `EmailStatisticsTab`, slug `statistics` under `EmailEditLayout`.
  - Parameters: `EmailChannelApplication` = `"emails-" + EmailChannelID` (the prefix `emails` is the internal `EmailPagePathSlugs.APPLICATION`, so keep it as a constant with a comment), `EmailChannelContentLanguage` = language code name, `EmailEditLayout` = EmailConfigurationID.
  - All page types are `public sealed`; the product helper (`EmailChannelLinkParametersHelper`) is internal, so build `PageParameterValues` in `IStatsAdminLinks`.
  - Verify the generated path matches the confirmed URLs.

### Local data

DB access as in `.agent-resources/report-09-members/PLAN.md` (Research).

**Community Portal DB** (`Kentico.Community`, same server and credentials; read only, never write). Use it to check SQL against real data:

- 45 emails, 1 email channel. 31 statistics rows, 343 hits (Sep 2025 – Aug 2026), 189 links.
- Newsletters (Regular, Sent) have zero statistics, no hits and a NULL ScheduledTime, because this local copy never sent them. Only 5 automation/autoresponder emails have hits (e.g. 39 "Member registration email confirmation": sent 68, unique opens 94, unique clicks 83).
- Uniques from hits match the statistics table (open = open or click per mailout). Use this as a SQL test.
- 44 mailouts have open/click hits but no sent hit, and unique opens can be higher than sent. **Rates can go over 100%.** Show the number and do not cap it; add a hint.
- Open lag after a send: average 99 min, max 31 h.

**Production Community Portal** (screenshots shared by the user): ~41 biweekly newsletters, sent 126–140 each, open rate 45–57%, click rate 9–20%, hard bounces 0–1, unsubscribes 0–1, spam 0. Automation "Q&A discussion notification": 172 sent, 96.5% opens, 61.6% clicks. Model the seed on these numbers.

**DancingGoat DB**: 7 emails, 1 email channel (ID 1, primary language ID 1, 2 languages). Regular emails 2 and 3 are Draft. 1 statistics row and 2 hits are real. **Never delete or change real rows.**

## Slices

1. Email summary (`implementation-spec-1.md`)

## Decisions

### Decisions (user, 2026-10-01)

1. **Rates**: open/click rate = unique ÷ delivered, as in the Statistics tab.
2. **Scope**: KPIs, the email table and top/bottom cover **Regular emails only**. Automated emails (Automation, FormAutoresponder, the others) go in a separate lifetime table. Reason: Regular emails are always marketing, sent to a recipient list with double opt-in. The other purposes mix transactional and marketing sends (legitimate interest, commerce, password recovery). This reasoning is for the code docs only; **do not put it in the report UI**.
3. **Seeding**: the user clones emails in the UI (native Clone; SQL cloning of `CMS_Content*` rows is too fragile). The seed adds only the send state, links, hits and statistics.
4. **Real send**: done. `examples/DancingGoat/Program.cs` adds `AddXperienceChannelSmtp("DancingGoatEmails", ...)` with the `SystemSmtpOptions` settings (Mailpit on `localhost:1025`), and `docs/Contributing-Setup.md` mentions it. The user will test real sends locally with several contacts. Use that as the check of hit logging + recalculation (run the scheduled task or use the Statistics tab "Refresh"), next to the seeded data. Real sends create real rows that the seed must not touch.
5. **Production check** (user, after the build): the user will install a prerelease package on the Community Portal production site (SendGrid sends) and compare the report with the native Statistics tab and the email list. So:
   - the report must work when bounces, spam and delivered come from SendGrid and not from hits
   - it must work with ~45 emails and one email channel
   - the page must not break when hits are missing
   - list in the report-back what the user should compare there (per newsletter: sent, delivered, open/click rate, hard bounces, unsubscribes, spam; range totals vs the sum of the native rows)

Report back concise: files, SQL shape, definitions chosen, shared changes, links, seeding, checks, not checked, open questions.

## Slice log

- 2026-10-01 – Slice 1: Email summary report (`3dc9fa2`).
