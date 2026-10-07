import { formatItemChange } from './format';
import { periodTotals } from './timeSeries';
import {
  StatsAgedItem,
  StatsCoverageItem,
  StatsPeriod,
  StatsRankedCaptions,
  StatsRankedItem,
  StatsSeries,
  StatsShareSlice,
} from './types';

export type CsvValue = string | number | null | undefined;

/** CSV text and its number of data rows (header not counted), so an export can be logged with its row count. */
export interface CsvData {
  readonly text: string;
  readonly rowCount: number;
}

function escapeCell(value: CsvValue): string {
  if (value === null || value === undefined) {
    return '';
  }

  let text = String(value);

  // Prevent spreadsheet formula injection from text values.
  if (typeof value === 'string' && /^[=+\-@\t\r]/.test(text)) {
    text = `'${text}`;
  }

  return /[",\r\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text;
}

/** Builds RFC 4180 CSV text from a header row and data rows. */
export function toCsv(
  header: readonly CsvValue[],
  rows: readonly (readonly CsvValue[])[],
): CsvData {
  return {
    text: [header, ...rows].map((row) => row.map(escapeCell).join(',')).join('\r\n'),
    rowCount: rows.length,
  };
}

/**
 * Builds CSV text for a ranked list: rank, label, optional secondary label, value,
 * optional secondary and third value, optional previous value and change (%, one decimal), share (%, one decimal) and URL (the absolute URL, else the admin link
 * from `getAdminHref` made absolute).
 */
export function toRankedCsv(
  items: readonly StatsRankedItem[],
  captions: StatsRankedCaptions,
  getAdminHref?: (item: StatsRankedItem) => string | null,
): CsvData {
  const header: CsvValue[] = [
    'Rank',
    captions.label,
    ...(captions.secondaryLabel ? [captions.secondaryLabel] : []),
    captions.value,
    ...(captions.secondaryValue ? [captions.secondaryValue] : []),
    ...(captions.tertiaryValue ? [captions.tertiaryValue] : []),
    ...(captions.previousValue ? [captions.previousValue] : []),
    ...(captions.change ? [`${captions.change} (%)`] : []),
    'Share (%)',
    'URL',
  ];

  const rows = items.map((item): CsvValue[] => [
    item.rank,
    item.label,
    ...(captions.secondaryLabel ? [item.secondaryLabel] : []),
    item.value,
    ...(captions.secondaryValue ? [item.secondaryValue] : []),
    ...(captions.tertiaryValue ? [item.tertiaryValue] : []),
    ...(captions.previousValue ? [item.previousValue] : []),
    ...(captions.change ? [toChangeCsv(item)] : []),
    Math.round(item.share * 1000) / 10,
    item.url ?? toAbsoluteUrl(getAdminHref?.(item) ?? null),
  ]);

  return toCsv(header, rows);
}

/**
 * Builds CSV text for a time series: period start, period label, one column per series, and a total
 * (leave it out with `includeTotal: false` when series measure different things, for example orders and revenue).
 * Values are raw numbers (dot decimal, no grouping).
 */
export function toTimeSeriesCsv(
  periods: readonly StatsPeriod[],
  series: readonly StatsSeries[],
  options: { readonly includeTotal?: boolean } = {},
): CsvData {
  const includeTotal = options.includeTotal ?? true;
  const totals = periodTotals(periods, series);
  return toCsv(
    ['Period start', 'Period', ...series.map((s) => s.name), ...(includeTotal ? ['Total'] : [])],
    periods.map((period, index) => [
      period.start,
      period.label,
      ...series.map((s) => s.values[index] ?? 0),
      ...(includeTotal ? [totals[index]] : []),
    ]),
  );
}

/**
 * Builds CSV text for share slices: name, value, optional secondary value (with `secondaryValueCaption`),
 * share (%, one decimal). Values are raw numbers.
 */
export function toShareCsv(
  slices: readonly StatsShareSlice[],
  labelCaption: string,
  valueCaption: string,
  secondaryValueCaption?: string,
): CsvData {
  const total = slices.reduce((sum, s) => sum + s.value, 0);
  return toCsv(
    [labelCaption, valueCaption, ...(secondaryValueCaption ? [secondaryValueCaption] : []), 'Share (%)'],
    slices.map((slice) => [
      slice.name,
      slice.value,
      ...(secondaryValueCaption ? [slice.secondaryValue] : []),
      total > 0 ? Math.round((slice.value / total) * 1000) / 10 : 0,
    ]),
  );
}

/** Builds CSV text for "x of y" rows: label, optional secondary label, covered, missing, total, share (%, one decimal). */
export function toCoverageCsv(
  items: readonly StatsCoverageItem[],
  captions: {
    readonly label: string;
    readonly secondaryLabel?: string;
    readonly covered: string;
    readonly missing: string;
  },
): CsvData {
  return toCsv(
    [
      captions.label,
      ...(captions.secondaryLabel ? [captions.secondaryLabel] : []),
      captions.covered,
      captions.missing,
      'Total',
      'Share (%)',
    ],
    items.map((item) => [
      item.label,
      ...(captions.secondaryLabel ? [item.secondaryLabel] : []),
      item.covered,
      item.missing,
      item.total,
      Math.round(item.share * 1000) / 10,
    ]),
  );
}

/**
 * Builds CSV text for an aged item list: label, optional category / language / channel / detail, since,
 * optional last modified, optional until, days and URL (the admin link from `getAdminHref` made absolute).
 */
export function toAgedCsv(
  items: readonly StatsAgedItem[],
  captions: {
    readonly label: string;
    readonly category?: string;
    readonly language?: string;
    readonly channel?: string;
    readonly detail?: string;
    readonly since: string;
    readonly lastModified?: string;
    readonly until?: string;
    readonly days: string;
  },
  getAdminHref?: (item: StatsAgedItem) => string | null,
): CsvData {
  return toCsv(
    [
      captions.label,
      ...(captions.category ? [captions.category] : []),
      ...(captions.language ? [captions.language] : []),
      ...(captions.channel ? [captions.channel] : []),
      ...(captions.detail ? [captions.detail] : []),
      captions.since,
      ...(captions.lastModified ? [captions.lastModified] : []),
      ...(captions.until ? [captions.until] : []),
      captions.days,
      'URL',
    ],
    items.map((item) => [
      item.label,
      ...(captions.category ? [item.category] : []),
      ...(captions.language ? [item.language] : []),
      ...(captions.channel ? [item.channel ?? null] : []),
      ...(captions.detail ? [item.detail] : []),
      item.since,
      ...(captions.lastModified ? [item.lastModified ?? null] : []),
      ...(captions.until ? [item.until ?? null] : []),
      item.days,
      toAbsoluteUrl(getAdminHref?.(item) ?? null),
    ]),
  );
}

/** Starts a browser download of CSV text. Adds a BOM so Excel reads UTF-8. */
export function downloadCsv(fileName: string, csv: CsvData): void {
  const blob = new Blob(['﻿', csv.text], { type: 'text/csv;charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

/** Change in % with one decimal; "New" or empty when there is no ratio (see `formatItemChange`). */
function toChangeCsv(item: StatsRankedItem): CsvValue {
  if (item.change !== null && item.change !== undefined) {
    return Math.round(item.change * 1000) / 10;
  }
  return formatItemChange(item) === 'New' ? 'New' : null;
}

/** Makes a same-origin path absolute, so CSV links work outside the admin. */
export function toAbsoluteUrl(path: string | null): string | null {
  return path ? new URL(path, window.location.origin).toString() : null;
}
