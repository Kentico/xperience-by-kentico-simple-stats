# Report 24: Google Analytics 4 traffic

Source: Claude Docs "Report 23: Overview (operations dashboard) and GA4 traffic" (https://claude.ai/artifact/9Tafh96N1o5EbDvDjz4hXS, 2026-10-30), Phase 2. Depends on `REPORT-23-OVERVIEW.md` (Overview page) being built first.

Goal: show anonymous traffic that GA4 collects next to Xperience data, joined by page, host, campaign and date. Not visitor-level joins.

Start only after the user has done the validation below.

## Validation first (user, before the build)

Run Data API queries in the GA4 Query Explorer (signed in as the user) with dimensions `hostName`, `pagePath`, metric `screenPageViews`, last 30 days, against a real property (Community Portal). Compare with Xperience:

- `hostName` values vs website channel domains (Channel management) and language domains.
- Language variants as `/{language}/...` paths, matching what `IWebPageUrlRetriever` returns.
- Home page `/` and trailing slashes vs `WebPageStatsUrlPath.Normalize` (returns an empty string for the root).
- Letter case: GA4 is case-sensitive; Xperience compares without case. Look for one page split across two paths.
- Old paths after renames (`CMS_WebPageFormerUrlPath` could map them; Round 2).
- GA4 views vs Simple Stats Top pages for a few pages, same dates. Expect GA4 to be higher.
- GA4 property time zone vs server time.

Write the findings into this spec as matching rules before the build.

## One-time setup (documented in the usage guide)

- Google Cloud project: enable the Google Analytics Data API, create a service account, create a JSON key.
  - Organizations created on or after 2024-05-03 block key creation by default (`iam.managed.disableServiceAccountKeyCreation`). An org policy admin must allow it for that project. Say this in the guide.
  - Keyless Workload Identity Federation (for example from an Azure managed identity) is possible for self-hosted deployments. Not in v1. _Not checked whether Kentico SaaS supports it._
- GA4 Admin → Property access management: add the service account email as Viewer, without email notification. _Adding users needs the GA4 Administrator role (from memory; verify)._
- App settings (secret store, never source control): property ID and the credentials JSON.

No marketer sign-in, no OAuth flow, no admin UI settings page in v1.

## Configuration

- Options class `SimpleStatsGoogleAnalyticsOptions { PropertyId, CredentialsJson }` bound with `services.AddOptions<>().BindConfiguration("Kentico.Xperience.Labs.SimpleStats:GoogleAnalytics")` in `SimpleStatsWebAdminModule.RegisterServices`. Section name: follow any existing Kentico Labs convention if one exists; otherwise this one.
- GA4 is on only when both values are set. When off, every GA4 part is hidden and the Overview shows a one-line "GA4 not configured" note with a link to the usage guide (administrators only, `IsAdministrator`).
- The credentials never reach the client, logs or exceptions. Error messages name the state only.

## Server

- Client wrapper `IGoogleAnalyticsDataClient` (internal): `RunReport(dimensions, metrics, from, to, filters, limit)` returning plain rows. One implementation; tests use a fake.
- **Decision (user):** the `Google.Analytics.Data.V1Beta` client library (simplest code, brings gRPC dependencies into customer apps) or REST `POST https://analyticsdata.googleapis.com/v1beta/properties/{id}:runReport` with an access token from `Google.Apis.Auth` (fewer dependencies). Check the dependency footprint of both before choosing; report it.
- Google has marked some JSON credential loaders as obsolete. Use the current recommended loader; no obsolete warnings.
- States: `NotConfigured`, `AuthFailed`, `NoPropertyAccess`, `QuotaExceeded`, `Error`, `Ok`. Map API errors to states; log the details server-side as warnings (without credentials).
- Cache: `StatsCache` with a separate, longer expiry for GA4 (proposal 30 minutes, constant `GoogleAnalyticsCacheMinutes`). GA4 standard reports lag behind real time anyway. Refresh still bypasses the cache.
- Quotas (standard property): 200,000 tokens per property per day, 40,000 per hour, 14,000 per project per property per hour, 10 concurrent requests. Request `returnPropertyQuota` and log the remaining hourly tokens at debug level. Never run more than 3 GA4 requests at a time from one page load. _Quota numbers from the source doc; verify against current Google docs._
- Page matching (to be confirmed by the validation above):
  - **One page** (web page Stats (Labs) tab): path from `IWebPageUrlRetriever` exactly as `WebPageStatsPage.GetFormUrlPath` does; hosts from the channel domain, or the language domains when `WebsiteChannelLanguageRoutingMode` is `LanguageDomains` (same code as `GetLanguageHosts`). GA4 filter: `pagePath` equals the path or the path + `/` (home: `/`), case-insensitive; `hostName` in the hosts when known.
  - **Many pages** (Overview, Top pages, Page freshness): query GA4 top N rows (`hostName`, `pagePath`, limit 250) for the range. Normalize with `WebPageStatsUrlPath.Normalize` / `NormalizeHost`. Build a path-to-page map from the live URLs of published pages (`IWebPageUrlRetriever`; check for a batch overload, otherwise query `CMS_WebPageUrlPath` and apply the channel's language routing rules). Rows with no match are "Not in Xperience" (old URLs, 404s, other apps on the domain). Rows with an unknown host are "Other hosts".
  - Optional, not in v1: send the web page GUID to GA4 as an event-scoped custom dimension (`content_id`) and match on it. Needs a change to the site's tracking script. Use only if URL matching proves unreliable.
- GA4 API names used (check against the property's metadata endpoint): dimensions `date`, `hostName`, `pagePath`, `sessionSource`, `sessionMedium`, `sessionCampaignName`; metrics `activeUsers`, `newUsers`, `sessions`, `screenPageViews`, `engagementRate`.
- Tests: fake client; matching (trailing slash, root, language prefix, case, unknown host, unmatched path); states; cache key includes the property ID and range; credentials not in any serialized result.

## Where GA4 shows up

Permission: new `StatsPermissions.GOOGLE_ANALYTICS` = `SimpleStats.GoogleAnalytics`, "Google Analytics data". GA4 parts need it plus the host report's permission.

- **Overview:**
  - KPI cards: visitors (`activeUsers`) and sessions, with change vs previous period.
  - "New content performance": pages first published in the range (Recently published rows) with their GA4 views. Pages with 0 views show 0, not hidden.
  - "Tracked share": Xperience page visit activities ÷ GA4 page views for the range, labeled approximate ("share of page views logged as contact activity"). Hint explains consent and different counting.
  - Attention item (Caution): a page published in the range with 0 GA4 views after 3 days. Constant `NoViewsAfterDays = 3`.
- **Top pages:** a "Source" toggle, Xperience activities / GA4. GA4 view shows views, users, matched page link or "Not in Xperience".
- **Page freshness:** when GA4 is on, "stale but popular" can use GA4 views instead of activities (toggle, default GA4).
- **Web page Stats (Labs) tab:** GA4 views, users, engagement rate for the page and language, with the same range.
- **Campaign sources** (Round 2): GA4 `sessionSource` / `sessionMedium` / `sessionCampaignName` per landing page. Fills the gap that Xperience does not store `utm_medium` or `utm_campaign`.

## Client

- A small "GA4" badge on every GA4 number, and a hint "From Google Analytics. May be up to a day behind. Dates use the GA4 property time zone."
- States: "not configured" (admins only), "no access to the property", "quota reached, try later", "error". Never an empty chart with no reason.

## Done when

- `REPORT-23-OVERVIEW.md` "Done when" rules, plus:
- Numbers for 5 pages match the GA4 UI (Reports → Engagement → Pages and screens) for the same dates, within GA4's own rounding.
- Matching results for the validation property written in the report-back: matched rows, "Not in Xperience" rows, "Other hosts" rows.
- Usage guide: setup steps (including the org policy note), app settings, permission row, quotas, data lag, time zone, what "Not in Xperience" means.
- Credentials checked absent from client properties, command responses and logs.

## Out of scope

- Real-time updates. A GA4 Realtime "visitors right now" tile is a possible later round.
- BigQuery export and visitor-level joins between GA4 and contacts.
- OAuth sign-in to Google from the admin UI.

## Open questions

- Client library or REST (Server, decision above).

## Report back

Concise: files, dependency footprint and choice, matching results, permission checks, checked, not checked, open questions.
