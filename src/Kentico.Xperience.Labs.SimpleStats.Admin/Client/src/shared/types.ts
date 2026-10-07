import type { Colors } from '@kentico/xperience-admin-components';

/** Mirrors `Kentico.Xperience.Labs.SimpleStats.Admin.Shared.StatsGrouping`. */
export type StatsGrouping = 'Day' | 'Week' | 'Month';

/** Mirrors `StatsFilter`. Dates are `yyyy-MM-dd` (server date, no time zone). */
export interface StatsFilter {
  readonly from: string;
  readonly to: string;
  readonly grouping: StatsGrouping;
  readonly channelId: number | null;
}

/**
 * Mirrors `StatsSnapshotFilter`: filter of a current-state report (no date range or grouping).
 * `null` means all.
 */
export interface StatsSnapshotFilter {
  /** Report-specific kind, for example a content type type (`Website`). */
  readonly kind: string | null;
  readonly channelId: number | null;
  /** Days the report looks ahead or back from now. Only reports with a window send it. */
  readonly window?: number | null;
  /** Content type (class) ID. Only reports with a content type filter send it. */
  readonly contentTypeId?: number | null;
}

/**
 * Mirrors `StatsLoadRequest` (input of the `LOAD` page command),
 * or `StatsSnapshotLoadRequest` with a `StatsSnapshotFilter`.
 */
export interface StatsLoadRequest<TFilter = StatsFilter> {
  readonly filter: TFilter;
  /** Drop cached data for the filter and read it again from the database. */
  readonly refresh: boolean;
}

/** Mirrors `StatsPeriod`. */
export interface StatsPeriod {
  readonly start: string;
  readonly label: string;
}

/** Mirrors `StatsChannelOption`. */
export interface StatsChannelOption {
  readonly id: number;
  readonly displayName: string;
  readonly type: string;
}

/** Mirrors `StatsRankedItem`: one row of a ranked list. */
export interface StatsRankedItem {
  /** 1-based position. */
  readonly rank: number;
  /** Stable, unique identifier (for example the URL). */
  readonly key: string;
  readonly label: string;
  readonly secondaryLabel: string | null;
  readonly value: number;
  readonly secondaryValue: number | null;
  /** Share of `StatsRankedResult.total` (0–1). */
  readonly share: number;
  /** Absolute link opened in a new tab. */
  readonly url: string | null;
  /**
   * Native admin page of the item, relative to the admin root (see `adminLinks.ts`).
   * Opened in the same tab. `null` or missing when the report has no admin links.
   */
  readonly adminPath?: string | null;
  /**
   * Value in the previous period of the same length. Missing when the report does not compare periods.
   */
  readonly previousValue?: number | null;
  /**
   * Change vs `previousValue` as a ratio (0.12 = +12%). Missing when the report does not compare periods
   * or `previousValue` is 0.
   */
  readonly change?: number | null;
  /** `value` formatted by the project's price formatter (amounts only). Missing: format by kind. */
  readonly valueText?: string;
  /** `secondaryValue` formatted like `valueText`. */
  readonly secondaryValueText?: string;
  /** `previousValue` formatted like `valueText`. */
  readonly previousValueText?: string;
  /** Optional third number (for example item quantity next to revenue and orders). Missing for lists with up to two values. */
  readonly tertiaryValue?: number | null;
  /** `tertiaryValue` formatted like `valueText`. */
  readonly tertiaryValueText?: string;
  /** Meaning of the item for chart colors (for example an order status). Missing for lists without it. */
  readonly tone?: StatsTone;
}

/** Mirrors `StatsTone`: meaning of an item for chart colors (traffic light). */
export type StatsTone = 'Neutral' | 'Problem' | 'Caution' | 'Done';

/** Mirrors `StatsRankedResult`: ranked list for one range and channel. */
export interface StatsRankedResult {
  readonly from: string;
  readonly to: string;
  readonly channelId: number | null;
  /** Top items, largest value first. */
  readonly items: readonly StatsRankedItem[];
  /** Sum of values over all items in the range (not only `items`). */
  readonly total: number;
  /** Number of distinct items in the range (not only `items`). */
  readonly itemCount: number;
  /** ISO timestamp of when the data was read from the database. */
  readonly updatedAt: string;
  /** What `value`, `previousValue` and `total` measure. Missing means `Count`. */
  readonly valueKind?: StatsValueKind;
  /** What `secondaryValue` measures. Missing means `Count`. */
  readonly secondaryValueKind?: StatsValueKind;
  /** What `tertiaryValue` measures. Missing means `Count`. */
  readonly tertiaryValueKind?: StatsValueKind;
  /** `total` formatted by the project's price formatter (amounts only). */
  readonly totalText?: string;
}

/** Column captions of a ranked list, used by the table, chart tooltip and CSV. */
export interface StatsRankedCaptions {
  /** Caption of the label column, for example "Page". */
  readonly label: string;
  /** Caption of the secondary label, for example "Title". Omit to hide it. */
  readonly secondaryLabel?: string;
  /** Caption of the value, for example "Visits". */
  readonly value: string;
  /** Caption of the secondary value, for example "Unique contacts". Omit to hide it. */
  readonly secondaryValue?: string;
  /** Caption of the third value, for example "Items". Omit to hide it. */
  readonly tertiaryValue?: string;
  /**
   * Caption of the previous period value, for example "Previous 30 days". Set it (with `change`) for lists
   * that compare periods; omit to hide both columns.
   */
  readonly previousValue?: string;
  /** Caption of the change column, for example "Change". Omit to hide it. */
  readonly change?: string;
  /** Format of the value and previous value (for example `Amount` for revenue). Default `Count`. */
  readonly valueKind?: StatsValueKind;
  /** Format of the secondary value. Default `Count`. */
  readonly secondaryValueKind?: StatsValueKind;
  /** Format of the third value. Default `Count`. */
  readonly tertiaryValueKind?: StatsValueKind;
}

