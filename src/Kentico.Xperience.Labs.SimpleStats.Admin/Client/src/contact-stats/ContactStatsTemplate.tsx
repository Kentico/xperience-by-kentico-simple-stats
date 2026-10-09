import {
  Button,
  ButtonColor,
  Callout,
  CalloutPlacementType,
  CalloutType,
  InfoCard,
  LinkButton,
  NameToggleButtons,
} from '@kentico/xperience-admin-components';
import React, { useCallback, useMemo, useState } from 'react';

import { toAdminHref } from '../shared/adminLinks';
import { sentences, shownText } from '../shared/campaigns';
import { ComparisonInfoCard } from '../shared/ComparisonInfoCard';
import { toRankedCsv, toTimeSeriesCsv } from '../shared/csv';
import { DataRetentionNote } from '../shared/DataRetentionNote';
import { parseServerDateTime } from '../shared/dates';
import { IdSelect, MultiSelect } from '../shared/filterControls';
import { numberFormat } from '../shared/format';
import { RankedBarChart } from '../shared/RankedBarChart';
import { RankedTable } from '../shared/RankedTable';
import { StackedColumnChart } from '../shared/StackedColumnChart';
import { StatsFilterBar } from '../shared/StatsFilterBar';
import { StatsTile } from '../shared/StatsTile';
import { toStatsSeries } from '../shared/timeSeries';
import { TimeSeriesTable } from '../shared/TimeSeriesTable';
import {
  StatsComparison,
  StatsFilter,
  StatsGrouping,
  StatsRankedCaptions,
  StatsRankedItem,
  StatsRankedResult,
  StatsTimeSeriesResult,
} from '../shared/types';
import { useCsvExport } from '../shared/useCsvExport';
import { useStatsCommand } from '../shared/useStatsCommand';
import { ContactHeatmap, ContactHeatmapResult, HourFormat, toHeatmapCsv } from './ContactHeatmap';
import '../shared/stats.css';

/** Mirrors `ContactStatsFilter`. */
interface ContactStatsFilter {
  readonly range: StatsFilter;
  readonly allTime: boolean;
  /** Empty means all types. */
  readonly activityTypes: readonly string[];
  /** Taxonomy of the tag interests, `null` for all. */
  readonly taxonomyId: number | null;
}

/** Mirrors `TagUsageTaxonomyOption`. */
interface TaxonomyOption {
  readonly id: number;
  readonly displayName: string;
  /** Distinct tags reached. */
  readonly tagCount: number;
}

/** Mirrors `ContactStatsTypeOption`. */
interface ContactStatsTypeOption {
  readonly activityType: string;
  readonly displayName: string;
  readonly count: number;
}

/** Mirrors `ContactStatsTotals`. */
interface ContactStatsTotals {
  readonly activities: number;
  readonly sessions: number;
  readonly pageVisits: number;
  readonly formSubmissions: number;
  readonly emailClicks: number;
  readonly activeDays: number;
  readonly rangeDays: number;
  readonly firstSeen: string | null;
  readonly lastSeen: string | null;
  readonly daysSinceLastSeen: number | null;
}

/** Mirrors `ContactStatsInsight`. */
interface ContactStatsInsight {
  readonly kind: string;
  readonly text: string;
}

/** Mirrors `ContactStatsResult`. */
interface ContactStatsResult {
  readonly contactId: number;
  readonly from: string;
  readonly to: string;
  readonly grouping: StatsGrouping;
  readonly allTime: boolean;
  readonly activityTypes: readonly string[];
  readonly typeOptions: readonly ContactStatsTypeOption[];
  readonly totals: ContactStatsTotals;
  /** `null` for All time. */
  readonly comparison: StatsComparison | null;
  readonly series: StatsTimeSeriesResult;
  readonly topPages: StatsRankedResult;
  readonly forms: StatsRankedResult;
  readonly emails: StatsRankedResult;
  readonly campaigns: StatsRankedResult;
  readonly interests: StatsRankedResult;
  /** Page visits per tag of the visited page and the items it links to. A visit counts once per tag. */
  readonly interestTags: StatsRankedResult;
  readonly taxonomyId: number | null;
  readonly taxonomyOptions: readonly TaxonomyOption[];
  readonly hasAnyUtmData: boolean;
  readonly insights: readonly ContactStatsInsight[];
  /** Admin path of the contact's Activities tab. */
  readonly activitiesPath: string | null;
  /** Admin path of this tab, for admin links. */
  readonly pagePath: string | null;
  readonly updatedAt: string;
}

