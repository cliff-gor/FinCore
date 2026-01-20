import { defineConfig, devices } from '@playwright/test';

const PORT = 4174;

/** Smoke tests run against `vite preview` of the production build; BFF calls are stubbed per test. */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: process.env.CI !== undefined,
  // One retry in CI so a single flaky run doesn't block the pipeline; locally a failure shows up at once.
  retries: process.env.CI !== undefined ? 1 : 0,
  reporter: process.env.CI !== undefined ? [['github'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: `http://localhost:${PORT}`,
    trace: 'retain-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: `pnpm exec vite preview --port ${PORT} --strictPort`,
    url: `http://localhost:${PORT}`,
    reuseExistingServer: process.env.CI === undefined,
    timeout: 60_000,
  },
});
