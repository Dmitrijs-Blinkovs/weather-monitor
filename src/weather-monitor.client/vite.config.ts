/// <reference types="vitest/config" />
import { env } from 'node:process';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

// The ASP.NET Core SPA proxy starts "npm run dev" and passes the backend's URLs in through the environment.
const backendUrl = env.ASPNETCORE_HTTPS_PORT
  ? `https://localhost:${env.ASPNETCORE_HTTPS_PORT}`
  : (env.ASPNETCORE_URLS?.split(';')[0] ?? 'https://localhost:7180');

// Mirror of src/WeatherMonitor.Web/Security/SecurityHeaders.cs. The Playwright tests run against `vite preview`,
// so serving the same headers there proves the production build works under the strict content security policy.
const securityHeaders = {
  'Content-Security-Policy':
    "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self'; font-src 'self'; " +
    "connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'",
  'X-Content-Type-Options': 'nosniff',
  'X-Frame-Options': 'DENY',
  'Referrer-Policy': 'strict-origin-when-cross-origin',
  'Permissions-Policy':
    'accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()',
  'Cross-Origin-Opener-Policy': 'same-origin',
  'Cross-Origin-Resource-Policy': 'same-origin',
};

// Node binds "localhost" to IPv6 ::1 only. Where IPv6 loopback is blocked (VPN or security software), connections to
// ::1 hang and the SPA proxy and Playwright never reach Vite; IPv4 loopback works everywhere.
const host = '127.0.0.1';

export default defineConfig({
  plugins: [react()],
  preview: {
    host,
    headers: securityHeaders,
  },
  server: {
    host,
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
