import { expect, type Page } from '@playwright/test';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const supportDir = dirname(fileURLToPath(import.meta.url));
export const storageStatePath = resolve(supportDir, '../.auth/storageState.json');

export type ApiResult<T = unknown> = {
  ok: boolean;
  status: number;
  contentType: string | null;
  json: T | null;
  text: string;
};

export async function waitForApp(page: Page) {
  await page.waitForLoadState('domcontentloaded');
  await expect(page.locator('#site-content, .captcha-panel').first()).toBeVisible({ timeout: 30_000 });
}

export async function completeCaptchaGate(page: Page) {
  await waitForApp(page);
  if (await isCaptchaVerified(page)) {
    return;
  }

  const gate = page.getByText('Quick human check', { exact: true });
  await expect(gate).toBeVisible({ timeout: 120_000 });
  await page.getByText(/I accept\./).last().click();
  await page.getByRole('button', { name: /Accept.*verify.*human/i }).click();
  await expect(gate).toBeHidden({ timeout: 120_000 });
  expect(await isCaptchaVerified(page), 'the browser verification cookie is accepted').toBe(true);
}

async function isCaptchaVerified(page: Page) {
  const response = await page.request.get('/api/captcha/status');
  if (!response.ok()) {
    throw new Error(`Human check status endpoint returned ${response.status()}.`);
  }
  const status = await response.json() as { verified?: boolean };
  if (typeof status.verified !== 'boolean') {
    throw new Error('Human check status endpoint did not return a verification state.');
  }
  return status.verified;
}

export async function gotoApp(page: Page, path: string) {
  await page.goto(path, { waitUntil: 'domcontentloaded' });
  await waitForApp(page);
}

export async function bodyText(page: Page) {
  return page.locator('body').innerText({ timeout: 30_000 });
}

export async function expectNoVisibleAppError(page: Page) {
  const state = await page.evaluate(() => {
    const blazor = document.querySelector<HTMLElement>('#blazor-error-ui');
    const body = document.body.innerText;
    return {
      blazorVisible: !!blazor && getComputedStyle(blazor).display !== 'none',
      fetchError: body.includes('An error occurred while fetching entries'),
      body: body.slice(0, 800)
    };
  });

  expect(state.blazorVisible, state.body).toBe(false);
  expect(state.fetchError, state.body).toBe(false);
}

async function apiFetch<T>(page: Page, url: string, method: string, body?: unknown): Promise<ApiResult<T>> {
  if (page.url() === 'about:blank') {
    await gotoApp(page, '/');
  }

  return page.evaluate(async ({ url, method, body }) => {
    const response = await fetch(url, {
      method,
      headers: body === undefined ? undefined : { 'content-type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body)
    });
    const text = await response.text();
    let json = null;
    try {
      json = text ? JSON.parse(text) : null;
    } catch {
      json = null;
    }

    return {
      ok: response.ok,
      status: response.status,
      contentType: response.headers.get('content-type'),
      json,
      text
    };
  }, { url, method, body });
}

export async function apiGet<T = unknown>(page: Page, url: string) {
  return apiFetch<T>(page, url, 'GET');
}

export async function apiPost<T = unknown>(page: Page, url: string, body: unknown) {
  return apiFetch<T>(page, url, 'POST', body);
}

export async function apiPut<T = unknown>(page: Page, url: string, body: unknown) {
  return apiFetch<T>(page, url, 'PUT', body);
}

export async function apiDelete<T = unknown>(page: Page, url: string, body?: unknown) {
  return apiFetch<T>(page, url, 'DELETE', body);
}

export async function openStemPreview(page: Page) {
  await gotoApp(page, '/data-management');
  const tab = page.getByRole('tab', { name: 'STEM Preview' });
  await tab.click();
  await expect(tab).toHaveAttribute('aria-selected', 'true');
  await page.locator('.stem-dropzone').waitFor({ state: 'visible' });
  await page.locator('input[type="file"].stem-dropzone-input').waitFor({ state: 'attached' });
}

export async function openQueryBuilder(page: Page) {
  await gotoApp(page, '/evaluation');
  const tab = page.getByRole('tab', { name: 'Query Builder', exact: true });
  await tab.click();
  await expect(tab).toHaveAttribute('aria-selected', 'true');
  await expect(page.getByText('Preset Filters', { exact: true })).toBeVisible();
}

export async function confirmDialogYes(page: Page) {
  await page.locator('.rz-dialog-wrapper, .rz-dialog').first().waitFor({ state: 'visible' });
  await page.getByRole('button', { name: /^Yes$/ }).click();
}

export function escapeDynamicLinqString(value: string) {
  return value.replace(/\\/g, '\\\\').replace(/"/g, '\\"');
}
