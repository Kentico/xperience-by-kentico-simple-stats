import { Callout, CalloutPlacementType, CalloutType, InfoCard } from '@kentico/xperience-admin-components';
import React, { useCallback, useMemo, useState } from 'react';

import { toAdminHref } from '../shared/adminLinks';
import { contentKindOptions } from '../shared/contentKinds';
import { CoverageBarChart, CoverageCaptions } from '../shared/CoverageBarChart';
import { CoverageTable } from '../shared/CoverageTable';
import { toCoverageCsv, toRankedCsv } from '../shared/csv';
import { formatShare, numberFormat } from '../shared/format';
import { RankedBarChart } from '../shared/RankedBarChart';
import { RankedTable } from '../shared/RankedTable';
import { SnapshotFilterBar } from '../shared/SnapshotFilterBar';
import { StatsTile } from '../shared/StatsTile';
import {
  StatsCoverageItem,
  StatsRankedCaptions,
  StatsRankedItem,
  StatsRankedResult,
  StatsSnapshotFilter,
} from '../shared/types';
import { useCsvExport } from '../shared/useCsvExport';
import { useStatsCommand } from '../shared/useStatsCommand';
import { TagUsageUnusedTag, toUnusedTagCsv, UnusedTagTable } from './UnusedTagTable';
import '../shared/stats.css';

/** Mirrors `TagUsageTaxonomyOption`. */
interface TagUsageTaxonomyOption {
  readonly id: number;
  readonly displayName: string;
  readonly tagCount: number;
}

/** Mirrors `TagUsageResult`. */
interface TagUsageResult {
  /** Applied taxonomy filter, `null` for all. */
  readonly taxonomyId: number | null;
  /** Applied content type type filter (`Website`, `Reusable`, `Email`, `Headless`), `null` for all. */
  readonly kind: string | null;
  readonly taxonomyCount: number;
  readonly tagCount: number;
  /** Tags no content item has (all content, not only the kind filter). */
  readonly unusedTagCount: number;
  /** Variants counted once per taxonomy field they have. */
  readonly fieldVariants: number;
  readonly untaggedVariants: number;
  /** `untaggedVariants` of `fieldVariants`, `null` without taxonomy fields. */
  readonly untaggedShare: number | null;
  /** `value` = language variants with the tag; `secondaryLabel` = taxonomy; `adminPath` opens the tag. */
  readonly topTags: StatsRankedResult;
  readonly unusedTags: readonly TagUsageUnusedTag[];
  /** Per taxonomy field: `covered` = tagged variants, `total` = variants, `secondaryLabel` = content types. */
  readonly fields: readonly StatsCoverageItem[];
  readonly taxonomyOptions: readonly TagUsageTaxonomyOption[];
  /** ISO timestamp of when the data was read from the database. */
  readonly updatedAt: string;
}

/** Mirrors `TagUsageClientProperties`. */
interface TagUsageTemplateProps {
  readonly report: TagUsageResult;
  /** Path of this page relative to the admin root, used to build admin links. */
  readonly pagePath: string | null;
}

const fieldCaptions: CoverageCaptions = {
  covered: 'Tagged',
  missing: 'Untagged',
  totalNoun: 'variants',
};

const topTagCaptions: StatsRankedCaptions = {
  label: 'Tag',
  secondaryLabel: 'Taxonomy',
  value: 'Uses',
};

const usesHint =
  'A use is a language variant with the tag in any field, so an item with the tag in two languages counts twice. A parent tag counts only its own uses, not the uses of its child tags.';
const unusedHint =
  'Tags that no content item has, in any language or field. The content filter does not apply. A parent tag whose child tags are used is listed when it is not used itself.';
const untaggedHint =
  'Language variants of content types with a taxonomy field that have no tag in it. A variant counts once per taxonomy field its content type has.';

function toFilter(report: TagUsageResult): StatsSnapshotFilter {
  return { kind: report.kind, channelId: null, taxonomyId: report.taxonomyId };
}

