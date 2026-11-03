# Plan: Navigation sections

## Goal

8 reports now sit flat under the Simple Stats (Labs) application. Group them into section pages with nested navigation, like the native Content types app ("Content types > Asset configurations > Mass asset upload").

## Research

### Product pattern (decompiled `Kentico.Xperience.Admin.Base` 31.9, verified)

```csharp
[assembly: UIPage(typeof(ContentTypesApplication), "asset-configuration", typeof(AssetConfigurationSection),
    "{$...assetconfigurations$}", "@kentico/xperience-admin-base/SectionLayout", 2147473647)]
[assembly: UIPage(typeof(AssetConfigurationSection), "mass-upload-config", typeof(MassAssetUploadConfigurationEditPage), "...", "@kentico/xperience-admin-base/Edit", 100)]

[UINavigation(true)]
public sealed class AssetConfigurationSection : SecondaryMenuSectionPage
{
    // only needed to pick a non-first child; base returns routes.FirstOrDefault()
    protected override Route GetDefaultRoute(IEnumerable<Route> routes) => ...;
}
```

- `SecondaryMenuSectionPage` is `[UINavigation(false)]` by default → section pages need `[UINavigation(true)]`.
- Template: `TemplateNames.SECTION_LAYOUT`. Base `GetDefaultRoute` = first child route (by order) → auto-navigates to first child. No override needed unless verification shows otherwise.

## Design rules

### Grouping (order in parentheses)

| Section (slug)            | Reports (keep existing order values within section)        |
| ------------------------- | ---------------------------------------------------------- |
| Contacts (`contacts`) 100 | Activity counts, Top pages, New contacts, Form submissions |
| Content (`content`) 200   | Content inventory                                          |
| Commerce (`commerce`) 300 | Orders and revenue, Customers                              |
| System (`system`) 400     | Event log                                                  |

Section icons: pick fitting `Icons.*` (e.g. match the native app categories); section pages may not show icons in nav — check.

## Slices

1. Navigation sections (`implementation-spec-1.md`)

## Slice log

- 2026-09-30 – Slice 1: section pages (Contacts, Content, Commerce, System), permission-aware `StatsNavigation`, "No reports available" page (`fca0826`, with Customers report and Export permission).
