import { defineConfig, devices } from 'playwright/test';
import { storageStatePath } from './support/app';

const baseURL = process.env.E2E_BASE_URL ?? 'http://localhost:8080';

export default defineConfig({
  testDir: './specs',
  globalSetup: './global-setup',
  fullyParallel: false,
  workers: 1,
  timeout: 120_000,
  expect: { timeout: 15_000 },
  retries: process.env.CI ? 1 : 0,
  reporter: [['list'], ['html', { open: 'never' }]],
  outputDir: './test-results',
  use: {
    baseURL,
    storageState: storageStatePath,
    headless: process.env.E2E_HEADLESS === '0' ? false : true,
    actionTimeout: 30_000,
    navigationTimeout: 30_000,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
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