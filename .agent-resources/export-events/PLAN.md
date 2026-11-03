# Plan: Export events

## Goal

Raise a server event after every "Export CSV" in any report, so developers can audit exports the same way they audit product listing exports. This is shared infrastructure, not a report. Read `.agent-resources/simple-stats/PLAN.md` (Export permission notes) and `.agent-resources/nav-sections/` first. Keep complexity low.

## Research

### Product feature (checked 2026-10-01, 31.9)

Docs: <https://docs.kentico.com/documentation/developers-and-admins/customization/extend-the-administration-interface/ui-pages/reference-ui-page-templates/listing-ui-page-template/export-listing-data#run-custom-code-after-an-export>

- After a successful export of a listing page with `EnableExport()`, the product raises `AfterExportListingEvent` (`Kentico.Xperience.Admin.Base`). The event is `AsyncEvent<ExportListingEventData>`, and its data is `ListingPageTypeName`, `UserID` (0 if unknown) and `Timestamp` (server local time).
- Handlers implement `IAsyncEventHandler<AfterExportListingEvent>` (`CMS.Base`) and are registered with `services.AddEventHandler<TEvent, THandler>()`. They run as singletons and cannot change or cancel the export.
- **The library cannot raise this event** (decompiled): the class is `sealed`, its data constructor is `internal`, `AsyncEvent<T>.Data` is get-only (the public constructor gives an empty `new TData()`), and the product raises it from the internal `ListingExportCommandManager` through injected `IEnumerable<IAsyncEventHandler<AfterExportListingEvent>>`. Reflection on the internal constructor is not allowed (fragile).
- **Decision (user, 2026-10-01): best effort is fine.** The CSV stays client-built. A user who can see a report can scrape its data anyway, so the event is an audit signal, not data protection.

## Slices

1. Export events (`implementation-spec-1.md`)

## Decisions

- 2026-10-01 (user): best effort is fine; the CSV stays client-built and the event is an audit signal (see Research).

## Slice log

- 2026-10-01 – Slice 1: `AfterExportStatsEvent`, shared `LOG_EXPORT` command, client `useCsvExport`, usage guide "Audit CSV exports" (`1c9af89`; spec `4a99e44`).
