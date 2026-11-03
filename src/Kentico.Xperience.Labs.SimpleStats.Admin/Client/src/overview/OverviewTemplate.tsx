import {
  Callout,
  CalloutPlacementType,
  CalloutType,
  InfoCard,
  NameToggleButtons,
} from '@kentico/xperience-admin-components';
import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';

import { toAdminHref } from '../shared/adminLinks';
import { ComparisonInfoCard } from '../shared/ComparisonInfoCard';
import { addDays } from '../shared/dates';
import { DataRetentionNote } from '../shared/DataRetentionNote';
import { RefreshControl } from '../shared/filterControls';
import { formatValue, numberFormat } from '../shared/format';
import { StatsFilter, StatsSnapshotFilter, StatsValueComparison } from '../shared/types';
import { StatsCommandState, useStatsCommand } from '../shared/useStatsCommand';
import { AttentionList } from './AttentionList';
import { formatUpdated, oldest } from './format';
import { OverviewCard } from './OverviewCard';
import { OverviewRows } from './OverviewRows';
import { OverviewKpi, OverviewList, OverviewTileKey, OverviewTileResult, OverviewTileState } from './types';
import '../shared/stats.css';
import './overview.css';

/** Mirrors `OverviewClientProperties`. */
interface OverviewTemplateProps {
  /** Tiles the user may see (source report permissions), in page order. */
  readonly tiles: readonly OverviewTileKey[];
  /** Range options in days. */
  readonly rangeDays: readonly number[];
  readonly defaultRangeDays: number;
  /** Days ahead of "Coming up". */
  readonly calendarWindow: number;
  /** Server date (`yyyy-MM-dd`) the ranges end on. */
  readonly today: string;
  readonly pagePath: string | null;
}

/** Page commands per tile (`OverviewTiles` on the server). Snapshot tiles do not follow the range. */
const tileCommands: Record<OverviewTileKey, { readonly command: string; readonly snapshot: boolean }> = {
  content: { command: 'LOAD_CONTENT', snapshot: true },
  locks: { command: 'LOAD_LOCKS', snapshot: true },
  freshness: { command: 'LOAD_FRESHNESS', snapshot: false },
  calendar: { command: 'LOAD_CALENDAR', snapshot: true },
  emails: { command: 'LOAD_EMAILS', snapshot: false },
  forms: { command: 'LOAD_FORMS', snapshot: false },
  contacts: { command: 'LOAD_CONTACTS', snapshot: false },
  members: { command: 'LOAD_MEMBERS', snapshot: false },
  orders: { command: 'LOAD_ORDERS', snapshot: false },
  events: { command: 'LOAD_EVENTS', snapshot: false },
  traffic: { command: 'LOAD_TRAFFIC', snapshot: false },
};

/** Tiles that can add "needs attention" items. */
const attentionTiles: readonly OverviewTileKey[] = ['content', 'locks', 'freshness', 'events'];

/** Short notes for tiles whose source is not set up. */
const notSetUp: Partial<Record<OverviewTileKey, string>> = {
  locks: 'content locking (turned off in Settings, so old locks are not checked)',
  emails: 'emails (no email tables)',
  members: 'member registrations (no member tables)',
  orders: 'commerce (no commerce tables)',
};

const snapshotFilter: StatsSnapshotFilter = { kind: null, channelId: null };

type TileCommand = StatsCommandState<OverviewTileResult | null, StatsFilter | StatsSnapshotFilter>;

function useTile(key: OverviewTileKey): TileCommand {
  return useStatsCommand<OverviewTileResult | null, StatsFilter | StatsSnapshotFilter>(null, tileCommands[key].command);
}

