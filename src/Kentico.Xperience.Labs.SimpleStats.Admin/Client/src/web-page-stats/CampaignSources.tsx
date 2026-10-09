import { InfoCard } from '@kentico/xperience-admin-components';
import React from 'react';

import { noUtmDataMessage, sentences, shownText } from '../shared/campaigns';
import { CsvData, toRankedCsv } from '../shared/csv';
import { formatShare, numberFormat } from '../shared/format';
import { RankedBarChart } from '../shared/RankedBarChart';
import { RankedTable } from '../shared/RankedTable';
import { StatsTile } from '../shared/StatsTile';
import { StatsRankedCaptions, StatsRankedResult } from '../shared/types';

/** Mirrors `WebPageCampaignsResult`. */
export interface WebPageCampaignsResult {
  readonly landings: number;
  readonly campaignLandings: number;
  /** Campaign landings / landings (0–1), or `null` without landings. */
  readonly campaignShare: number | null;
  readonly campaignVisitors: number;
  readonly bySource: StatsRankedResult;
  readonly bySourceContent: StatsRankedResult;
  /** Any activity on the site has a UTM source (`false`: UTM values are not captured). */
  readonly hasAnyUtmData: boolean;
}

interface CampaignSourcesProps {
  readonly campaigns: WebPageCampaignsResult;
  readonly from: string;
  readonly to: string;
  readonly isLoading: boolean;
  readonly hasError: boolean;
  readonly saveCsv: (exportName: string, fileName: string, csv: CsvData) => void;
}

const sourceCaptions: StatsRankedCaptions = {
  label: 'Source',
  value: 'Landings',
  secondaryValue: 'Visitors',
};

const contentCaptions: StatsRankedCaptions = {
  label: 'Source',
  secondaryLabel: 'Content',
  value: 'Landings',
};

const noCampaignLandingsMessage = 'No campaign landings on this page in the selected range.';

/**
 * Campaign sources of the page: landing page activities and the UTM source and content stored on them.
 * Xperience does not store UTM values by itself; the usage guide's UTM capture sample does.
 */
export const CampaignSources = ({ campaigns, from, to, isLoading, hasError, saveCsv }: CampaignSourcesProps) => {
  const rangeText = `${from} – ${to}`;
  const isEmpty = campaigns.campaignLandings === 0;
  const emptyMessage = campaigns.hasAnyUtmData ? noCampaignLandingsMessage : noUtmDataMessage;

  const exportSources = () => {
    saveCsv(
      'web-page-stats-campaign-sources',
      `web-page-stats-campaign-sources_${from}_${to}.csv`,
      toRankedCsv(campaigns.bySource.items, sourceCaptions),
    );
  };

  const exportContents = () => {
    saveCsv(
      'web-page-stats-campaign-content',
      `web-page-stats-campaign-content_${from}_${to}.csv`,
      toRankedCsv(campaigns.bySourceContent.items, contentCaptions),
    );
  };

  return (
    <>
      <div className="SimpleStats-kpis">
        <InfoCard
          caption="Landings"
          tooltip="Landing page activities logged for this page in the edited language. A landing is logged for the first page of a browsing session, so this is about the number of sessions that started on this page."
          text={numberFormat.format(campaigns.landings)}
          details="Sessions that started here"
        />
        <InfoCard
          caption="Campaign landings"
          tooltip="Landings with a UTM source (utm_source) stored on the activity."
          text={campaigns.hasAnyUtmData ? numberFormat.format(campaigns.campaignLandings) : '–'}
          details={
            !campaigns.hasAnyUtmData
              ? 'No UTM values on this site'
              : campaigns.campaignShare === null
                ? 'No landings'
                : `${formatShare(campaigns.campaignShare)} of landings, ${numberFormat.format(campaigns.campaignVisitors)} contacts`
          }
        />
      </div>

      <div className="SimpleStats-tiles SimpleStats-tiles--halves">
        <StatsTile
          headline="Campaign sources"
          description={sentences(
            'Campaign landings per UTM source.',
            shownText(campaigns.bySource, 'sources'),
            'Share is of campaign landings; visitors are distinct contacts.',
          )}
          isLoading={isLoading}
          hasError={hasError}
          isEmpty={isEmpty}
          emptyMessage={emptyMessage}
          onExportCsv={exportSources}
          renderChart={() => (
            <RankedBarChart
              items={campaigns.bySource.items}
              captions={sourceCaptions}
              ariaLabel={`Campaign landings by source, ${rangeText}`}
            />
          )}
          renderTable={() => <RankedTable items={campaigns.bySource.items} captions={sourceCaptions} />}
        />
        <StatsTile
          headline="Source and content"
          description={sentences(
            'Campaign landings per UTM source and content (utm_content).',
            shownText(campaigns.bySourceContent, 'pairs'),
            '(none) means no content was given.',
          )}
          isLoading={isLoading}
          hasError={hasError}
          isEmpty={isEmpty}
          emptyMessage={emptyMessage}
          onExportCsv={exportContents}
          renderTable={() => (
            <RankedTable items={campaigns.bySourceContent.items} captions={contentCaptions} showSecondaryLabel />
          )}
        />
      </div>
    </>
  );
};
