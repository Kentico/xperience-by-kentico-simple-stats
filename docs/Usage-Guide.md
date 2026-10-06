# Usage Guide

## Setup

1. Install the `Kentico.Xperience.Labs.SimpleStats.Admin` NuGet package in your Xperience by Kentico web project.
2. Run the application. No service registration is needed; the library registers its services and admin UI automatically.

## Application

The library adds the **Simple Stats (Labs)** application to the **Digital marketing** category.

## Navigation

Reports are grouped into sections. Opening the application or a section opens its first report the role may see.

| Section  | Reports                                                                                    |
| -------- | ------------------------------------------------------------------------------------------ |
| Contacts | Activity counts, Top pages, New contacts, Form submissions, Member registrations, Consents |
| Emails   | Email summary, Recipient lists                                                             |
| Content  | Content inventory                                                                          |
| Commerce | Orders and revenue, Customers                                                              |
| System   | Event log                                                                                  |

Sections have no permission of their own. A section is hidden when the role has no permission for any of its reports. A role with **View** but no report permission sees a **No reports available** message.

## Permissions

- Administrators see the application and all reports by default.
- For other roles, open **Role management**, select the role, and edit its permissions for **Simple Stats (Labs)**:
  - **View** - opens the application.
  - One permission per report. The role sees only the reports it has a permission for. Other reports are hidden from the navigation and return an error if opened by URL.
  - **Export** - shows the **Export CSV** buttons in all reports the role can see. Without it, the buttons are hidden.

| Permission           | Code name                      |
| -------------------- | ------------------------------ |
| Activity counts      | `SimpleStats.ActivityCounts`   |
| Top pages            | `SimpleStats.TopPages`         |
| New contacts         | `SimpleStats.NewContacts`      |
| Form submissions     | `SimpleStats.FormSubmissions`  |
| Content inventory    | `SimpleStats.ContentInventory` |
| Event log            | `SimpleStats.EventLog`         |
| Orders and revenue   | `SimpleStats.OrdersRevenue`    |
| Customers            | `SimpleStats.Customers`        |
| Member registrations | `SimpleStats.Members`          |
| Consents             | `SimpleStats.Consents`         |
| Email summary        | `SimpleStats.EmailSummary`     |
| Recipient lists      | `SimpleStats.RecipientLists`   |
| Web page stats       | `SimpleStats.WebPageStats`     |
| Export               | `SimpleStats.Export`           |

The **Export** permission only hides the buttons. The CSV is built in the browser from the data the report already shows, so a role that can see a report can still copy its numbers. It is not data protection.

## Reports

### Activity counts

Shows contact activities per activity type over time as a stacked column chart.

- **KPIs** - total activities, top activity type, and number of activity types in the range.
- **Filters**
  - **Date range** - last 7, 30 or 90 days, or a custom range (up to about 5 years).
  - **Group by** - day, week (weeks start on Monday) or month.
  - **Channel** - all channels, or one website or email channel (uses the activity channel).
- **Chart / table** - switch the tile to a table with the exact numbers.
- **Export CSV** - downloads the table as a CSV file.

### Top pages

Shows the 25 most visited page URLs in the range as a bar chart, largest on top.

- **KPIs** - total page visits, number of distinct page URLs, and the top page.
- **Filters** - date range and website channel (no grouping).
- **Chart / table** - the table lists rank, URL (opens the page in a new tab), visits, unique contacts and share of all page visits in the range.
- **Export CSV** - downloads the list as a CSV file.

Visits are grouped by the logged URL with the query string and fragment removed, so `/page?utm_source=x` counts as `/page`. Other differences (host, trailing slash, letter case) count as different pages.

### Web page stats