export const OverviewTemplate = (props: OverviewTemplateProps) => {
  const { pagePath, today } = props;
  const [days, setDays] = useState(props.defaultRangeDays);

  // One command per tile, called in a fixed order. Each loads on its own, so a slow or failing tile does not block the others.
  const commands: Record<OverviewTileKey, TileCommand> = {
    content: useTile('content'),
    locks: useTile('locks'),
    freshness: useTile('freshness'),
    calendar: useTile('calendar'),
    emails: useTile('emails'),
    forms: useTile('forms'),
    contacts: useTile('contacts'),
    members: useTile('members'),
    orders: useTile('orders'),
    events: useTile('events'),
    traffic: useTile('traffic'),
  };
  const commandsRef = useRef(commands);
  commandsRef.current = commands;

  const visible = useMemo(() => new Set(props.tiles), [props.tiles]);
  const filter = useMemo<StatsFilter>(
    () => ({ from: addDays(today, -(days - 1)), to: today, grouping: 'Day', channelId: null }),
    [today, days],
  );

  const loadTiles = useCallback(
    (snapshot: boolean, refresh: boolean) => {
      for (const key of props.tiles) {
        if (tileCommands[key].snapshot === snapshot) {
          void commandsRef.current[key].load(snapshot ? snapshotFilter : filter, { refresh });
        }
      }
    },
    [props.tiles, filter],
  );

  // Current-state tiles load once; range tiles load on every range change.
  useEffect(() => loadTiles(true, false), []); // eslint-disable-line react-hooks/exhaustive-deps
  useEffect(() => loadTiles(false, false), [loadTiles]);

  const handleRefresh = () => {
    loadTiles(true, true);
    loadTiles(false, true);
  };

  const tiles = Object.fromEntries(
    (Object.keys(tileCommands) as OverviewTileKey[]).map((key) => {
      const { data, isLoading, hasError } = commands[key];
      const state: OverviewTileState = {
        key,
        visible: visible.has(key),
        data,
        isLoading: isLoading || (visible.has(key) && data === null && !hasError),
        hasError,
      };
      return [key, state];
    }),
  ) as Record<OverviewTileKey, OverviewTileState>;

  const getHref = useCallback((path: string | null) => toAdminHref(path, pagePath), [pagePath]);
  const visibleStates = props.tiles.map((key) => tiles[key]);
  const anyLoading = visibleStates.some((t) => t.isLoading);
  const updatedAt = oldest(visibleStates.flatMap((t) => (t.data ? [t.data.updatedAt] : [])));

  if (props.tiles.length === 0) {
    return (
      <div className="SimpleStats-root">
        <Callout type={CalloutType.FriendlyWarning} placement={CalloutPlacementType.OnDesk} headline="Nothing to show yet">
          <p>
            Each part of the Overview needs the permission of the report it comes from (for example Form submissions or
            Event log). Your role has the Overview permission but none of these report permissions. Ask an administrator to
            add report permissions to your role in Role management.
          </p>
        </Callout>
      </div>
    );
  }

  const rangeText = days === 1 ? 'Last day' : `Last ${days} days`;
  const attentionStates = attentionTiles.map((key) => tiles[key]).filter((t) => t.visible);
  const attentionItems = attentionStates.flatMap((t) => (t.data?.available ? t.data.attention : []));
  const attentionLoading = attentionStates.some((t) => t.isLoading);
  const attentionErrors = attentionStates.filter((t) => t.hasError).length;
  const notSetUpTexts = visibleStates.flatMap((t) => (t.data && !t.data.available && notSetUp[t.key] ? [notSetUp[t.key]!] : []));
  const summary = anyLoading ? null : buildSummary(rangeText, days, tiles);

  const calendar = tiles.calendar;
  const calendarHref = getHref(calendar.data?.reportPath ?? null);

  return (
    <div className="SimpleStats-root">
      <div className="SimpleStats-filterBar">
        <div className="SimpleStats-filterItem">
          <span className="SimpleStats-label">Date range</span>
          <NameToggleButtons
            items={props.rangeDays.map((d) => ({ id: String(d), label: `${d} days` }))}
            selectedItemId={String(days)}
            onChange={(id) => setDays(Number(id))}
          />
        </div>
        <RefreshControl onRefresh={handleRefresh} isLoading={anyLoading} updatedAt={updatedAt} />
      </div>

      {attentionStates.length > 0 && (
        <OverviewCard
          headline="Needs attention"
          description="Problems first, then items to check. Days in a workflow step are based on the last change of the item. Counts are per language variant."
          isLoading={attentionLoading && attentionItems.length === 0}
          hasError={false}
        >
          {attentionItems.length > 0 ? (
            <AttentionList items={attentionItems} getHref={getHref} />
          ) : (
            <div className="SimpleStats-empty">Nothing needs attention.</div>
          )}
          {attentionLoading && attentionItems.length > 0 && <div className="SimpleStats-overviewMore">Still checking…</div>}
          {attentionErrors > 0 && (
            <div className="SimpleStats-error">Some checks could not be loaded. Select Refresh to try again.</div>
          )}
        </OverviewCard>
      )}

      {summary && <p className="SimpleStats-overviewSummary">{summary}</p>}

      <div className="SimpleStats-kpis">
        <KpiCard tile={tiles.calendar} kpiKey="published" rangeText="Last 7 days" />
        <KpiCard tile={tiles.forms} kpiKey="submissions" rangeText={rangeText} />
        <KpiCard tile={tiles.contacts} kpiKey="new-contacts" rangeText={rangeText} />
        <KpiCard tile={tiles.members} kpiKey="new-members" rangeText={rangeText} />
        <KpiCard tile={tiles.emails} kpiKey="click-rate" rangeText={rangeText} />
        <KpiCard tile={tiles.orders} kpiKey="revenue" rangeText={rangeText} />
        <KpiCard tile={tiles.events} kpiKey="errors" rangeText={rangeText} />
        <KpiCard tile={tiles.traffic} kpiKey="activities" rangeText={rangeText} />
      </div>
      {notSetUpTexts.length > 0 && <p className="SimpleStats-overviewMore">Not set up in this project: {notSetUpTexts.join('; ')}.</p>}

      {calendar.visible && (
        <div className="SimpleStats-tiles SimpleStats-tiles--halves">
          <OverviewCard
            headline={`Coming up (next ${props.calendarWindow} days)`}
            description="Scheduled publishes, unpublishes and email sends. Editing a scheduled item cancels its schedule."
            isLoading={calendar.isLoading}
            hasError={calendar.hasError}
            updatedAt={calendar.data?.updatedAt}
            reportHref={calendarHref}
            reportLabel="Open Publishing calendar"
          >
            <ListRows tile={calendar} listKey="upcoming" emptyMessage="Nothing is scheduled in the next days." getHref={getHref} />
          </OverviewCard>

          <OverviewCard
            headline="Recently published"
            description="Published in the last 7 days. This is a fixed window of the Publishing calendar, not the selected range."
            isLoading={calendar.isLoading}
            hasError={calendar.hasError}
            updatedAt={calendar.data?.updatedAt}
            reportHref={calendarHref}
            reportLabel="Open Publishing calendar"
          >
            <ListRows tile={calendar} listKey="recent" emptyMessage="Nothing was published in the last 7 days." getHref={getHref} />
          </OverviewCard>
        </div>
      )}

      {(tiles.emails.visible || tiles.forms.visible) && (
        <div className="SimpleStats-tiles SimpleStats-tiles--halves">
          {tiles.emails.visible && (
            <OverviewCard
              headline="Emails"
              description="Regular emails with a send date in the selected range. Numbers are the emails' lifetime statistics. Rates are divided by delivered."
              isLoading={tiles.emails.isLoading}
              hasError={tiles.emails.hasError}
              available={tiles.emails.data?.available}
              notSetUpMessage="Email tables were not found in this project."
              updatedAt={tiles.emails.data?.updatedAt}
              reportHref={getHref(tiles.emails.data?.reportPath ?? null)}
              reportLabel="Open Email summary"
            >
              <div className="SimpleStats-kpis">
                <KpiCard tile={tiles.emails} kpiKey="emails" rangeText={rangeText} />
                <KpiCard tile={tiles.emails} kpiKey="open-rate" rangeText={rangeText} />
                <KpiCard tile={tiles.emails} kpiKey="click-rate" rangeText={rangeText} />
              </div>
              <h4 className="SimpleStats-overviewSubhead">Best and worst by click rate</h4>
              <OverviewRows
                rows={[...findList(tiles.emails, 'best').rows, ...findList(tiles.emails, 'worst').rows]}
                emptyMessage="No email sent in the selected range has enough delivered emails to rank."
                getHref={getHref}
              />
            </OverviewCard>
          )}

          {tiles.forms.visible && (
            <OverviewCard
              headline="Forms"
              description="Forms with the most submissions in the selected range."
              isLoading={tiles.forms.isLoading}
              hasError={tiles.forms.hasError}
              updatedAt={tiles.forms.data?.updatedAt}
              reportHref={getHref(tiles.forms.data?.reportPath ?? null)}
              reportLabel="Open Form submissions"
            >
              <ListRows tile={tiles.forms} listKey="top" emptyMessage="No form submissions in the selected range." getHref={getHref} />
            </OverviewCard>
          )}
        </div>
      )}

      {tiles.events.visible && (
        <OverviewCard
          headline="System"
          description={['Errors and warnings in the event log in the selected range.', tiles.events.data?.note].filter(Boolean).join(' ')}
          isLoading={tiles.events.isLoading}
          hasError={tiles.events.hasError}
          updatedAt={tiles.events.data?.updatedAt}
          reportHref={getHref(tiles.events.data?.reportPath ?? null)}
          reportLabel="Open Event log"
        >
          <div className="SimpleStats-kpis">
            <KpiCard tile={tiles.events} kpiKey="errors" rangeText={rangeText} />
            <KpiCard tile={tiles.events} kpiKey="warnings" rangeText={rangeText} />
          </div>
        </OverviewCard>
      )}

      <DataRetentionNote
        message="Activity data covers only contacts who consented to tracking, and contact and activity cleanup deletes old data. The event log keeps a limited number of events. All dates and times are server time. Each part shows when its data was read; select Refresh for the latest numbers."
      />
    </div>
  );
};

