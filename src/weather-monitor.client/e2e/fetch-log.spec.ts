import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';
import { mockApi } from './support/mockApi';

test.describe('Fetch log page', () => {
  test('lists attempts newest first with result, HTTP status, duration and error', async ({ page }) => {
    await mockApi(page);
    await page.goto('/fetch-log');

    const rows = page.getByRole('table').getByRole('row');
    await expect(rows).toHaveCount(26); // header + 25 rows (default page size)
    await expect(rows.nth(1)).toContainText('London');
    await expect(rows.nth(1)).toContainText('Success');
    await expect(rows.nth(1)).toContainText('19 Oct 2026, 15:00:00'); // 12:00 UTC shown in Riga time, in English

    const failed = rows.filter({ hasText: 'Rome' }).first();
    await expect(failed).toContainText('Failed');
    await expect(failed).toContainText('401');
    await expect(failed).toContainText('Invalid API key');
    await expect(page.getByText('Page 1 of 3 · 60 attempts')).toBeVisible();
  });

  test('filters by city and result and starts again from the first page', async ({ page }) => {
    const api = await mockApi(page);
    await page.goto('/fetch-log');
    await page.getByRole('button', { name: 'Next' }).click();
    await expect(page.getByText(/Page 2 of 3/)).toBeVisible();

    await page.getByLabel('City').selectOption('Rome');
    await page.getByLabel('Result').selectOption('Failure');

    const rows = page.getByRole('table').getByRole('row');
    await expect(rows).toHaveCount(3);
    await expect(page.getByText('Page 1 of 1 · 2 attempts')).toBeVisible();
    const last = api.logRequests().at(-1)!;
    expect(Object.fromEntries(last.searchParams)).toMatchObject({ page: '1', city: 'Rome', status: 'Failure' });
  });

  test('pages through the log', async ({ page }) => {
    const api = await mockApi(page);
    await page.goto('/fetch-log');

    await page.getByRole('button', { name: 'Next' }).click();
    await page.getByRole('button', { name: 'Next' }).click();

    await expect(page.getByText('Page 3 of 3 · 60 attempts')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Next' })).toBeDisabled();
    expect(api.logRequests().at(-1)!.searchParams.get('page')).toBe('3');
  });

  test('shows an error with a retry button when the API fails', async ({ page }) => {
    await mockApi(page, { logsStatus: 503 });
    await page.goto('/fetch-log');

    await expect(page.getByRole('alert')).toContainText('Could not load data.');
    await expect(page.getByRole('button', { name: 'Retry' })).toBeVisible();
  });

  for (const colorScheme of ['light', 'dark'] as const) {
    test(`has no detectable accessibility violations in ${colorScheme} mode`, async ({ page }) => {
      await page.emulateMedia({ colorScheme });
      await mockApi(page);
      await page.goto('/fetch-log');
      await expect(page.getByRole('table')).toBeVisible();

      const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa']).analyze();
      expect(results.violations).toEqual([]);
    });
  }
});
