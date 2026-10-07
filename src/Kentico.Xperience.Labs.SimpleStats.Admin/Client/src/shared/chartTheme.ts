import * as am5 from '@amcharts/amcharts5';
import am5ThemesAnimated from '@amcharts/amcharts5/themes/Animated';
import { Colors } from '@kentico/xperience-admin-components';

/**
 * Same font rule as the admin design system chart theme
 * (xperience-by-kentico-admin-design-components `Charts/ChartTheme.ts`).
 */
export function getXbkTheme(root: am5.Root): am5.Theme {
  const theme = am5.Theme.new(root);
  theme.rule('Label').setAll({
    fontFamily: 'GT Walsheim, sans-serif',
  });
  return theme;
}

/**
 * Resolves a `Colors` token (`var(--name)`) to its current value on `element`.
 * amCharts draws on canvas, so it cannot use CSS custom properties directly.
 * The admin defines the tokens on its theme wrapper (light or dark theme class), not on `<html>`,
 * so pass an element inside the admin, for example the chart's `root.dom`.
 */
export function resolveToken(token: Colors, element: Element): string | undefined {
  const name = /var\((--[^)]+)\)/.exec(token)?.[1];
  if (!name) {
    return undefined;
  }
  const value = getComputedStyle(element).getPropertyValue(name).trim();
  return value.startsWith('#') || value.startsWith('rgb') ? value : undefined;
}

/**
 * Solid admin color tokens used as the series palette, in order: the product color, then the tag colors.
 * No status colors (alert, warning, success, info): series and slices are plain categories (for example
 * project-defined order statuses), and red or green would suggest a meaning they do not have.
 * Charts that need a meaning set a fixed color per series (for example event log types) or use `highlightFrom`.
 * Neon green and rose are left out (they read as success and alert).
 */
const paletteTokens: Colors[] = [
  Colors.Product,
  Colors.BackgroundTagSkyBlue,
  Colors.BackgroundTagYellow,
  Colors.BackgroundTagKontentTurquoise,
  Colors.BackgroundTagUltramarineBlue,
  Colors.BackgroundTagWarmGrey,
  Colors.BackgroundTagMajorelleBlue,
  Colors.BackgroundTagKenticoOrange,
];

export function getSeriesPalette(element: Element): am5.Color[] {
  return paletteTokens
    .map((token) => resolveToken(token, element))
    .filter((value): value is string => value !== undefined)
    .map((value) => am5.color(value));
}

export interface ChartTokens {
  readonly text: am5.Color;
  readonly textLow: am5.Color;
  readonly grid: am5.Color;
  readonly tooltip: am5.Color;
  readonly tooltipText: am5.Color;
  /** Card background, used to separate slices. */
  readonly surface: am5.Color;
}

export function getChartTokens(element: Element): ChartTokens {
  const read = (token: Colors, fallback: string) =>
    am5.color(resolveToken(token, element) ?? fallback);

  // Fallbacks match tokens.css values in case a token is missing at runtime.
  return {
    text: read(Colors.TextDefaultOnLight, '#151515'),
    textLow: read(Colors.TextLowEmphasis, '#525252'),
    grid: read(Colors.DividerDefault, '#dfdfdf'),
    tooltip: read(Colors.TooltipBackground, '#151515'),
    tooltipText: read(Colors.TextDefaultOnDark, '#ffffff'),
    surface: read(Colors.PaperBackground, '#ffffff'),
  };
}

/**
 * Height (px) kept free at the bottom of every chart for the amCharts logo, which amCharts draws
 * in the bottom-left corner of the root when no license is set. Keeps it off legends and axis labels.
 * Charts with a computed height add it to their height.
 */
export const chartLogoSpace = 28;

/**
 * Creates the amCharts root of every shared chart, with the admin themes and room for the amCharts logo.
 * All charts must create their root here, so chart-wide setup lives in one place.
 * An amCharts license (`am5.addLicense(...)`, which also removes the logo) can be added here later.
 */
export function createChartRoot(elementId: string): am5.Root {
  const root = am5.Root.new(elementId);
  root.setThemes([am5ThemesAnimated.new(root), getXbkTheme(root)]);
  root.container.set('paddingBottom', chartLogoSpace);
  return root;
}

/**
 * Returns the palette color that differs most from `base` (RGB distance), for a second series that must
 * stand out from the first (for example a line over columns). Falls back to `base`.
 */
export function getContrastingColor(palette: readonly am5.Color[], base: am5.Color | undefined): am5.Color | undefined {
  if (!base) {
    return palette[1] ?? palette[0];
  }
  const distance = (a: am5.Color, b: am5.Color) =>
    (a.r - b.r) ** 2 + (a.g - b.g) ** 2 + (a.b - b.b) ** 2;
  return palette.reduce<am5.Color>((best, c) => (distance(c, base) > distance(best, base) ? c : best), base);
}
