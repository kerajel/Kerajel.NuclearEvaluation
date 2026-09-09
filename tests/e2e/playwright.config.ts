import { defineConfig, devices } from '@playwright/test';
import { storageStatePath } from './support/app';

const baseURL = process.env.E2E_BASE_URL ?? 'http://localhost:8080';
const configuredWorkers = Number.parseInt(process.env.E2E_WORKERS ?? '', 10);
const workers = Number.isFinite(configuredWorkers) && configuredWorkers > 0
  ? configuredWorkers
  : process.env.CI
    ? 2
    : undefined;

export default defineConfig({
  testDir: './specs',
  globalSetup: './global-setup',
  fullyParallel: true,
  workers,
  timeout: 120_000,
  expect: { timeout: 15_000 },
  forbidOnly: Boolean(process.env.CI),
  failOnFlakyTests: Boolean(process.env.CI),
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI
    ? [['github'], ['html', { open: 'never' }]]
    : [['list'], ['html', { open: 'never' }]],
  outputDir: './test-results',
  use: {
    baseURL,
    storageState: storageStatePath,
    headless: process.env.E2E_HEADLESS === '0' ? false : true,
    actionTimeout: 30_000,
    navigationTimeout: 30_000,
    trace: process.env.CI ? 'on-first-retry' : 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'off',
    launchOptions: {
      args: ['--disable-features=HttpsUpgrades,HttpsFirstBalancedModeAutoEnable']
    }
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] }
    }
  ]
});