/** Mirrors `ContactStatsClientProperties`. */
interface ContactStatsTemplateProps {
  readonly report: ContactStatsResult;
  readonly today: string;
}

const pageVisitType = 'pagevisit';
const landingPageType = 'landingpage';
const formSubmitType = 'bizformsubmit';
const emailClickType = 'emailclick';

const pageCaptions: StatsRankedCaptions = { label: 'Page', secondaryLabel: 'Channel · language', value: 'Visits' };
const formCaptions: StatsRankedCaptions = { label: 'Form', secondaryLabel: 'Last submitted', value: 'Submissions' };
const emailCaptions: StatsRankedCaptions = { label: 'Email', secondaryLabel: 'Last clicked', value: 'Clicks' };
const campaignCaptions: StatsRankedCaptions = { label: 'Source', secondaryLabel: 'Content', value: 'Sessions' };
const interestCaptions: StatsRankedCaptions = { label: 'Content type', value: 'Visits', secondaryValue: 'Pages' };
const tagCaptions: StatsRankedCaptions = { label: 'Tag', secondaryLabel: 'Taxonomy', value: 'Visits', secondaryValue: 'Pages' };

type InterestView = 'tags' | 'types';

const interestViewItems = [
  { id: 'tags', label: 'Tags' },
  { id: 'types', label: 'Content types' },
];

const dateFormat = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium' });

const hourFormatItems = [
  { id: '24', label: '24h' },
  { id: '12', label: '12h' },
];

function toFilter(report: ContactStatsResult): ContactStatsFilter {
  return {
    // One contact, so no channel filter.
    range: { from: report.from, to: report.to, grouping: report.grouping, channelId: null },
    allTime: report.allTime,
    activityTypes: report.activityTypes,
    taxonomyId: report.taxonomyId,
  };
}

function lastSeenDetails(totals: ContactStatsTotals): string {
  if (totals.daysSinceLastSeen === null) {
    return 'No activity';
  }
  if (totals.daysSinceLastSeen === 0) {
    return 'Today';
  }
  return totals.daysSinceLastSeen === 1 ? 'Yesterday' : `${numberFormat.format(totals.daysSinceLastSeen)} days ago`;
}

function formatServerDate(value: string | null): string {
  const date = value ? parseServerDateTime(value) : null;
  return date ? dateFormat.format(date) : '–';
}

