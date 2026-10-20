/**
 * The UI is English-only, so dates and numbers are always formatted in English (24-hour clock) regardless of the
 * browser's language. Times are still shown in the viewer's own time zone.
 */
export const UI_LOCALE = 'en-GB';

const dateTimeFormat = new Intl.DateTimeFormat(UI_LOCALE, { dateStyle: 'medium', timeStyle: 'short' });
const dateTimeWithSecondsFormat = new Intl.DateTimeFormat(UI_LOCALE, { dateStyle: 'medium', timeStyle: 'medium' });
const shortTimeFormat = new Intl.DateTimeFormat(UI_LOCALE, { hour: '2-digit', minute: '2-digit' });
const dayAndTimeFormat = new Intl.DateTimeFormat(UI_LOCALE, {
  day: 'numeric',
  month: 'short',
  hour: '2-digit',
  minute: '2-digit',
});
const integerFormat = new Intl.NumberFormat(UI_LOCALE);

type DateInput = string | number | Date;

export const formatDateTime = (value: DateInput) => dateTimeFormat.format(new Date(value));

export const formatDateTimeWithSeconds = (value: DateInput) => dateTimeWithSecondsFormat.format(new Date(value));

/** Axis ticks show only the time for short spans and add the day for spans longer than a day and a half. */
export function formatAxisTick(value: number, spanMs: number): string {
  return spanMs > 36 * 60 * 60 * 1000 ? dayAndTimeFormat.format(value) : shortTimeFormat.format(value);
}

export const formatTemperature = (celsius: number) => `${celsius.toFixed(1)} °C`;

export const formatInteger = (value: number) => integerFormat.format(value);
