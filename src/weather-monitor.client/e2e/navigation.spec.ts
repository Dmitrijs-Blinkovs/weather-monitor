import { expect, test } from '@playwright/test';
import { mockApi } from './support/mockApi';

test('the root opens the temperature page and the header switches between pages', async ({ page }) => {
  await mockApi(page);
  await page.goto('/');

  await expect(page).toHaveURL(/\/temperature$/);
  await expect(page.getByRole('heading', { level: 1, name: 'Temperature' })).toBeVisible();

  await page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: 'Fetch log' }).click();
  await expect(page).toHaveURL(/\/fetch-log$/);
  await expect(page.getByRole('heading', { level: 1, name: 'Fetch log' })).toBeVisible();

  await page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: 'Temperature' }).click();
  await expect(page.getByRole('heading', { level: 1, name: 'Temperature' })).toBeVisible();
});

test('a deep link opens the page directly', async ({ page }) => {
  await mockApi(page);
  await page.goto('/fetch-log');

  await expect(page.getByRole('heading', { level: 1, name: 'Fetch log' })).toBeVisible();
});

test('the page is in English even in a Latvian browser', async ({ page }) => {
  await mockApi(page);
  await page.goto('/temperature');

  await expect(page.locator('html')).toHaveAttribute('lang', 'en');
  await expect(page.getByRole('button', { name: 'Last 24 hours' })).toBeVisible();
  await expect(page.getByText(/^Refreshed \d{1,2} [A-Z][a-z]{2} \d{4}, \d{2}:\d{2}$/)).toBeVisible();
});
