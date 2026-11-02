# Slice 2 – Round 2 (user feedback)

1. **Top users: change vs previous period.** Same SQL shape as sources/codes (previous count + `Change`, "New" when previous is 0). Update tests + CSV.
2. **Stack order in "Events over time":** bottom to top = Information, Warning, Error (most common at the bottom). Colors stay fixed. Legend, table and CSV series order match.
3. **Page icon:** same as the native Event log application. Find the icon the product uses (the Event log app's `UIApplication` attribute / metadata in the `Kentico.Xperience.Admin.*` assemblies) and use the same `Icons` constant.

Also (round 1 feedback): "Top sources" and "Top event codes" each on their own full-width row at any screen width (no side-by-side).