/** One chart series. `values` aligns with the period axis. */
export interface StatsSeries {
  readonly key: string;
  readonly name: string;
  readonly values: readonly number[];
  /** Fixed color token (for example for severities). Omit to use the next palette color. */
  readonly color?: Colors;
  /** Format of the values in tables and tooltips. Default `Count`. */
  readonly kind?: StatsValueKind;
  /** `values` formatted by the project's price formatter, aligned with `values`. Missing: format by `kind`. */
  readonly texts?: readonly string[];
}

/** Mirrors `StatsTimeSeries`: one series of a time series report. */
export interface StatsTimeSeries {
  readonly key: string;
  readonly displayName: string;
  /** Count per period (zero-filled). Aligns with `StatsTimeSeriesResult.periods`. */
  readonly values: readonly number[];
  readonly total: number;
}

/** Mirrors `StatsTimeSeriesResult`: counts per series per period for one range. */
export interface StatsTimeSeriesResult {
  readonly from: string;
  readonly to: string;
  readonly grouping: StatsGrouping;
  /** `null` for all channels or reports without a channel. */
  readonly channelId: number | null;
  readonly periods: readonly StatsPeriod[];
  /** Series in display order. */
  readonly series: readonly StatsTimeSeries[];
  readonly total: number;
  /** ISO timestamp of when the data was read from the database. */
  readonly updatedAt: string;
}

/** One slice of a share (donut) chart or table. */
export interface StatsShareSlice {
  readonly key: string;
  readonly name: string;
  readonly value: number;
  /** Optional second number shown in tables and CSV (for example revenue next to orders). */
  readonly secondaryValue?: number | null;
  /** `secondaryValue` formatted by the project's price formatter. Missing: format the number. */
  readonly secondaryValueText?: string | null;
  /**
   * Fixed slice color (for example a status color). Missing: the next palette color.
   * Later slices with the same color get lighter variants of it, so they stay apart.
   */
  readonly color?: Colors;
}

/** Mirrors `StatsCoverageItem`: "x of y" row, for example items with a language variant. */
export interface StatsCoverageItem {
  /** Stable, unique identifier (for example a language code name). */
  readonly key: string;
  readonly label: string;
  readonly secondaryLabel: string | null;
  readonly covered: number;
  readonly total: number;
  /** `total` - `covered`. */
  readonly missing: number;
  /** `covered` / `total` (0–1). */
  readonly share: number;
}

/** Mirrors `StatsComparison`: a value in the range compared with the previous period of the same length. */
export interface StatsComparison {
  readonly current: number;
  readonly previous: number;
  /** Relative change as a ratio (0.12 = +12%). `null` when `previous` is 0. */
  readonly change: number | null;
  /** First day of the previous period (`yyyy-MM-dd`). */
  readonly previousFrom: string;
  /** Last day of the previous period (`yyyy-MM-dd`). */
  readonly previousTo: string;
}

/** Mirrors `StatsAgedItem`: one row of an "oldest first" list. */
export interface StatsAgedItem {
  /** Stable, unique identifier. */
  readonly key: string;
  readonly label: string;
  /** For example the content type. */
  readonly category: string | null;
  readonly language: string | null;
  /** For example the workflow step. */
  readonly detail: string | null;
  /** Date the age is counted from (`yyyy-MM-dd`, server date). */
  readonly since: string;
  /** Whole days since `since`. */
  readonly days: number;
  /** Native admin page, relative to the admin root (see `adminLinks.ts`). */
  readonly adminPath?: string | null;
  /** Optional channel text (for example "Content hub - Marketing" for reusable items, set by the server). Left out when not used. */
  readonly channel?: string | null;
  /** Optional date of the last change (`yyyy-MM-dd`, server date) when the age counts from something else (for example a lock). */
  readonly lastModified?: string | null;
}

/**
 * Mirrors `StatsValueKind`: what a value measures, so shared components format it
 * (see `formatValue`). Count values can be fractional (for example item quantities).
 */
export type StatsValueKind = 'Count' | 'Amount' | 'Ratio';

/**
 * Mirrors `StatsValueComparison`: a decimal value (count, amount or ratio) in the range compared with
 * the previous period of the same length. Values are `null` when they cannot be computed
 * (for example an average without orders).
 */
export interface StatsValueComparison {
  readonly kind: StatsValueKind;
  readonly current: number | null;
  readonly previous: number | null;
  /**
   * Relative change as a ratio (0.12 = +12%). `null` when a value is `null` or `previous` is 0.
   * For `Ratio` values it is the difference in ratio points (0.05 = +5 percentage points), `null` only when a value is `null`.
   */
  readonly change: number | null;
  /** First day of the previous period (`yyyy-MM-dd`). */
  readonly previousFrom: string;
  /** Last day of the previous period (`yyyy-MM-dd`). */
  readonly previousTo: string;
  /** `current` formatted by the project's price formatter (amounts only). Missing: format the number. */
  readonly currentText?: string;
  /** `previous` formatted like `currentText`. */
  readonly previousText?: string;
}

/** Mirrors `StatsValueSeries`: one decimal series aligned with a period axis. */
export interface StatsValueSeries {
  readonly key: string;
  readonly displayName: string;
  readonly kind: StatsValueKind;
  /** Value per period (zero-filled, amounts rounded to 2 decimals). */
  readonly values: readonly number[];
  readonly total: number;
  /** `values` formatted by the project's price formatter (amounts only). Missing: format the numbers. */
  readonly texts?: readonly string[];
  /** `total` formatted like `texts`. */
  readonly totalText?: string;
}
