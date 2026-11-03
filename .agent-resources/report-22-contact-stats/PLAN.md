# Plan: Report 22 – Contact stats tab

## Goal

Audience: marketers looking at one contact. Answers "how engaged is this contact, how has that changed, and what are they interested in?" The product's **Activities** tab (`Kentico.Xperience.Admin.DigitalMarketing.UIPages.ContactActivityList`) is a plain list with a text search and no filters. This tab shows trends and insights instead. Read `.agent-resources/simple-stats/PLAN.md`, `.agent-resources/report-01-activity-counts/`, `.agent-resources/report-21-campaign-sources/` and the web page Stats tab code first (`UIPages/WebPageStatsPage.cs`, `Reports/WebPageStats/`, `Client/src/web-page-stats/`). It is the model for a tab on another application's page. Build on existing code; do not duplicate.

## Research

### Data (checked in local DB 2026-10-08)

- `OM_Activity`: `ActivityContactID`, `ActivityCreated`, `ActivityType`, `ActivityItemID`, `ActivityTitle`, `ActivityValue`, `ActivityURL`, `ActivityWebPageItemGUID`, `ActivityLanguageID`, `ActivityChannelID`, `ActivityUTMSource`, `ActivityUTMContent`.
- Local types: `pagevisit` 1,941, `landingpage` 611, `bizformsubmit` 575, `memberregistration` 171, `emailclick` 92. These include the 1,140 synthetic rows from `seed-campaign-sources.sql`.
- 459 contacts have activities, about 7 each on average. The top contact (454) has 104 activities over 3 days. Contact 9 has 16 activities of 5 types and was created 2026-07-15.
- Form submissions: `ActivityItemID` = `CMS_Form.FormID` (for example 1 = "Coffee sample list"). Email clicks: `ActivityItemID` = the email (see how Email summary / Activity counts resolve it), `ActivityValue` = clicked URL.
- Activity type display names come from `OM_ActivityType` (as Activity counts does). Custom activity types appear the same way.
- Landing page activities mark the start of a browsing session (see `report-21-campaign-sources`), so landings ≈ sessions.

## Design rules

### Definitions

- **Range**: presets as the shared filter, plus **All time** (default; from the contact's `ContactCreated` or first activity, whichever is earlier).
- **Sessions**: landing page activities. **Active days**: distinct days with any activity.
- **Last seen**: newest activity. **First seen**: oldest activity.
- **Trend**: activities in the range compared with the previous period of the same length (`StatsComparison`). Hidden for All time.
- Aggregates over this contact only. No other contact's data is shown, except an optional cohort figure (see Scope).

## Slices

1. Contact stats tab (`implementation-spec-1.md`)
2. Interests by tag (user decision 2026-10-09) (`implementation-spec-2.md`)

## Decisions

### Placement (user decision)

- New tab on each contact in the **Contact management** application, for example `/admin/contact-management/contacts/9/overview`.
- Parent: `Kentico.Xperience.Admin.DigitalMarketing.UIPages.ContactEditSection` (confirmed in `Kentico.Xperience.Admin.DigitalMarketing.dll`). Order **1001**. Name **"Stats (Labs)"**. Pick an existing `Icons` member.
- Read the contact ID from the parent's URL parameter the way the product's contact pages do (check `ContactActivityList` / `ContactEditSection` in the decompiled assembly).
- Permission: new `StatsPermissions.CONTACT_STATS` = `SimpleStats.ContactStats`, "Contact stats". It is evaluated through `StatsApplicationPermissionEvaluator` and the tab is hidden without it, as the web page Stats tab does. Contact management's own permissions still guard the parent.

## Slice log

- 2026-10-09 – Slices 1–2: contact Stats (Labs) tab with interests by tag (`2c33bd4`).
- 2026-10-09 – Fixes: unknown activity types kept (`92efab1`); permission check before contact lookup (`46709fa`); bounded taxonomy cache keys (`2592366`); taxonomy change reads only tag data (`5f9599c`); SQL integration tests (`af1f353`).
