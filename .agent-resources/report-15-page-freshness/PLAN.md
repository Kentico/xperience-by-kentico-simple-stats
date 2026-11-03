# Plan: Report 15 – Page freshness

## Goal

Audience: content marketers. Answers "which pages do people read that we haven't updated in a long time, and which published pages does nobody visit?" simple-stats PLAN "Later candidates → Content management": stale but popular + pages with no visits. Time-range report (visits) combined with current state (last change), in the **Content** section. Read `.agent-resources/simple-stats/PLAN.md`, `.agent-resources/report-02-top-pages/`, `.agent-resources/report-05-content-inventory/` and the Web page stats code (`Reports/WebPageStats/`) first. Build on existing code; do not duplicate.

## Research

### Data (checked in local DB 2026-10-06)

- Page visits: `OM_Activity` with `ActivityType` = `PredefinedActivityType.PAGE_VISIT`, matched to a page by `ActivityWebPageItemGUID` + `ActivityLanguageID` (same rule as `WebPageStatsRepository`). Locally 1,350 page visits with a GUID, Aug 30 – Oct 5 2026.
- Pages: `CMS_WebPageItem` (`WebPageItemGUID`, `WebPageItemContentItemID`, `WebPageItemWebsiteChannelID`, `WebPageItemTreePath`). 67 rows locally, including folders.
- Last change: `CMS_ContentItemLanguageMetadata.ContentItemLanguageMetadataModifiedWhen` (same meaning as Content inventory "last change").
- Published: the variant has a `CMS_ContentItemCommonData` row with the published `VersionStatus` (map with the product enum, as Content inventory does). First publish: `ContentItemCommonDataFirstPublishedWhen`.
- Page folders have no content type and are left out (`ClassType` rule from `ContentInventorySql`).
- Page links: website channel page link as in `StatsChannelItemPaths`.

## Design rules

### Definitions

- **Visits**: page visit activities of the page variant in the range. **Visitors**: distinct contacts.
- **Stale**: last change more than 12 months ago (same threshold as Content inventory, reuse the constant).
- **Stale but popular**: published, stale, and at least 1 visit in the range. Ranked by visits.
- **No visits**: published, first published before the range start (new pages get a fair chance), and 0 visits in the range. Ordered by first publish date, oldest first.
- Counts are per language variant (a page in `en` and `es` is two rows).

## Slices

1. Page freshness (`implementation-spec-1.md`)

## Slice log

- 2026-10-07 – Slice 1: Page freshness report (`1767381`; spec `253afb9`).
