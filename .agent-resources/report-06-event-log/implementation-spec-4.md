# Slice 4 – Round 4 (user feedback)

1. **`WebFarmMonitor` is an Xperience source**, as an exact source name (not a prefix). SQL: `[Source] = @SourceExactN` combined with the prefix `LIKE`s; C# `IsXperience` uses the same rule (case-insensitive). Tests: `WebFarmMonitor` native; `WebFarmMonitorX` and `My.WebFarmMonitor` custom.
2. **Bug: switching the Top sources toggle also re-rendered (rebuilt) the Top event codes chart.** Fix in shared code: keep the toggle state local to the sources tile (own component), and make the shared charts (`RankedBarChart`, `StackedColumnChart`, `DonutChart`, `CoverageBarChart`) rebuild only when their data changes by content (`React.memo`, content-stable derived rows). Check that other reports' tile toggles do not cause the same issue.
