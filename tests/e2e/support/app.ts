import { expect, type Page } from 'playwright/test';
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
  await page.waitForFunction(
    () => !document.body.innerText.includes('Loading Nuclear Evaluation'),
    null,
    { timeout: 30_000 }
  ).catch(() => undefined);
}

export async function completeCaptchaGate(page: Page) {
  await waitForApp(page);
  const body = await bodyText(page);
  if (!body.includes('Quick human check')) {
    return;
  }

  await page.getByText(/I accept\./).last().click();
  await page.getByRole('button', { name: /Accept.*verify.*human/i }).click();
  await page.waitForFunction(
    () => !document.body.innerText.includes('Quick human check'),
    null,
    { timeout: 120_000 }
  );
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
  await page.getByText('STEM Preview', { exact: true }).click();
  await page.locator('input[type="file"]').waitFor({ state: 'attached' });
}

export async function openQueryBuilder(page: Page) {
  await gotoApp(page, '/evaluation');
  await page.getByText('Query Builder', { exact: true }).click();
  await expect(page.getByText('Preset Filters')).toBeVisible();
}

export async function confirmDialogYes(page: Page) {
  await page.locator('.rz-dialog-wrapper, .rz-dialog').first().waitFor({ state: 'visible' });
  await page.getByRole('button', { name: /^Yes$/ }).click();
}

export function escapeDynamicLinqString(value: string) {
  return value.replace(/\\/g, '\\\\').replace(/"/g, '\\"');
}