export const ContactStatsTemplate = (props: ContactStatsTemplateProps) => {
  const saveCsv = useCsvExport();
  const { data: report, isLoading, hasError, load } = useStatsCommand<ContactStatsResult, ContactStatsFilter>(props.report);
  const heatmapCommand = useStatsCommand<ContactHeatmapResult | null, ContactStatsFilter>(null, 'LOAD_HEATMAP');
  const [filter, setFilter] = useState<ContactStatsFilter>(() => toFilter(props.report));
  // The heatmap is loaded only after the user asks for it; then it follows the filter.
  const [heatmapRequested, setHeatmapRequested] = useState(false);
  const [hourFormat, setHourFormat] = useState<HourFormat>('24');
  const [interestView, setInterestView] = useState<InterestView>('tags');

  const apply = (next: ContactStatsFilter, refresh = false) => {
    setFilter(next);
    void load(next, { refresh });
    // The heatmap ignores the taxonomy, so a taxonomy-only change keeps it.
    const taxonomyOnly =
      next.range === filter.range && next.allTime === filter.allTime && next.activityTypes === filter.activityTypes;
    if (heatmapRequested && (refresh || !taxonomyOnly)) {
      void heatmapCommand.load(next, { refresh });
    }
  };

  const handleRangeChange = (range: StatsFilter) => {
    // A grouping change keeps "All time"; a preset or custom range leaves it.
    const groupingOnly = range.grouping !== filter.range.grouping;
    apply({ ...filter, range, allTime: groupingOnly ? filter.allTime : false });
  };

  const handleGenerateHeatmap = () => {
    setHeatmapRequested(true);
    void heatmapCommand.load(filter);
  };

  const pagePath = report.pagePath;
  const getAdminHref = useCallback((item: StatsRankedItem) => toAdminHref(item.adminPath, pagePath), [pagePath]);
  const activitiesHref = toAdminHref(report.activitiesPath, pagePath);

  const chartSeries = useMemo(() => toStatsSeries(report.series.series), [report.series.series]);

  const includes = (type: string) => report.activityTypes.length === 0 || report.activityTypes.includes(type);
  const notSelected = (name: string) => `${name} are not in the selected activity types.`;

  const rangeText = report.allTime ? `All time (${report.from} – ${report.to})` : `${report.from} – ${report.to}`;
  const fileSuffix = `contact-${report.contactId}_${report.from}_${report.to}`;
  const totals = report.totals;
  const heatmap = heatmapCommand.data;

  // Taxonomy select only in the tag view, and only when there is a choice (as in Tag usage).
  const interestControls = () => (
    <>
      {interestView === 'tags' && (report.taxonomyOptions.length > 1 || filter.taxonomyId !== null) && (
        <IdSelect
          label="Taxonomy"
          allLabel="All taxonomies"
          options={report.taxonomyOptions.map((option) => ({
            id: option.id,
            label: option.displayName,
            secondaryLabel: `${numberFormat.format(option.tagCount)} tags`,
          }))}
          value={filter.taxonomyId}
          onChange={(taxonomyId) => apply({ ...filter, taxonomyId })}
        />
      )}
      <NameToggleButtons
        items={interestViewItems}
        selectedItemId={interestView}
        onChange={(id) => setInterestView(id as InterestView)}
      />
    </>
  );

  return (
    <div className="SimpleStats-root SimpleStats-root--framed">
      <StatsFilterBar
        filter={filter.range}
        today={props.today}
        onChange={handleRangeChange}
        onRefresh={() => apply(filter, true)}
        isLoading={isLoading}
        updatedAt={report.updatedAt}
        showChannel={false}
        allTime={filter.allTime}
        onAllTime={() => apply({ ...filter, allTime: true })}
        actions={
          activitiesHref && (
            <LinkButton
              label="All activities"
              color={ButtonColor.Tertiary}
              href={activitiesHref}
              title="Open the full list in the contact's Activities tab"
            />
          )
        }
      >
        <MultiSelect
          label="Activity types"
          allLabel="All types"
          noun="types"
          options={report.typeOptions.map((option) => ({
            value: option.activityType,
            label: option.displayName,
            secondaryLabel: `${numberFormat.format(option.count)} activities`,
          }))}
          value={filter.activityTypes}
          onChange={(activityTypes) => apply({ ...filter, activityTypes })}
        />
      </StatsFilterBar>

      {report.insights.length > 0 && (
        <Callout type={CalloutType.QuickTip} placement={CalloutPlacementType.OnDesk} headline="Insights">
          <ul className="SimpleStats-insights">
            {report.insights.map((insight) => (
              <li key={insight.kind}>{insight.text}</li>
            ))}
          </ul>
        </Callout>
      )}

      <div className="SimpleStats-kpis">
        {report.comparison ? (
          <ComparisonInfoCard
            caption="Activities"
            tooltip="Activities of this contact in the selected range and activity types."
            comparison={report.comparison}
            noun="activities"
          />
        ) : (
          <InfoCard
            caption="Activities"
            tooltip="Activities of this contact since it was created (or since its first activity, whichever is earlier), in the selected activity types."
            text={numberFormat.format(totals.activities)}
            details="All time"
          />
        )}
        <InfoCard
          caption="Sessions"
          tooltip="Landing page activities. A landing is logged for the first page of a browsing session, so this is about the number of visits to the site."
          text={includes(landingPageType) ? numberFormat.format(totals.sessions) : '–'}
          details={includes(landingPageType) ? rangeText : 'Landing pages not selected'}
        />
        <InfoCard
          caption="Active days"
          tooltip="Distinct days with any activity in the selected range."
          text={numberFormat.format(totals.activeDays)}
          details={`Of ${numberFormat.format(totals.rangeDays)} days`}
        />
        <InfoCard
          caption="Last seen"
          tooltip={`Newest activity of the selected types, any date (server time). First seen: ${formatServerDate(totals.firstSeen)}.`}
          text={formatServerDate(totals.lastSeen)}
          details={lastSeenDetails(totals)}
        />
      </div>

      <StatsTile
        headline="Activity over time"
        description={`Activities per ${report.grouping.toLowerCase()}, stacked by activity type.`}
        isLoading={isLoading}
        hasError={hasError}
        isEmpty={report.series.total === 0}
        emptyMessage="No activities in the selected range and activity types."
        onExportCsv={() =>
          saveCsv(
            'contact-stats-series',
            `contact-stats-series_${fileSuffix}_${report.grouping.toLowerCase()}.csv`,
            toTimeSeriesCsv(report.series.periods, chartSeries),
          )
        }
        renderChart={() => (
          <StackedColumnChart periods={report.series.periods} series={chartSeries} ariaLabel={`Activities by type, ${rangeText}`} />
        )}
        renderTable={() => <TimeSeriesTable grouping={report.grouping} periods={report.series.periods} series={chartSeries} />}
      />

      <StatsTile
        headline="When active"
        description="Activities by weekday and hour in the selected range and activity types. Hours are in the server time zone."
        isLoading={heatmapCommand.isLoading}
        hasError={heatmapCommand.hasError}
        isEmpty={heatmap !== null && heatmap.total === 0}
        emptyMessage="No activities in the selected range and activity types."
        onExportCsv={
          heatmap
            ? () => saveCsv('contact-stats-heatmap', `contact-stats-heatmap_${fileSuffix}.csv`, toHeatmapCsv(heatmap))
            : undefined
        }
        headerControls={
          heatmap && (
            <NameToggleButtons
              items={hourFormatItems}
              selectedItemId={hourFormat}
              onChange={(id) => setHourFormat(id as HourFormat)}
            />
          )
        }
        renderTable={() =>
          heatmap ? (
            <ContactHeatmap heatmap={heatmap} hourFormat={hourFormat} />
          ) : (
            <div className="SimpleStats-heatmapStart">
              <span>Shows on which weekdays and at which hours this contact is active. Loaded on request.</span>
              {!heatmapRequested && (
                <Button label="Generate heatmap" color={ButtonColor.Secondary} onClick={handleGenerateHeatmap} />
              )}
            </div>
          )
        }
      />

      <StatsTile
        headline="Pages visited"
        description={sentences('Page visits per page and language.', shownText(report.topPages, 'pages'), 'Links open the live page.')}
        isLoading={isLoading}
        hasError={hasError}
        isEmpty={report.topPages.items.length === 0}
        emptyMessage={includes(pageVisitType) ? 'No page visits in the selected range.' : notSelected('Page visits')}
        onExportCsv={() =>
          saveCsv('contact-stats-pages', `contact-stats-pages_${fileSuffix}.csv`, toRankedCsv(report.topPages.items, pageCaptions))
        }
        renderChart={() => (
          <RankedBarChart items={report.topPages.items} captions={pageCaptions} ariaLabel={`Pages visited, ${rangeText}`} />
        )}
        renderTable={() => <RankedTable items={report.topPages.items} captions={pageCaptions} showSecondaryLabel />}
      />

      <div className="SimpleStats-tiles SimpleStats-tiles--halves">
        <StatsTile
          headline="Forms submitted"
          description={sentences('Form submissions per form.', shownText(report.forms, 'forms'))}
          isLoading={isLoading}
          hasError={hasError}
          isEmpty={report.forms.items.length === 0}
          emptyMessage={includes(formSubmitType) ? 'No form submissions in the selected range.' : notSelected('Form submissions')}
          onExportCsv={() =>
            saveCsv(
              'contact-stats-forms',
              `contact-stats-forms_${fileSuffix}.csv`,
              toRankedCsv(report.forms.items, formCaptions, getAdminHref),
            )
          }
          renderTable={() => (
            <RankedTable items={report.forms.items} captions={formCaptions} getAdminHref={getAdminHref} showSecondaryLabel />
          )}
        />
        <StatsTile
          headline="Emails clicked"
          description={sentences('Email link clicks per email.', shownText(report.emails, 'emails'))}
          isLoading={isLoading}
          hasError={hasError}
          isEmpty={report.emails.items.length === 0}
          emptyMessage={includes(emailClickType) ? 'No email clicks in the selected range.' : notSelected('Email clicks')}
          onExportCsv={() =>
            saveCsv(
              'contact-stats-emails',
              `contact-stats-emails_${fileSuffix}.csv`,
              toRankedCsv(report.emails.items, emailCaptions, getAdminHref),
            )
          }
          renderTable={() => (
            <RankedTable items={report.emails.items} captions={emailCaptions} getAdminHref={getAdminHref} showSecondaryLabel />
          )}
        />
      </div>

      {report.hasAnyUtmData && (
        <StatsTile
          headline="Campaign sources"
          description={sentences(
            'Sessions (landings) per UTM source and content.',
            shownText(report.campaigns, 'pairs'),
            '(none) means no content was given.',
          )}
          isLoading={isLoading}
          hasError={hasError}
          isEmpty={report.campaigns.items.length === 0}
          emptyMessage={
            includes(landingPageType) ? 'No campaign sessions in the selected range.' : notSelected('Landing pages')
          }
          onExportCsv={() =>
            saveCsv(
              'contact-stats-campaigns',
              `contact-stats-campaigns_${fileSuffix}.csv`,
              toRankedCsv(report.campaigns.items, campaignCaptions),
            )
          }
          renderChart={() => (
            <RankedBarChart items={report.campaigns.items} captions={campaignCaptions} ariaLabel={`Campaign sources, ${rangeText}`} />
          )}
          renderTable={() => <RankedTable items={report.campaigns.items} captions={campaignCaptions} showSecondaryLabel />}
        />
      )}

      {interestView === 'tags' ? (
        <StatsTile
          key="tags"
          headline="Interests"
          description={sentences(
            'Page visits per tag. Tags come from the visited page and the items it links to (including images), so filter by taxonomy to focus.',
            'A visit counts once per tag, so the numbers do not add up.',
            shownText(report.interestTags, 'tags'),
          )}
          headerControls={interestControls()}
          isLoading={isLoading}
          hasError={hasError}
          isEmpty={report.interestTags.items.length === 0}
          emptyMessage={
            !includes(pageVisitType)
              ? notSelected('Page visits')
              : report.taxonomyId !== null
                ? 'The page visits in the selected range reached no tags of this taxonomy.'
                : 'The page visits in the selected range reached no tags.'
          }
          onExportCsv={() =>
            saveCsv(
              'contact-stats-tags',
              `contact-stats-tags_${fileSuffix}${report.taxonomyId === null ? '' : `_taxonomy-${report.taxonomyId}`}.csv`,
              toRankedCsv(report.interestTags.items, tagCaptions),
            )
          }
          renderChart={() => (
            <RankedBarChart
              items={report.interestTags.items}
              captions={tagCaptions}
              ariaLabel={`Interests by tag, ${rangeText}`}
              showShare={false}
            />
          )}
          renderTable={() => (
            <RankedTable items={report.interestTags.items} captions={tagCaptions} showSecondaryLabel showShare={false} />
          )}
        />
      ) : (
        <StatsTile
          key="types"
          headline="Interests"
          headerControls={interestControls()}
          description={sentences(
            'Page visits per content type of the visited page.',
            shownText(report.interests, 'content types'),
            'Pages that no longer exist are left out.',
          )}
          isLoading={isLoading}
          hasError={hasError}
          isEmpty={report.interests.items.length === 0}
          emptyMessage={includes(pageVisitType) ? 'No page visits in the selected range.' : notSelected('Page visits')}
          onExportCsv={() =>
            saveCsv(
              'contact-stats-interests',
              `contact-stats-interests_${fileSuffix}.csv`,
              toRankedCsv(report.interests.items, interestCaptions),
            )
          }
          renderChart={() => (
            <RankedBarChart items={report.interests.items} captions={interestCaptions} ariaLabel={`Interests, ${rangeText}`} />
          )}
          renderTable={() => <RankedTable items={report.interests.items} captions={interestCaptions} />}
        />
      )}

      <DataRetentionNote />
    </div>
  );
};
