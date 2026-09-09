import { chromium, type FullConfig, type Page } from '@playwright/test';
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
  await waitForSeedData(page);
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

type SeedState = {
  ok: boolean;
  status: number;
  projectCount: number;
  sampleCount: number;
  seriesIds: string;
};

async function waitForSeedData(page: Page) {
  const configuredTimeout = Number.parseInt(process.env.E2E_SEED_TIMEOUT_MS ?? '', 10);
  const timeoutMs = Number.isFinite(configuredTimeout) && configuredTimeout > 0
    ? configuredTimeout
    : 20 * 60 * 1_000;
  const deadline = Date.now() + timeoutMs;
  let lastState: SeedState | { error: string } | undefined;

  while (Date.now() < deadline) {
    try {
      const state = await page.evaluate(async () => {
        const response = await fetch('/api/views/projects', {
          method: 'POST',
          headers: { 'content-type': 'application/json' },
          body: JSON.stringify({ filter: 'Id == 1', top: 1, skip: 0 })
        });
        const payload = response.ok ? await response.json() : null;
        const project = payload?.entries?.[0];

        return {
          ok: response.ok,
          status: response.status,
          projectCount: payload?.totalCount ?? 0,
          sampleCount: project?.sampleCount ?? 0,
          seriesIds: project?.seriesIds ?? ''
        };
      });

      lastState = state;
      if (state.ok && state.projectCount === 1 && state.sampleCount > 0 && state.seriesIds.length > 0) {
        return;
      }
    } catch (error) {
      lastState = { error: error instanceof Error ? error.message : String(error) };
    }

    await page.waitForTimeout(2_000);
  }

  throw new Error(
    'Timed out waiting for the complete sandbox seed. Last state: ' + JSON.stringify(lastState)
  );
}
