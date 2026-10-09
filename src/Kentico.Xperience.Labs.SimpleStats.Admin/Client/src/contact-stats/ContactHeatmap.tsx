import { Colors } from '@kentico/xperience-admin-components';
import React from 'react';

import { CsvData, toCsv } from '../shared/csv';
import { numberFormat } from '../shared/format';

/** Mirrors `ContactHeatmapCell`. */
export interface ContactHeatmapCell {
  /** 0 = Monday … 6 = Sunday. */
  readonly weekday: number;
  readonly hour: number;
  readonly count: number;
}

/** Mirrors `ContactHeatmapResult`. */
export interface ContactHeatmapResult {
  readonly contactId: number;
  readonly from: string;
  readonly to: string;
  readonly activityTypes: readonly string[];
  /** All 7 × 24 cells, Monday first. */
  readonly cells: readonly ContactHeatmapCell[];
  readonly max: number;
  readonly total: number;
  readonly updatedAt: string;
}

export const weekdayNames = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'] as const;
const hours = Array.from({ length: 24 }, (_, hour) => hour);

/** Hour labels: 24-hour (`00`–`23`) or 12-hour (`12a`–`11p`, compact to fit 24 columns; tooltips spell out AM/PM). */
export type HourFormat = '24' | '12';

function hourLabel(hour: number, format: HourFormat): string {
  if (format === '12') {
    return `${hour % 12 === 0 ? 12 : hour % 12}${hour < 12 ? 'a' : 'p'}`;
  }
  return String(hour).padStart(2, '0');
}

function hourRangeText(hour: number, format: HourFormat): string {
  if (format === '12') {
    return `${hour % 12 === 0 ? 12 : hour % 12}:00–${hour % 12 === 0 ? 12 : hour % 12}:59 ${hour < 12 ? 'AM' : 'PM'}`;
  }
  return `${hourLabel(hour, format)}:00–${hourLabel(hour, format)}:59`;
}

/** Weekday × hour table with cells shaded by count (product color), plus the exact number in each cell's tooltip. */
export interface ContactHeatmapProps {
  readonly heatmap: ContactHeatmapResult;
  /** Hour label format. Default 24-hour. */
  readonly hourFormat?: HourFormat;
}

export const ContactHeatmap = ({ heatmap, hourFormat = '24' }: ContactHeatmapProps) => {
  const lookup = new Map(heatmap.cells.map((cell) => [`${cell.weekday}-${cell.hour}`, cell.count]));

  return (
    <div className="SimpleStats-tableScroll">
      <table className="SimpleStats-heatmap" aria-label={`Activities by weekday and hour, ${heatmap.from} – ${heatmap.to}`}>
        <thead>
          <tr>
            <th scope="col" />
            {hours.map((hour) => (
              <th key={hour} scope="col">
                {hourLabel(hour, hourFormat)}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {weekdayNames.map((name, weekday) => (
            <tr key={name}>
              <th scope="row">{name.slice(0, 3)}</th>
              {hours.map((hour) => {
                const count = lookup.get(`${weekday}-${hour}`) ?? 0;
                // At least 15% for any activity, so a single activity stays visible next to a busy hour.
                const strength = count === 0 || heatmap.max === 0 ? 0 : Math.round(15 + (85 * count) / heatmap.max);
                return (
                  <td
                    key={hour}
                    title={`${name} ${hourRangeText(hour, hourFormat)}: ${numberFormat.format(count)} activities`}
                    style={
                      strength > 0
                        ? { backgroundColor: `color-mix(in srgb, ${Colors.Product} ${strength}%, transparent)` }
                        : undefined
                    }
                  >
                    <span className="SimpleStats-visuallyHidden">{count}</span>
                  </td>
                );
              })}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
};

/** CSV: one row per weekday and hour. */
/** CSV hours are always 0–23 (sortable), whatever the shown format. */
export function toHeatmapCsv(heatmap: ContactHeatmapResult): CsvData {
  return toCsv(
    ['Weekday', 'Hour', 'Activities'],
    heatmap.cells.map((cell) => [weekdayNames[cell.weekday] ?? cell.weekday, cell.hour, cell.count]),
  );
}
