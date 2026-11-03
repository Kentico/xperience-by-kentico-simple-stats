# Plan: Report 10 – Consents

## Goal

Tenth report. Audience: marketers, data protection / compliance leads. simple-stats PLAN "Later candidates → Consents → Agreements and revocations per consent over time". Time-range report. Read `.agent-resources/simple-stats/PLAN.md`, `.agent-resources/report-08-customers/` (point-in-time "active customers" series, ratio KPIs, cumulative series), `.agent-resources/report-09-members/` and `.agent-resources/nav-sections/` first; skim `.agent-resources/report-05-content-inventory/` for the coverage infra. Build on existing code; do not duplicate. Keep complexity low.

## Research

### Local data (checked 2026-09-30)

DB access as in `.agent-resources/report-09-members/PLAN.md` (Research).

- `CMS_Consent` (2 rows, user-generated): `ConsentID`, `ConsentName`, `ConsentDisplayName`, `ConsentContent`, `ConsentGuid`, `ConsentLastModified`, `ConsentHash` (hash of the current text). DancingGoat: `DancingGoatTracking`, `DancingGoatCoffeeSampleListForm`. Consents are per project → never hardcode names/IDs.
- `CMS_ConsentAgreement` (seeded by `.agent-resources/report-10-consents/seed-consent-agreements.sql`, re-runnable): `ConsentAgreementContactID` (FK OM_Contact), `ConsentAgreementConsentID` (FK CMS_Consent), `ConsentAgreementRevoked` bit, `ConsentAgreementConsentHash` NULL, `ConsentAgreementTime`. Index on (ContactID, ConsentID). Tracking: 350 agreements / 33 revocations / 323 contacts; Coffee sample: 133 / 18 / 119; Jul 4 – Sep 30.
- **Product semantics** (decompiled `CMS.DataProtection.ConsentAgreementService`, 31.9): `Agree` inserts a row (Revoked 0, hash = current `ConsentHash`); `Revoke` inserts a row (Revoked 1, hash NULL); `IsAgreed` = latest row for contact + consent is not revoked. So the table is an event history; current state = latest row per contact + consent.
- `CMS_ConsentArchive` (0 rows): archived consent text versions (verify columns; hash per version). Agreements whose hash ≠ current `ConsentHash` were given to an older text.
- Contact deletion: FK to `OM_Contact` means agreements go with deleted contacts (verify the product deletes them, don't assume cascade). Revoke may trigger data erasure handlers in projects. Say both in the data note.
- Verify public Info classes (`ConsentInfo`, `ConsentAgreementInfo`, `ConsentArchiveInfo`) and reference them in SQL doc comments.
- Native app: Data protection, `Kentico.Xperience.Application.DataProtection`, slug `data-protection`, icon `xp-doc-user`. Per consent: `ConsentList` → `:` `ConsentEditSection` → `consent-agreements` (`ConsentAgreementList`) (decompiled `Kentico.Xperience.Admin.DigitalMarketing`). Link each consent row to its agreements listing via `IStatsAdminLinks` if the page types are public and the parameter is the consent ID (verify); else link to the consents list, or skip + say why.

## Slices

1. Consents (`implementation-spec-1.md`)

## Slice log

- 2026-09-30 – Slice 1: Consents report (`bebfc36`).