interface KpiCardProps {
  readonly tile: OverviewTileState;
  readonly kpiKey: string;
  /** Shown for KPIs without a comparison. */
  readonly rangeText: string;
}

/** One KPI of a tile as a card. Hidden when the tile is not visible or its source is not set up. */
const KpiCard = ({ tile, kpiKey, rangeText }: KpiCardProps) => {
  if (!tile.visible || (tile.data && !tile.data.available)) {
    return null;
  }

  const kpi: OverviewKpi | undefined = tile.data?.kpis.find((k) => k.key === kpiKey);
  if (tile.hasError || tile.isLoading || !kpi) {
    return (
      <InfoCard
        caption={kpi?.caption ?? captionFallback(kpiKey)}
        tooltip="Loads from its report."
        text="–"
        details={tile.hasError ? 'Could not load. Select Refresh.' : 'Loading…'}
      />
    );
  }

  const updated = tile.data ? formatUpdated(tile.data.updatedAt) : null;
  const tooltip = updated ? `${kpi.tooltip} Data from ${updated}.` : kpi.tooltip;

  return kpi.comparison ? (
    <ComparisonInfoCard caption={kpi.caption} tooltip={tooltip} comparison={kpi.comparison} noun={kpi.noun} />
  ) : (
    <InfoCard caption={kpi.caption} tooltip={tooltip} text={formatValue(kpi.value, kpi.kind)} details={rangeText} />
  );
};

