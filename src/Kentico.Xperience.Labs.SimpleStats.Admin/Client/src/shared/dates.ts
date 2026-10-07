/** Parses a `yyyy-MM-dd` string as a local date (no time zone shift). */
export function parseDateOnly(value: string): Date {
  const [year, month, day] = value.split('-').map(Number);
  return new Date(year, month - 1, day);
}

/** Formats a local date as `yyyy-MM-dd`. */
export function formatDateOnly(date: Date): string {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

/** Adds days to a `yyyy-MM-dd` string. */
export function addDays(value: string, days: number): string {
  const date = parseDateOnly(value);
  date.setDate(date.getDate() + days);
  return formatDateOnly(date);
}

/** Inclusive number of days between two `yyyy-MM-dd` strings. */
export function rangeLength(from: string, to: string): number {
  const ms = parseDateOnly(to).getTime() - parseDateOnly(from).getTime();
  return Math.round(ms / 86_400_000) + 1;
}

/**
 * Parses a server date and time without a time zone (`yyyy-MM-ddTHH:mm[:ss...]`, server local time, as sent for
 * `DateTime` values) as the same wall-clock time, so it is shown in server time like the server dates.
 * Returns `null` for other values.
 */
export function parseServerDateTime(value: string): Date | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/.exec(value);
  if (!match) {
    return null;
  }
  const [, year, month, day, hour, minute] = match.map(Number);
  return new Date(year, month - 1, day, hour, minute);
}

/** Server date and time as `yyyy-MM-dd HH:mm` (for CSV), or the value as sent when it cannot be parsed. */
export function formatServerDateTimeCsv(value: string): string {
  const date = parseServerDateTime(value);
  if (!date) {
    return value;
  }
  const hh = String(date.getHours()).padStart(2, '0');
  const mm = String(date.getMinutes()).padStart(2, '0');
  return `${formatDateOnly(date)} ${hh}:${mm}`;
}