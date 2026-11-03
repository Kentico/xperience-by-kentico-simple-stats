import { parseServerDateTime } from '../shared/dates';

const timeFormat = new Intl.DateTimeFormat(undefined, { timeStyle: 'short' });
const dateTimeFormat = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' });

/** When data was read (ISO timestamp with time zone): time today, else date and time. `null` for invalid values. */
export function formatUpdated(value: string): string | null {
  const date = new Date(value);
  if (Number.isNaN(date.getTime()) || date.getFullYear() < 2000) {
    return null;
  }
  return (date.toDateString() === new Date().toDateString() ? timeFormat : dateTimeFormat).format(date);
}

/** Server local date and time of a row (`yyyy-MM-ddTHH:mm:ss`), shown as stored (server time). */
export function formatRowDate(value: string): string {
  const date = parseServerDateTime(value);
  return date ? dateTimeFormat.format(date) : value;
}

/** Oldest of the ISO timestamps, or `undefined` when there are none. */
export function oldest(values: readonly string[]): string | undefined {
  let result: string | undefined;
  let resultTime = Number.POSITIVE_INFINITY;
  for (const value of values) {
    const time = new Date(value).getTime();
    if (!Number.isNaN(time) && new Date(value).getFullYear() >= 2000 && time < resultTime) {
      result = value;
      resultTime = time;
    }
  }
  return result;
}