const captions: Record<string, string> = {
  published: 'Published',
  submissions: 'Form submissions',
  'new-contacts': 'New contacts',
  'new-members': 'New members',
  'click-rate': 'Click rate',
  'open-rate': 'Open rate',
  emails: 'Emails sent',
  revenue: 'Revenue',
  errors: 'Errors',
  warnings: 'Warnings',
  activities: 'Tracked activities',
};

function captionFallback(key: string): string {
  return captions[key] ?? key;
}

const emptyList: OverviewList = { key: '', total: 0, rows: [] };

function findList(tile: OverviewTileState, key: string): OverviewList {
  return tile.data?.lists.find((l) => l.key === key) ?? emptyList;
}

interface ListRowsProps {
  readonly tile: OverviewTileState;
  readonly listKey: string;
  readonly emptyMessage: string;
  readonly getHref: (adminPath: string | null) => string | null;
}

const ListRows = ({ tile, listKey, emptyMessage, getHref }: ListRowsProps) => {
  const list = findList(tile, listKey);
  return <OverviewRows rows={list.rows} total={list.total} emptyMessage={emptyMessage} getHref={getHref} />;
};

/**
 * Template summary, for example "Last 7 days: 14 items published, 2 emails sent, form submissions up 18%."
 * Built only from loaded numbers; hidden, failed or not set up tiles are left out.
 */
