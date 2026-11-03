# Slice 2 – Round 2 (user feedback)

Screenshot `docs/images/dancing-goat-orders-and-revenue-stats.jpg`.

1. **ComboChart line color**: without a fixed color, the line takes the palette color (`chartTheme.ts` tokens) with the largest RGB distance from the columns' color (`getContrastingColor`). No new colors.
2. **Legend markers match the series**: the line's bullets are added after `legend.data.setAll`, so the legend marker is the plain solid line (series color, `strokeWidth` 2). The white-ringed bullet on the marker made it look dashed. Columns, stacks, slices and bars keep square markers that match their fills.
3. **amCharts logo**: every shared chart (StackedColumn, RankedBar, Donut, CoverageBar, Combo) creates its root with `createChartRoot` in `chartTheme.ts` (themes + `paddingBottom: chartLogoSpace` = 28px). The logo sits bottom-left in the root, so it no longer overlaps legends or axis labels. Bar charts with computed heights add the same space. A later amCharts license (`am5.addLicense`) goes in that one helper (code comment there). No license config was added.

Donut colors and filter bar alignment are unchanged.
