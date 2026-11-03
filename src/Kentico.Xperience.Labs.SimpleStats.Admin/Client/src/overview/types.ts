import { StatsTone, StatsValueComparison, StatsValueKind } from '../shared/types';

/** Tile keys (`OverviewTiles` on the server), in page order. */
export type OverviewTileKey =
  | 'content'
  | 'locks'
  | 'freshness'
  | 'calendar'
  | 'emails'
  | 'forms'
  | 'contacts'
  | 'members'
  | 'orders'
  | 'events'
  | 'traffic';

/** Mirrors `OverviewKpi`. */
export interface OverviewKpi {
  readonly key: string;
  readonly caption: string;
  readonly tooltip: string;
  /** Plural noun, for example "submissions". */
  readonly noun: string;
  readonly kind: StatsValueKind;
  readonly value: number | null;
  /** Missing (`null`) for KPIs without a comparison. */
  readonly comparison: StatsValueComparison | null;
}

/** Mirrors `OverviewRow`. */
export interface OverviewRow {
  readonly key: string;
  readonly label: string;
  readonly detail: string | null;
  /** Server local time, `yyyy-MM-ddTHH:mm:ss`. */
  readonly date: string | null;
  readonly adminPath: string | null;
}

/** Mirrors `OverviewList`. */
export interface OverviewList {
  readonly key: string;
  /** Rows in the full report, not only `rows`. */
  readonly total: number;
  readonly rows: readonly OverviewRow[];
}

/** Mirrors `OverviewAttentionItem`. */
export interface OverviewAttentionItem {
  readonly key: string;
  readonly text: string;
  readonly count: number;
  readonly tone: StatsTone;
  /** Full report, relative to the admin root. */
  readonly adminPath: string | null;
}

/** Mirrors `OverviewTileResult`. */
export interface OverviewTileResult {
  readonly tile: OverviewTileKey;
  /** `false` when the source is not set up (no tables, locking disabled). */
  readonly available: boolean;
  readonly kpis: readonly OverviewKpi[];
  readonly lists: readonly OverviewList[];
  readonly attention: readonly OverviewAttentionItem[];
  readonly reportPath: string | null;
  readonly note: string | null;
  readonly updatedAt: string;
}

/** State of one tile on the page. */
export interface OverviewTileState {
  readonly key: OverviewTileKey;
  /** The user has the source report permission. */
  readonly visible: boolean;
  readonly data: OverviewTileResult | null;
  /** Loading, or not loaded yet. */
  readonly isLoading: boolean;
  readonly hasError: boolean;
}