function buildSummary(rangeText: string, days: number, tiles: Record<OverviewTileKey, OverviewTileState>): string | null {
  const parts: string[] = [];

  const kpi = (tile: OverviewTileState, key: string): OverviewKpi | undefined =>
    tile.visible && tile.data?.available && !tile.hasError ? tile.data.kpis.find((k) => k.key === key) : undefined;

  const published = kpi(tiles.calendar, 'published');
  if (published?.value !== null && published?.value !== undefined) {
    const count = countText(published.value, 'item', 'items');
    parts.push(days === 7 ? `${count} published` : `${count} published in the last 7 days`);
  }

  const emails = kpi(tiles.emails, 'emails');
  if (emails?.value !== null && emails?.value !== undefined) {
    parts.push(`${countText(emails.value, 'email', 'emails')} sent`);
  }

  pushChange(parts, 'form submissions', kpi(tiles.forms, 'submissions'));
  pushChange(parts, 'new contacts', kpi(tiles.contacts, 'new-contacts'));
  pushChange(parts, 'revenue', kpi(tiles.orders, 'revenue'));

  return parts.length === 0 ? null : `${rangeText}: ${parts.join(', ')}.`;
}

function pushChange(parts: string[], name: string, kpi: OverviewKpi | undefined) {
  const comparison: StatsValueComparison | null | undefined = kpi?.comparison;
  if (!kpi || !comparison || comparison.current === null) {
    return;
  }
  if (comparison.change === null) {
    parts.push(`${name}: ${formatValue(comparison.current, comparison.kind, comparison.currentText)}`);
  } else if (comparison.change === 0) {
    parts.push(`${name} unchanged`);
  } else {
    const direction = comparison.change > 0 ? 'up' : 'down';
    parts.push(`${name} ${direction} ${formatUnsignedChange(Math.abs(comparison.change), comparison.kind)}`);
  }
}

function countText(value: number, singular: string, plural: string): string {
  return `${numberFormat.format(value)} ${value === 1 ? singular : plural}`;
}

const unsignedPercent = new Intl.NumberFormat(undefined, { style: 'percent', maximumFractionDigits: 1 });
const unsignedPoints = new Intl.NumberFormat(undefined, { maximumFractionDigits: 1 });

/** Size of a change without its sign ("18%", or "2 pp" for rates); the summary says up or down. */
function formatUnsignedChange(change: number, kind: StatsValueComparison['kind']): string {
  return kind === 'Ratio' ? `${unsignedPoints.format(change * 100)} pp` : unsignedPercent.format(change);
}