export const TagUsageTemplate = (props: TagUsageTemplateProps) => {
  const saveCsv = useCsvExport();
  const { data: report, isLoading, hasError, load } = useStatsCommand<TagUsageResult, StatsSnapshotFilter>(props.report);
  const [filter, setFilter] = useState<StatsSnapshotFilter>(() => toFilter(props.report));

  const taxonomyOptions = useMemo(
    () =>
      report.taxonomyOptions.map((taxonomy) => ({
        id: taxonomy.id,
        label: taxonomy.displayName,
        secondaryLabel: `${numberFormat.format(taxonomy.tagCount)} ${taxonomy.tagCount === 1 ? 'tag' : 'tags'}`,
      })),
    [report.taxonomyOptions],
  );

  const handleFilterChange = (next: StatsSnapshotFilter) => {
    setFilter(next);
    void load(next);
  };

  const handleRefresh = () => {
    void load(filter, { refresh: true });
  };

  const { pagePath } = props;
  const getAdminHref = useCallback(
    (item: StatsRankedItem) => toAdminHref(item.adminPath, pagePath),
    [pagePath],
  );
  const getUnusedHref = useCallback(
    (item: TagUsageUnusedTag) => toAdminHref(item.adminPath, pagePath),
    [pagePath],
  );

  const taxonomy = report.taxonomyOptions.find((option) => option.id === report.taxonomyId);
  const fileSuffix = [(report.kind ?? 'all').toLowerCase(), ...(taxonomy ? [`taxonomy-${taxonomy.id}`] : [])].join('_');

  const exportFieldsCsv = () => {
    saveCsv(
      'tag-usage-fields',
      `tag-usage-fields_${fileSuffix}.csv`,
      toCoverageCsv(report.fields, {
        label: 'Field',
        secondaryLabel: 'Content types',
        covered: fieldCaptions.covered,
        missing: fieldCaptions.missing,
      }),
    );
  };

  const exportTopCsv = () => {
    saveCsv('tag-usage-top', `tag-usage-top_${fileSuffix}.csv`, toRankedCsv(report.topTags.items, topTagCaptions, getAdminHref));
  };

  const exportUnusedCsv = () => {
    saveCsv('tag-usage-unused', `tag-usage-unused_${fileSuffix}.csv`, toUnusedTagCsv(report.unusedTags, getUnusedHref));
  };

  const hasTaxonomies = report.taxonomyOptions.length > 0;

  const topListed =
    report.topTags.itemCount > report.topTags.items.length
      ? ` The ${numberFormat.format(report.topTags.items.length)} most used of ${numberFormat.format(report.topTags.itemCount)} used tags are listed.`
      : '';
  const unusedListed =
    report.unusedTagCount > report.unusedTags.length
      ? ` The first ${numberFormat.format(report.unusedTags.length)} of ${numberFormat.format(report.unusedTagCount)} are listed.`
      : '';

  return (
    <div className="SimpleStats-root">
      <SnapshotFilterBar
        filter={filter}
        onChange={handleFilterChange}
        kinds={contentKindOptions}
        select={
          report.taxonomyOptions.length > 1
            ? {
                label: 'Taxonomy',
                allLabel: 'All taxonomies',
                options: taxonomyOptions,
                value: filter.taxonomyId ?? null,
                onChange: (taxonomyId) => handleFilterChange({ ...filter, taxonomyId }),
              }
            : undefined
        }
        onRefresh={handleRefresh}
        isLoading={isLoading}
        updatedAt={report.updatedAt}
      />

      {!hasTaxonomies ? (
        <Callout type={CalloutType.QuickTip} placement={CalloutPlacementType.OnDesk} headline="No taxonomies">
          <p>
            Create a taxonomy with tags in the Taxonomies application and add a taxonomy field to your content types to see
            how tags are used.
          </p>
        </Callout>
      ) : (
        <>
          <div className="SimpleStats-kpis">
            <InfoCard
              caption="Taxonomies"
              tooltip="Taxonomies in the taxonomy filter."
              text={numberFormat.format(report.taxonomyCount)}
              details={taxonomy ? taxonomy.displayName : 'All taxonomies'}
            />
            <InfoCard
              caption="Tags"
              tooltip="Tags of the selected taxonomies, parent and child tags. Used tags are on at least one content item (all content)."
              text={numberFormat.format(report.tagCount)}
              details={`${numberFormat.format(report.tagCount - report.unusedTagCount)} used`}
            />
            <InfoCard
              caption="Unused tags"
              tooltip={unusedHint}
              text={numberFormat.format(report.unusedTagCount)}
              details={report.unusedTagCount > 0 ? 'Not on any content item' : 'Every tag is used'}
            />
            <InfoCard
              caption="Untagged share"
              tooltip={untaggedHint}
              text={report.untaggedShare === null ? '–' : formatShare(report.untaggedShare)}
              details={
                report.untaggedShare === null
                  ? 'No taxonomy fields in the selected content'
                  : `${numberFormat.format(report.untaggedVariants)} of ${numberFormat.format(report.fieldVariants)} variants`
              }
            />
          </div>

          <StatsTile
            headline="Untagged content by field"
            description={`${untaggedHint} A reusable field schema field is one row for all content types with the schema. Smart folders and listings that filter by tags miss untagged content.`}
            isLoading={isLoading}
            hasError={hasError}
            isEmpty={report.fields.length === 0}
            emptyMessage="No content type in the selected content has a taxonomy field of the selected taxonomy."
            onExportCsv={exportFieldsCsv}
            renderChart={() => (
              <CoverageBarChart
                items={report.fields}
                captions={fieldCaptions}
                ariaLabel="Tagged and untagged language variants per taxonomy field"
                labelWidth={280}
              />
            )}
            renderTable={() => (
              <CoverageTable
                items={report.fields}
                labelCaption="Field"
                captions={fieldCaptions}
                secondaryLabelCaption="Content types"
                totalCaption="Variants"
              />
            )}
          />

          <div className="SimpleStats-tiles SimpleStats-tiles--halves">
            <StatsTile
              headline="Top tags"
              description={`Tags by uses in the selected content. ${usesHint} Click a tag to open it in the Taxonomies application.${topListed}`}
              isLoading={isLoading}
              hasError={hasError}
              isEmpty={report.topTags.items.length === 0}
              emptyMessage="No content item in the selected content has a tag."
              onExportCsv={exportTopCsv}
              renderChart={() => (
                <RankedBarChart
                  items={report.topTags.items}
                  captions={topTagCaptions}
                  ariaLabel="Most used tags"
                  getHref={getAdminHref}
                  showShare={false}
                />
              )}
              renderTable={() => (
                <RankedTable
                  items={report.topTags.items}
                  captions={topTagCaptions}
                  getAdminHref={getAdminHref}
                  showSecondaryLabel
                  showShare={false}
                />
              )}
            />

            <StatsTile
              headline="Unused tags"
              description={`${unusedHint} Click a tag to open it in the Taxonomies application.${unusedListed}`}
              isLoading={isLoading}
              hasError={hasError}
              isEmpty={report.unusedTags.length === 0}
              emptyMessage="Every tag is used."
              onExportCsv={exportUnusedCsv}
              renderTable={() => <UnusedTagTable items={report.unusedTags} getAdminHref={getUnusedHref} />}
            />
          </div>
        </>
      )}
    </div>
  );
};