A **Stats** tab on each web page in website channels (after the page's other tabs). Shows contact activities logged for that page in the language being edited, as a stacked column chart per activity type.

- **KPIs** - page visits, unique visitors (distinct contacts with a page visit), form submissions (with distinct submitters and their share of unique visitors), and total activities with the number of distinct contacts.
- **Filters** - date range and grouping (no channel: the page belongs to one channel).
- **Chart / table** - switch the tile to a table with the exact numbers.
- **Export CSV** - downloads the table as a CSV file.

Activities are matched by the web page they were logged for (`ActivityWebPageItemGUID`) and the language. Only counts are shown; no contact details.

Form submission activities have no web page link or language, so they are matched by URL instead: the logged `ActivityURL` against the current live URL of the page variant (the channel home page is `/`, or `/{language}` for other languages), in the page's channel. The host, query string, fragment and a trailing slash are ignored; letter case follows the database collation (case-insensitive by default). Limits:

- Submissions made under a former URL of the page (before it was moved or renamed) are not counted.
- A page without a live URL (never published) shows no form submissions.
- Channels with [language-specific domains](https://docs.kentico.com/documentation/developers-and-admins/configuration/website-channel-management#configure-language-specific-domains) use the same paths for all languages, so the URL host must also match: the domain and aliases configured for the edited language in `WebsiteChannelDomains:LanguageDomains` of the running environment (host and port, letter case ignored). If the language has no domain configured, the host is not checked and submissions of all languages on the same path are counted together; the tooltip says so.
- Domains are read from the configuration of the environment that runs the admin. Activities logged under other hosts (for example production data copied to a local database) are not matched on language-domain channels.
- Language prefix channels ignore the host, because the path already includes the language. The tab requires the **Web page stats** permission of **Simple Stats (Labs)** (not the **View** permission of the application) and is hidden on the channel root.

### New contacts

Shows contacts created per period, stacked by identified and anonymous, and the share of each.

- **Identified** - the contact has an email address. **Anonymous** - no email address (for example, a site visitor who has not submitted a form).
- **KPIs** - new contacts in the range, identified (count and share) and anonymous (count and share).
- **Filters** - date range and grouping (no channel; contacts have no channel).
- **Tiles** - "New contacts over time" (stacked column chart or table) and "Identified vs anonymous" (donut chart or table). Each has its own CSV export.

Counts only include contacts that still exist. Contacts deleted by cleanup and contacts removed when merged into another contact are not counted. A contact that gets an email address later counts as identified in the period it was created.

### Form submissions

Shows submissions per form over time and ranks all forms from most to least used.

- **Source** - counts come from the form data tables (`FormInserted`), so they include every stored submission, not only submissions logged as contact activities.
- **KPIs** - total submissions (with the change vs the previous period of the same length, for example "+12% vs previous 30 days"), average submissions per day, and forms with no submissions.
- **Filters** - date range and grouping (no channel; form data has no channel).
- **Tiles** - "Submissions over time" (stacked column chart per form or table; the top 5 forms are shown, the rest are grouped as Other) and "Forms by submissions" (bar chart or table of every form, including forms with 0). Click a form's graph bar or name in the table to open the form's **Submissions** tab in the **Forms** application. Each tile has its own CSV export.

Deleting a contact (manually or by inactive contact cleanup) deletes their activities but not their form submissions, so submissions stay counted. Submissions are removed only when editors delete them or through [personal data erasure](https://docs.kentico.com/x/04B1CQ).

### Member registrations

Shows [members](https://docs.kentico.com/documentation/business-users/members) (visitors with a site account) registered over time, total members, external sign-ups and members per member role.

- **Definitions**
  - **New member** - a member account created in the range (`MemberCreated`).
  - **Total members** (on a day) - members created on or before that day that still exist. Deleted members are not counted, also not for past days.
  - **External** - the member signed up through an external sign-in provider (for example Google or Microsoft). **Internal** - all other members.
  - **Disabled** - the member account is disabled now (current state, not historical).
- **KPIs** - new members, total members (on the last day of the range vs the last day of the previous period), external sign-ups (share of new members; the change is in percentage points, "pp"; "–" without new members), each vs the previous period of the same length, and disabled members (current state, no comparison).
- **Filters** - date range and grouping (no channel; members are global).
- **Tiles**
  - "Member growth" - new members as columns (left axis) and total members at the end of each period as a line (right axis), or a table with new, internal, external and total members.
  - "New members by sign-in type" - internal vs external new members in the range, as a donut chart or table.
  - "Members by role" - current members per member role, plus **No role** for members without a role, with how many of them were created in the range. Roles are the current state. A member can have several roles, so the share is of all members and shares do not add up to 100%. Roles without members are left out. Click a role to open it in the native **Members** application.
- **Open members** - opens the native **Members** application.
- Each tile has its own CSV export.

Deleted members are not counted. When the member tables do not exist, the report is empty.

### Consents

Shows [consent](https://docs.kentico.com/documentation/developers-and-admins/data-protection/consent-management) agreements and revocations over time, agreed contacts, all consents compared and which consent text agreed contacts agreed to.

- **Definitions**
  - **Agreement** / **revocation** - one agree or revoke action of a contact in the range. Agreeing again (for example to a new consent text) counts again.
  - **Agreed contacts** (on a day) - contacts whose latest agreement or revocation of the consent on or before that day is an agreement (the same rule the product uses). With **All consents**, each contact who agrees to at least one consent is counted once (not the sum per consent), so the number means "contacts you may process data of".
  - **Revocation rate** - revocations divided by agreements in the range ("–" without agreements; the change is in percentage points, "pp").
  - **Older text** - agreed contacts whose latest agreement was given to an older consent text (its hash is not the consent's current hash).
- **KPIs** - agreements, revocations, revocation rate and agreed contacts (on the last day of the range vs the last day of the previous period), each vs the previous period of the same length.
- **Filters** - date range, grouping and consent (all consents or one; no channel).
- **Tiles**
  - "Agreements and revocations" - stacked columns per period, or a table.
  - "Agreed contacts over time" - agreed contacts at the end of each period as a line, or a table (no total column; the values are counts on a day and do not add up).
  - "Consent text version" - per consent, agreed contacts on the current text vs an older text. Contacts on an older text may need to agree again.
  - "Consents" - all consents (the consent filter does not apply, so consents can be compared): agreed contacts on the last day of the range with the previous period value and change, and agreements and revocations in the range. A contact can agree to several consents, so shares are of all agreed contacts and do not add up to 100%. Click a consent to open its **Consent agreements** in the native **Data protection** application.
- **Open data protection** - opens the consents of the native **Data protection** application.
- Each tile has its own CSV export.

The report reads the stored agreements only and compares consent texts by hash, not by content. Deleting a contact also deletes its consent agreements, also for past days, so numbers can be lower than they were. Revoking can trigger data erasure in projects that handle it. When the consent tables do not exist, the report is empty.

### Email summary

Shows the [email statistics](https://docs.kentico.com/documentation/business-users/digital-marketing/emails/track-email-statistics) of regular emails sent in a date range: totals, each email, top and bottom performers, email activity over time and automated emails.

- **Definitions**
  - **Sent in range** - regular emails whose send date (the **Send date** of the native email list) is in the range and that are sent or sending. Drafts and scheduled emails are left out. This sets the scope of the KPIs, the email table and the top and bottom performers.
  - **Email numbers** - the lifetime totals of the email statistics, the same numbers as the email's **Statistics** tab. Opens and clicks that come after the range still count for an email sent in the range. The statistics are recalculated by a scheduled task (or **Refresh** on the Statistics tab), so the newest opens and clicks can be missing.
  - **Open rate** / **click rate** - unique opens or clicks divided by delivered, like the Statistics tab. The native email list divides by sent, so it can show a slightly lower rate. A click counts as an open. Rates can be over 100% when recipients open or click without a recorded send; the report shows them as they are.
  - **Delivery rate** - delivered divided by sent. **Unsubscribe rate**, bounces and spam reports are divided by sent, like the Statistics tab.
  - **Range totals** - sums of the emails' numbers; rates are sums divided by sums (for example all unique opens divided by all delivered), not averages of the email rates. A rate is "–" when its denominator is 0. Rate changes are in percentage points ("pp").
  - **Bounces and spam reports** - read from the email statistics, which the delivery provider fills (for example SendGrid). They are "–" when the provider does not track them, left out of the sums, and the KPI or column is hidden when no email has them.
  - **Email activity over time** - from the raw statistics records of all emails (regular and automated), by when recipients acted, not by send date: sent emails, and recipients with at least one open (or click), click or unsubscribe in the period. A recipient who acts in two periods counts in both.
  - **Top and bottom performers** - the 5 regular emails sent in the range with the highest and the 5 with the lowest open rate or click rate. Emails with fewer than 10 delivered are left out. With fewer than 10 emails, the bottom list leaves out the emails already in the top list.
  - **Automated emails** - emails of every other purpose (automation, form autoresponders, confirmations, commerce) with statistics. Their numbers are lifetime totals, not limited to the range; only **Sent in range** counts sends in the range.
- **KPIs** - emails sent, sent, open rate, click rate; delivery rate, hard bounces, unsubscribe rate and spam reports. Each vs the regular emails sent in the previous period of the same length.
- **Filters** - date range, grouping and email channel (shown with more than one email channel).
- **Tiles**
  - "Email activity over time" - columns per period (sent, unique opens, unique clicks, unsubscribes), or a table.
  - "Emails sent in range" - each email with send date, sent, delivered, open rate, click rate, hard bounces and unsubscribes, or a bar chart of open rates. Click an email to open its native **Statistics** tab.
  - "Top and bottom performers" - open rate / click rate switch.
  - "Automated emails" - lifetime table with a link to each email's Statistics tab.
- **Open emails** - opens the native email list of the email channel (shown when one channel is selected or there is only one).
- Each tile has its own CSV export.

Deleting an email deletes its statistics. When the email tables do not exist, the report is empty. The activity series reads the statistics records by time; on sites with a very large number of sends, long ranges take longer.

### Recipient lists

Shows [recipient list](https://docs.kentico.com/documentation/business-users/digital-marketing/emails/send-regular-emails-to-subscribers) subscriptions and unsubscriptions over time, subscribers, the current status of list members and all lists compared.

- **Definitions**
  - **Subscription** / **unsubscription** - one confirmed subscription or one unsubscription (revoked subscription) of a contact in the range. Xperience stores both as subscription confirmation records. Subscribing again counts again.
  - **Subscribers** (on a day) - contacts who are members of the list now and whose latest subscription or unsubscription of the list on or before that day is a subscription. List membership has no history, so it is applied as it is now. Bounces are current state only, so they are not applied to the time series. With **All lists**, each contact subscribed to at least one list is counted once.
  - **Receiving**, **Bounced**, **Unsubscribed**, **Not confirmed** - the current status (now, not at the end of the range) of list members, counted like the recipient list overview in the native **Recipient lists** application: receiving = subscribed and the email has not bounced; bounced = subscribed, but the email had a hard bounce or reached the soft bounce limit (`BouncedEmailsGlobalOptions.SoftBounceLimit`, default 5); unsubscribed = the latest action is an unsubscription; not confirmed = a member without a subscription confirmation (double opt-in pending, or added without confirmation; the native overview does not count these). With **All lists**, each contact is counted once per list.
  - **Unsubscribe rate** - unsubscriptions divided by subscriptions in the range ("–" without subscriptions; the change is in percentage points, "pp").
- **KPIs** - subscriptions, unsubscriptions, unsubscribe rate and subscribers (on the last day of the range vs the last day of the previous period), each vs the previous period of the same length.
- **Filters** - date range, grouping and recipient list (all lists or one; no channel).
- **Tiles**
  - "Subscriptions and unsubscriptions" - stacked columns per period, or a table.
  - "Subscribers over time" - subscribers at the end of each period as a line, or a table (no total column; the values are counts on a day and do not add up).
  - "Subscriber status" - donut chart or table of the current statuses (list filter applied).
  - "Recipient lists" - all lists (the list filter does not apply, so lists can be compared): current statuses, subscriptions and unsubscriptions in the range, and the subscriber change (subscribers on the last day of the range minus subscribers on the day before the range). The chart shows receiving members per list. Click a list to open it in the native **Recipient lists** application.
- **Open recipient lists** - opens the native **Recipient lists** application.
- Each tile has its own CSV export.

The report counts only what Xperience stores as subscription confirmations. Deleting a contact also deletes its subscriptions and list memberships, also for past days, so numbers can be lower than they were; merged contacts keep their subscriptions. Custom code that deletes subscription records and list members on unsubscribe (instead of revoking the subscription) leaves no unsubscription, so those unsubscriptions are not counted. When the recipient list tables do not exist, the report is empty.

### Content inventory

Shows the current state of content items (no date range, not a trend): items by content type, status, language and age, items waiting in workflow steps, and unused reusable items.

- **Filters** - content: all, pages, reusable, emails or headless (the content type's **Use for** setting). With pages, emails or headless, a **Channel** filter lists the website, email or headless channels. All and reusable have no channel filter (reusable items have no channel).
- **KPIs** - content items (with a split per content kind), content types in use, languages (with the default language), action needed (language variants unchanged in a workflow step for more than 14 days), language variants not modified in 12 months, and unused reusable items (only with all or reusable).
- **Tiles**
  - "Action needed: items in workflow steps" - shown only when variants are in a workflow step. Longest unchanged first; bars over 14 days are highlighted. Click an item to open it (or its workflow's steps when the item cannot be linked). The time an item entered its step is not stored, so days count from the last change of the language variant.
  - "Oldest content" - the 25 language variants with the oldest last change (bars over 12 months are highlighted); click an item to open it. Beside it, "Content age" (variants under 3 months, 3–6, 6–12 and over 12 months since their last change) and "Status" (see below) are stacked.
  - "Unused reusable items" - chart of unused items per content type, table of the 25 least recently changed unused items (click an item to open it in the **Content hub**). An item counts as used when another content item references it, in any language or version: through the content item selector or rich text editor (in content type fields or Page and Email Builder component properties), or through custom components with a [reference extractor](https://docs.kentico.com/documentation/developers-and-admins/customization/extend-the-administration-interface/ui-form-components/ui-form-component-reference-extractors). References that exist only in code are not tracked.
  - "Items by content type" - bar chart (up to 25 types with items) or table of every content type, including types with no items. Click a type to open it in the **Content types** application.
  - "Status" - donut chart or table of language variants by the status of their latest version: published, draft, in workflow, unpublished. Scheduled publish and unpublish counts are in the tile description.
  - "Language coverage" - items with a variant in each language vs all items, so missing translations show as the missing part.
- Each tile has its own CSV export.

Page folders are not counted. Items in all workspaces are counted. "Last change" is the modified time of the language variant's latest version. Items open where they are edited: reusable items in the **Content hub**, pages, emails and headless items in their channel application.

### Orders and revenue

Shows [digital commerce](https://docs.kentico.com/documentation/business-users/manage-commerce-stores) orders and revenue over time, orders by status, and the products with the most revenue.

- **Revenue** - the order grand total (incl. shipping and tax) as stored when the order was placed. Orders without a stored grand total count as orders with 0 revenue.
- **Currency** - orders store no currency. Amounts are formatted with the project's price formatter (`CMS.Commerce.IPriceFormatter`), the same way the native **Orders** application shows them, so they show your store's currency. Without a custom formatter the product default is used (2 decimals, no currency symbol). Amounts are not converted between currencies. Chart axes show plain numbers; tooltips and tables show formatted amounts.
- **KPIs** - orders, revenue, average order value (revenue / orders; "–" when there are no orders) and items sold (sum of item quantities), each with the change vs the previous period of the same length.
- **Filters** - date range, grouping and **Order status** (all statuses, or one of the project's order statuses from **Commerce configuration**, in their order). No channel; orders have no channel. The status filter applies to the KPIs, "Orders and revenue over time" and "Top products".
- **Tiles**
  - "Orders and revenue over time" - orders as columns (left axis) and revenue as a line (right axis), or a table.
  - "Orders by status" - donut chart or table (orders, revenue, share) of the current status of orders created in the range. Always shows every status, also with the status filter set.
  - "Top products" - the 10 products with the most revenue (sum of order item totals), grouped by SKU (by item name when an item has no SKU), with quantity, previous period revenue and change ("New" when there was none).
- **Open orders** - opens the native **Orders** application.
- Each tile has its own CSV export. CSV values are raw numbers (dot decimal separator, no grouping, no currency); amount columns are marked "(raw amount)".

Orders are grouped by the date they were created. Deleted orders are not counted. When the project does not use digital commerce (no orders, or no commerce tables), the report is empty.

### Customers

Shows [digital commerce](https://docs.kentico.com/documentation/business-users/manage-commerce-stores) customer growth, ordering and returning customers, where customers are, and the top customers.

- **Definitions**
  - **New customer** - a customer created in the range (the date the customer record was created). The order status filter does not apply.
  - **Ordering customer** - a customer with at least one order in the range.
  - **Returning customer** - an ordering customer who also has an order before the range starts (any time).
  - **Active customer** (on a day) - a customer with at least one order in the **activity window** (30, 60, 90 or 180 days, default 90) up to and including that day. A customer stops being active window days after their last order; an order exactly window days before a day no longer counts.
  - **Customer location** - the country and state of the selected address type (**Billing** or **Shipping**) on the customer's most recent order in the range. Customers whose order has no such address, or an address without a country, are counted as **Unknown**.
- **KPIs** - new customers, ordering customers, returning customers (share of ordering customers; the change is in percentage points, "pp"), active customers (on the last day of the range vs the last day of the previous period) and revenue per customer (revenue / ordering customers; "–" without ordering customers), each vs the previous period of the same length.
- **Filters** - date range, grouping, **Order status** (all statuses, or one of the project's order statuses) and **Location by** (Billing or Shipping address). No channel. The status filter applies to everything based on orders (not to new customers). The address type applies only to the location tiles. The activity window toggle is in the "Active customers" tile and also sets the "Active customers" KPI.
- **Tiles**
  - "Customer growth" - new customers as columns (left axis) and total customers at the end of each period as a line (right axis), or a table.
  - "Active customers" - active customers at the end of each day, week or month (the range end for the last one), as a line or a table, with an activity window toggle (30, 60, 90 or 180 days). Values are counts on that day, so the table and CSV have no total.
  - "Customers by country" - the 15 countries with the most ordering customers (incl. **Unknown**), with the previous period count and change.
  - "Top states and regions" - the 10 states with the most ordering customers ("State, Country"). Customers without a state are left out of this tile (they are still in "Customers by country"). Only addresses in countries that have states in Xperience (the default data has states for the USA only) can have a state.
  - "Top customers" - the 10 customers with the most revenue, orders or items (sum of item quantities), switched with the **Revenue / Orders / Items** toggle in the tile. Opens as a table with email, revenue, orders, items and the change of the ranked value vs the previous period ("New" when there was none). The name falls back to the email, then "Customer #ID". Click a customer to open it in the native **Customers** application.
- **Open customers** - opens the native **Customers** application.
- **Revenue** is the order grand total (incl. shipping and tax) as stored, formatted by the project's price formatter like in "Orders and revenue". Each tile has its own CSV export (raw numbers; amount columns are marked "(raw amount)").

Deleted customers and orders are not counted. When the project does not use digital commerce (no commerce tables), the report is empty.

### Event log

Shows events of the system [event log](https://docs.kentico.com/documentation/developers-and-admins/configuration/event-log) per type over time, and the sources, event codes and users with the most events.

- **KPIs** - all events, errors, warnings and information events in the range, each with the change vs the previous period of the same length (for example "+40% vs previous 30 days"). The change is shown as text only; a rise in errors or warnings is not colored.
- **Filters** - date range, grouping and **Event type** (all, errors, warnings or information). The event type filter applies to the tiles, not to the KPIs.
- **Tiles**
  - "Events over time" - stacked column chart or table, one series per type with fixed colors, stacked from the bottom: information, warnings, errors.
  - "Top sources" and "Top event codes" - the 10 sources and event codes with the most events, with their count in the previous period and the change ("New" when there were none).
    - "Top sources" has a toggle: **All**, **Xperience** (sources logged by the product: starting with `CMS.` or `Kentico.`, plus `WebFarmMonitor`) or **Custom** (all other sources, usually logged by project code). The CSV export has the selected list.
  - "Top users" - the 10 users with the most events, with the same previous period count and change. Events logged without a user (for example by background tasks) are one **System** row. Click a user to open it in the **Users** application.
- **Open event log** - opens the native **Event log** application to see single events.
- Each tile has its own CSV export.

The event log keeps at most the number of events in **Settings → System → Event log → Event log size**. When the log is full, the oldest events are deleted, so long ranges can show fewer events in older periods than really happened. The report shows the current limit.

### Dates and caching

Time-based reports use the server date. Results of all reports are cached for 5 minutes, so new activities, contacts, submissions, content changes, events, orders, customers, members, consent agreements and recipient list subscriptions can take a few minutes to appear. Select **Refresh** to load the latest numbers.

## Audit CSV exports

After every **Export CSV**, the library raises `AfterExportStatsEvent` (`Kentico.Xperience.Labs.SimpleStats.Admin.UIPages`). It works like the product's [`AfterExportListingEvent`](https://docs.kentico.com/documentation/developers-and-admins/customization/extend-the-administration-interface/ui-pages/reference-ui-page-templates/listing-ui-page-template/export-listing-data#run-custom-code-after-an-export): handlers implement `IAsyncEventHandler<AfterExportStatsEvent>` (`CMS.Base`), run as singletons and cannot change or cancel the export.

`asyncEvent.Data` (`StatsExportEventData`) has:

| Property             | Description                                                                                                     |
| -------------------- | --------------------------------------------------------------------------------------------------------------- |
| `ReportPageTypeName` | Full type name of the report page, for example `Kentico.Xperience.Labs.SimpleStats.Admin.UIPages.ConsentsPage`. |
| `UserID`             | The administration user who exported, or 0 if unknown.                                                          |
| `Timestamp`          | Time of the export, server local time.                                                                          |
| `ExportName`         | Stable ID of the tile, for example `consents-events`. See the list below.                                       |
| `FileName`           | Name of the downloaded file, for example `consents-events_2026-09-01_2026-09-30_day.csv`.                       |
| `RowCount`           | Data rows in the file, not counting the header.                                                                 |

Example handler that writes to the event log:

```csharp
using CMS.Base;

using Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

using Microsoft.Extensions.Logging;

public class StatsExportAuditHandler(ILogger<StatsExportAuditHandler> logger) : IAsyncEventHandler<AfterExportStatsEvent>
{
    public Task HandleAsync(AfterExportStatsEvent asyncEvent, CancellationToken cancellationToken)
    {
        var data = asyncEvent.Data;
        logger.LogInformation(
            new EventId(0, "EXPORT"),
            "User {UserID} exported {RowCount} rows of '{ExportName}' ({FileName}) from {ReportPage} at {Timestamp}.",
            data.UserID, data.RowCount, data.ExportName, data.FileName, data.ReportPageTypeName, data.Timestamp);

        return Task.CompletedTask;
    }
}
```

Register it in `Program.cs`:

```csharp
builder.Services.AddEventHandler<AfterExportStatsEvent, StatsExportAuditHandler>();
```

One handler class can implement both `IAsyncEventHandler<AfterExportListingEvent>` and `IAsyncEventHandler<AfterExportStatsEvent>` to audit listing and stats exports in one place. Register it once per event.

A handler exception is logged and does not fail the export. Handlers run one after another; one failing does not stop the others.

**Best effort.** The CSV is built in the browser. After the download starts, the browser reports the export to the server (the `LOG_EXPORT` page command, which needs the **Export** permission and the report permission). The event is an audit signal, not data protection: a user who can see a report can copy its data without exporting, and a modified browser can skip or fake the report. The filter used for the export is not part of the event; `FileName` has the main filter values (dates, grouping, selected IDs).

`ExportName` is the file-name prefix (the part before the first `_`), except `customers-active`, whose file name also has the activity window (for example `customers-active-30d_...`):

| Report               | `ExportName` values                                                                                                                                                                                          |
| -------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Activity counts      | `activity-counts`                                                                                                                                                                                            |
| Top pages            | `top-pages`                                                                                                                                                                                                  |
| New contacts         | `new-contacts`, `new-contacts-share`                                                                                                                                                                         |
| Form submissions     | `form-submissions`, `form-submissions-by-form`                                                                                                                                                               |
| Member registrations | `members-growth`, `members-sign-in-type`, `members-by-role`                                                                                                                                                  |
| Consents             | `consents-events`, `consents-agreed-contacts`, `consents`, `consents-text-versions`                                                                                                                          |
| Email summary        | `email-summary-activity`, `email-summary-emails`, `email-summary-performers`, `email-summary-automated`                                                                                                      |
| Recipient lists      | `recipient-lists-events`, `recipient-lists-subscribers`, `recipient-lists`, `recipient-lists-status`                                                                                                         |
| Content inventory    | `content-inventory-types`, `content-inventory-status`, `content-inventory-age`, `content-inventory-oldest`, `content-inventory-workflow`, `content-inventory-unused-reusable`, `content-inventory-languages` |
| Orders and revenue   | `orders-revenue`, `orders-by-status`, `orders-top-products`                                                                                                                                                  |
| Customers            | `customers-growth`, `customers-active`, `customers-by-country`, `customers-top-states`, `customers-top-by-revenue`, `customers-top-by-orders`, `customers-top-by-items`                                      |
| Event log            | `event-log`, `event-log-sources`, `event-log-sources-xperience`, `event-log-sources-custom`, `event-log-codes`, `event-log-users`                                                                            |

## Data retention

Contact and activity cleanup (configured in **Settings**) deletes old data. A drop in older periods can mean data was deleted, not that activity went down.

## Sample data

In the `examples/DancingGoat` project, use the **Sample data generator** application to create contacts and activities.
