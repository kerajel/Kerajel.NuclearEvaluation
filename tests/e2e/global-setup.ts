import { chromium, type FullConfig, type Page } from 'playwright/test';
import { mkdir } from 'node:fs/promises';
import { dirname } from 'node:path';
import { completeCaptchaGate, storageStatePath, waitForApp } from './support/app';

export default async function globalSetup(config: FullConfig) {
  const baseURL = process.env.E2E_BASE_URL ?? String(config.projects[0]?.use.baseURL ?? 'http://localhost:8080');
  await mkdir(dirname(storageStatePath), { recursive: true });

  const browser = await chromium.launch({
    headless: process.env.E2E_HEADLESS === '0' ? false : true,
    args: ['--disable-features=HttpsUpgrades,HttpsFirstBalancedModeAutoEnable']
  });
  const page = await browser.newPage({ baseURL });

  await gotoWhenReady(page, '/');
  await completeCaptchaGate(page);
  await waitForApp(page);
  await page.context().storageState({ path: storageStatePath });
  await browser.close();
}

async function gotoWhenReady(page: Page, path: string) {
  let lastError: unknown;

  for (let attempt = 0; attempt < 90; attempt++) {
    try {
      await page.goto(path, { waitUntil: 'domcontentloaded', timeout: 10_000 });
      return;
    } catch (error) {
      lastError = error;
      await page.waitForTimeout(1_000);
    }
  }

  throw lastError;
}