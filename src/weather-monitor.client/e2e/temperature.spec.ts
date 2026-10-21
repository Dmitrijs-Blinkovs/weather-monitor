import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';
import { CITIES, mockApi } from './support/mockApi';

const ENGLISH_DATE = /\d{1,2} (Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sept?|Oct|Nov|Dec) \d{4}, \d{2}:\d{2}/;

test.describe('Temperature page', () => {
  test('shows every city with country, temperature, min/max and last update time', async ({ page }) => {
    await mockApi(page);
    await page.goto('/temperature');

    const cards = page.getByRole('list', { name: 'Cities in the selected period' }).getByRole('listitem');
    await expect(cards).toHaveCount(3);

    // Alphabetical, like the legend and the tooltip - not by temperature (the mock keeps Rome warmest, Riga coldest).
    await expect(cards.getByRole('heading')).toHaveText(['London', 'Riga', 'Rome']);

    for (const { city, country } of CITIES) {
      const card = cards.filter({ has: page.getByRole('heading', { name: city }) });
      await expect(card.getByText(country, { exact: true })).toBeVisible();
      await expect(card.getByText(/^-?\d+\.\d °C$/).first()).toBeVisible();
      await expect(card.getByText('Min')).toBeVisible();
      await expect(card.getByText('Max')).toBeVisible();
      await expect(card.locator('time')).toHaveText(ENGLISH_DATE);
    }
  });

  test('draws all cities on one chart with a legend and direct labels', async ({ page }) => {
    await mockApi(page);
    await page.goto('/temperature');

    const chart = page.getByRole('figure');
    await expect(chart.locator('.recharts-line')).toHaveCount(3);
    await expect(chart.locator('.recharts-area')).toHaveCount(3);
    for (const { city, country } of CITIES) {
      await expect(chart.getByText(`${city}, ${country}`)).toBeVisible();
      await expect(chart.locator('.chart-direct-label', { hasText: city })).toBeVisible();
    }

    await page.getByLabel('Min/max band').uncheck();
    await expect(chart.locator('.recharts-area')).toHaveCount(0);
  });

  test('a city with a single observation so far is drawn as a point', async ({ page }) => {
    await mockApi(page, { singleObservationCities: ['Riga'] });
    await page.goto('/temperature');

    const chart = page.getByRole('figure');
    await expect(chart.locator('.recharts-line-dots .recharts-dot')).toHaveCount(1);
    await expect(chart.locator('.chart-direct-label', { hasText: 'Riga' })).toBeVisible();
  });

  test('the tooltip lists every city at the hovered time', async ({ page }) => {
    await mockApi(page);
    await page.goto('/temperature');

    const plot = page.locator('.recharts-wrapper');
    const box = (await plot.boundingBox())!;
    await page.mouse.move(box.x + box.width * 0.92, box.y + box.height / 2);

    const tooltip = page.locator('.chart-tooltip');
    await expect(tooltip).toBeVisible();
    for (const { city, country } of CITIES) {
      await expect(tooltip.getByText(`${city}, ${country}`)).toBeVisible();
    }
  });

  test('requests the last 24 hours by default and the chosen preset afterwards', async ({ page }) => {
    const api = await mockApi(page);
    await page.goto('/temperature');
    await expect(page.getByRole('figure')).toBeVisible();

    expect(spanHours(api.historyRequests()[0]!)).toBe(24);

    await page.getByRole('button', { name: 'Last 7 days' }).click();
    await expect.poll(() => api.historyRequests().map(spanHours)).toContain(7 * 24);
    await expect(page.getByRole('button', { name: 'Last 7 days' })).toHaveAttribute('aria-pressed', 'true');
  });

  test('filters by a custom date and time range in the viewer\'s time zone', async ({ page }) => {
    const api = await mockApi(page);
    await page.goto('/temperature');
    await expect(page.getByRole('figure')).toBeVisible();

    await page.getByLabel('From').fill('2026-10-18T08:00');
    await page.getByLabel('To').fill('2026-10-19T08:30');
    await page.getByRole('button', { name: 'Apply' }).click();

    // Europe/Riga is UTC+3 in October.
    await expect
      .poll(() => api.historyRequests().map((url) => `${url.searchParams.get('from')}|${url.searchParams.get('to')}`))
      .toContain('2026-10-18T05:00:00.000Z|2026-10-19T05:30:00.000Z');
    await expect(page.getByRole('button', { name: 'Last 24 hours' })).toHaveAttribute('aria-pressed', 'false');
  });

  test('explains an invalid range instead of requesting it', async ({ page }) => {
    const api = await mockApi(page);
    await page.goto('/temperature');
    await expect(page.getByRole('figure')).toBeVisible();
    const before = api.historyRequests().length;

    await page.getByLabel('From').fill('2026-10-19T10:00');
    await page.getByLabel('To').fill('2026-10-19T09:00');
    await page.getByRole('button', { name: 'Apply' }).click();

    await expect(page.getByRole('alert')).toHaveText('"From" must be earlier than "To".');
    expect(api.historyRequests()).toHaveLength(before);
  });

  test('lists every reading as a table, page by page', async ({ page }) => {
    const api = await mockApi(page);
    await page.goto('/temperature');

    await page.getByLabel('Show as table').check();

    const table = page.getByRole('table');
    await expect(table.getByRole('row').first()).toContainText('Fetched at');
    await expect(table.getByRole('row').first()).toContainText('Last update');
    await expect(table.getByRole('cell', { name: 'IT', exact: true }).first()).toBeVisible();
    // A page holds 20 minutes of fetches: 60 readings plus the header row.
    await expect(table.getByRole('row')).toHaveCount(61);

    const pages = page.getByRole('navigation', { name: 'Reading pages' });
    await expect(pages).toContainText('Page 1 of 6 · 360 readings');
    await pages.getByRole('button', { name: 'Next' }).click();

    await expect(pages).toContainText('Page 2 of 6');
    expect(api.readingRequests().at(-1)?.searchParams.get('page')).toBe('2');
  });

  for (const colorScheme of ['light', 'dark'] as const) {
    test(`has no detectable accessibility violations in ${colorScheme} mode`, async ({ page }) => {
      await page.emulateMedia({ colorScheme });
      await mockApi(page);
      await page.goto('/temperature');
      await expect(page.getByRole('figure')).toBeVisible();

      const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa']).analyze();
      expect(results.violations).toEqual([]);

      await page.getByLabel('Show as table').check();
      await expect(page.getByRole('navigation', { name: 'Reading pages' })).toBeVisible();

      const tableResults = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa']).analyze();
      expect(tableResults.violations).toEqual([]);
    });
  }
});

function spanHours(url: URL): number {
  const from = Date.parse(url.searchParams.get('from') ?? '');
  const to = Date.parse(url.searchParams.get('to') ?? '');
  return Math.round((to - from) / 3_600_000);
}
