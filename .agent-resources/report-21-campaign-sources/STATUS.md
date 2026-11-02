# Status

## Current slice

Slices 1–2 done; committed in `4cac184` (2026-10-08). On `main`.

## Components

| Component | State |
| --- | --- |
| Page `UIPages/CampaignSourcesPage.cs` | done |
| Server `Reports/CampaignSources/` | done |
| Client `Client/src/campaign-sources/` | done |
| Unit tests `CampaignSources*Tests.cs, WebPageStatsTests.cs` | done |
| Integration tests | done (`CampaignSourcesIntegrationTests.cs, WebPageStatsIntegrationTests.cs`) |
| `docs/Usage-Guide.md` "Campaign sources", "Campaign sources on the Stats (Labs) tab" | done |
| Stats (Labs) tab section (`UIPages/WebPageStatsPage.cs`, `Reports/WebPageStats/`, `Client/src/web-page-stats/`) | done |
| Shared `Shared/StatsUtm.cs` | done |
| Seed `seed-utm-landings.sql`, `seed-campaign-sources.sql` | done (local dev DB only) |

## Open checks

- Manual "Done when" checks (visual check in DancingGoat, numbers vs native apps): results not recorded in the repo.

## Pending decisions

- None.
