# Plan: Report 24 – Google Analytics 4 traffic

## Goal

Source: Claude Docs "Report 23: Overview (operations dashboard) and GA4 traffic" (<https://claude.ai/artifact/9Tafh96N1o5EbDvDjz4hXS>, 2026-10-30), Phase 2. Depends on `.agent-resources/report-23-overview/` (Overview page) being built first.

Goal: show anonymous traffic that GA4 collects next to Xperience data, joined by page, host, campaign and date. Not visitor-level joins.

Start only after the user has done the validation in `implementation-spec-1.md` ("Validation first").

## Research

### One-time setup (documented in the usage guide)

- Google Cloud project: enable the Google Analytics Data API, create a service account, create a JSON key.
  - Organizations created on or after 2024-05-03 block key creation by default (`iam.managed.disableServiceAccountKeyCreation`). An org policy admin must allow it for that project. Say this in the guide.
  - Keyless Workload Identity Federation (for example from an Azure managed identity) is possible for self-hosted deployments. Not in v1. _Not checked whether Kentico SaaS supports it._
- GA4 Admin → Property access management: add the service account email as Viewer, without email notification. _Adding users needs the GA4 Administrator role (from memory; verify)._
- App settings (secret store, never source control): property ID and the credentials JSON.

No marketer sign-in, no OAuth flow, no admin UI settings page in v1.

## Design rules

### Out of scope

- Real-time updates. A GA4 Realtime "visitors right now" tile is a possible later round.
- BigQuery export and visitor-level joins between GA4 and contacts.
- OAuth sign-in to Google from the admin UI.

## Slices

1. GA4 traffic (Overview Phase 2) (`implementation-spec-1.md`)

## Slice log

- None yet.
