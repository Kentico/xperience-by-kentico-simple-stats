import { numberFormat } from './format';
import { StatsRankedResult } from './types';

/**
 * Texts shared by the campaign views (web page "Stats (Labs)" tab and the Campaign sources report).
 * UTM values come from landing page activities; Xperience does not store them by itself.
 */

/** Empty state when no activity on the site has a UTM source (UTM capture is not set up). */
export const noUtmDataMessage = 'No UTM values are stored on this site. See UTM capture in the usage guide.';

/** Label of an empty UTM content. Mirrors `StatsUtm.NoValueLabel`. */
export const noValueLabel = '(none)';

/** "UTM capture (optional)" section of the usage guide. */
export const utmCaptureGuideUrl =
  'https://github.com/Kentico/xperience-by-kentico-simple-stats/blob/main/docs/Usage-Guide.md#utm-capture-optional';

/** "Top 10 of 25 sources." when the list is cut, else an empty string. */
export function shownText(list: StatsRankedResult, noun: string): string {
  return list.itemCount > list.items.length
    ? `Top ${list.items.length} of ${numberFormat.format(list.itemCount)} ${noun}.`
    : '';
}

/** Joins non-empty sentences with spaces. */
export function sentences(...parts: readonly string[]): string {
  return parts.filter(Boolean).join(' ');
}
