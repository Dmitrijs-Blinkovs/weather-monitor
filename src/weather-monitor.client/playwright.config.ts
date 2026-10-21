import { env } from 'node:process';
import { defineConfig, devices } from '@playwright/test';

const port = 4173;

/**
 * Browser tests against the production build (`vite preview`). The API is mocked per test with `page.route`
 * (see e2e/support/mockApi.ts), so the suite needs no backend, database or API key and runs the same in CI.
 */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: Boolean(env.CI),
  retries: env.CI ? 2 : 0,
  reporter: env.CI ? [['github'], ['html', { open: 'never' }]] : [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: `http://127.0.0.1:${port}`,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    // A Latvian browser in Riga proves the UI stays English and times are shown in the viewer's zone.
    locale: 'lv-LV',
    timezoneId: 'Europe/Riga',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: `npm run build && npm run preview -- --port ${port} --strictPort`,
    url: `http://127.0.0.1:${port}`,
    reuseExistingServer: !env.CI,
    timeout: 180_000,
  },
});
