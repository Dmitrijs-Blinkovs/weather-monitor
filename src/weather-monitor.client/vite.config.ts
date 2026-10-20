/// <reference types="vitest/config" />
import { env } from 'node:process';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

// The ASP.NET Core SPA proxy starts "npm run dev" and passes the backend's URLs in through the environment.
const backendUrl = env.ASPNETCORE_HTTPS_PORT
  ? `https://localhost:${env.ASPNETCORE_HTTPS_PORT}`
  : (env.ASPNETCORE_URLS?.split(';')[0] ?? 'https://localhost:7180');

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      // The development certificate is self-signed, hence secure: false.
      '/api': { target: backendUrl, secure: false },
      '/health': { target: backendUrl, secure: false },
    },
  },
  test: {
    // Unit/component tests only; browser tests in e2e/ run with Playwright.
    include: ['src/**/*.test.{ts,tsx}'],
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    css: false,
    restoreMocks: true,
    coverage: {
      provider: 'v8',
      include: ['src/**/*.{ts,tsx}'],
      exclude: ['src/**/*.test.{ts,tsx}', 'src/test/**', 'src/main.tsx'],
      reporter: ['text-summary', 'html', 'cobertura'],
    },
  },
});
