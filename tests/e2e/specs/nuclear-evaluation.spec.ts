import { expect, test, type Page } from 'playwright/test';
import { randomUUID } from 'node:crypto';
import { closeSync, openSync, statSync, writeSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  apiDelete,
  apiGet,
  apiPost,
  apiPut,
  bodyText,
  confirmDialogYes,
  escapeDynamicLinqString,
  expectNoVisibleAppError,
  gotoApp,
  openQueryBuilder,
  openStemPreview,
  waitForApp
} from '../support/app';

const specDir = dirname(fileURLToPath(import.meta.url));
const fixtureDir = resolve(specDir, '../fixtures');
const stemA = resolve(fixtureDir, 'stem-a.csv');
const stemB = resolve(fixtureDir, 'stem-b.csv');
const stemBad = resolve(fixtureDir, 'stem-bad.csv');
const stemDat = resolve(fixtureDir, 'stem-tab.dat');
const stemGood = resolve(fixtureDir, 'stem-good.csv');
const stemTsv = resolve(fixtureDir, 'stem-tab.tsv');

type FetchDataResult<T = Record<string, unknown>> = {
  isSuccessful?: boolean;
  totalCount: number;
  entries: T[];
};

type SeriesView = {
  id: number;
  seriesType: number;
  createdAt: string;
  sgasComment: string;
  workingPaperLink: string;
  isDu: boolean;
  isNu: boolean;
  analysisCompleteDate: string | null;
  sampleCount: number;
};

type SampleView = {
  externalCode: string;
};

type ProjectView = {
  id: number;
  name: string;
};

type PresetFilterEntry = {
  id?: number;
  presetFilterEntryType: number;
  logicalFilterOperator?: number;
  isEnabled?: boolean;
  presetFilterId?: number;
  serializedDescriptors?: string;
};

type PresetFilter = {
  id: number;
  name: string;
  entries: PresetFilterEntry[];
};

type SeriesCountsView = {
  seriesCount: number;
  sampleCount: number;
  subSampleCount: number;
  apmCount: number;
  particleCount: number;
};

type ChartBinCounts = {
  isotope: string;
  bins: unknown[];
};


test.beforeEach(async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
});

test('CAP-01 captcha status remains verified in a fresh browser session', async ({ page }) => {
  await gotoApp(page, '/data-management');
  const status = await apiGet<{ verified: boolean }>(page, '/api/captcha/status');
  const text = await bodyText(page);

  expect(status.ok).toBe(true);
  expect(status.json?.verified).toBe(true);
  expect(text).not.toContain('Quick human check');
});

test('NAV-01 home page and sidebar navigation render', async ({ page }) => {
  await gotoApp(page, '/');
  const text = await bodyText(page);

  await expect(page.getByText('Nuclear Evaluation').first()).toBeVisible();
  expect(text).toContain('Home');
  expect(text).toContain('Data Management');
  expect(text).toContain('Evaluation');
  await expectNoVisibleAppError(page);
});

test('NAV-02 unknown route shows friendly not-found page', async ({ page }) => {
  await gotoApp(page, '/definitely-not-a-real-route');

  await expect(page.getByText('Page not found')).toBeVisible();
  await expect(page.getByText("Sorry, but there's nothing here!")).toBeVisible();
  await expectNoVisibleAppError(page);
});

test('UI-01 dark/light appearance toggle changes the active theme', async ({ page }) => {
  await gotoApp(page, '/data-management');
  const before = await themeState(page);

  await page.locator('button:has-text("light_mode"), button:has-text("dark_mode")').first().click();
  await page.waitForTimeout(750);
  const after = await themeState(page);

  expect(after).not.toEqual(before);
});

test('DM-01 Data Management Series tab loads totals and first page without fetch errors', async ({ page }) => {
  await gotoApp(page, '/data-management');
  await page.waitForFunction(() => document.body.innerText.includes('TOTAL SERIES') && document.body.innerText.includes('100,000'), null, { timeout: 60_000 });
  const counts = await apiPost<SeriesCountsView>(page, '/api/views/series-counts', { top: 1, skip: 0 });

  expect(counts.ok).toBe(true);
  expect(counts.json?.seriesCount).toBe(100_000);
  expect(counts.json?.sampleCount).toBeGreaterThan(0);
  expect(counts.json?.subSampleCount).toBeGreaterThan(0);
  expect(counts.json?.apmCount).toBeGreaterThan(0);
  expect(counts.json?.particleCount).toBeGreaterThan(0);

  const expectedCounts = [
    counts.json!.seriesCount,
    counts.json!.sampleCount,
    counts.json!.subSampleCount,
    counts.json!.apmCount,
    counts.json!.particleCount
  ].map(value => value.toLocaleString('en-US'));

  await page.waitForFunction(
    values => values.every(value => document.body.innerText.includes(value)),
    expectedCounts,
    { timeout: 60_000 }
  );

  const text = await bodyText(page);

  expect(text).toContain('TOTAL SERIES');
  for (const value of expectedCounts) {
    expect(text).toContain(value);
  }
  expect(text).toContain('10000');
  await expectNoVisibleAppError(page);
});

test('DM-02 no PMI upload/dashboard affordances remain in the app shell', async ({ page }) => {
  await gotoApp(page, '/data-management');
  const dmText = await bodyText(page);
  await gotoApp(page, '/evaluation');
  const evalText = await bodyText(page);

  expect(`${dmText}\n${evalText}`).not.toMatch(/\bPMI\b|PMI upload|PMI dashboard/i);
});

test('DM-03 grid result cache and reserved grid height are active', async ({ page }) => {
  await gotoApp(page, '/data-management');
  await page.waitForFunction(() => document.body.innerText.includes('TOTAL SERIES') && document.body.innerText.includes('100,000'), null, { timeout: 60_000 });
  const state = await page.evaluate(() => {
    const keys = Object.keys(localStorage);
    const grids = [...document.querySelectorAll<HTMLElement>('.rz-data-grid')].map(grid => {
      const style = getComputedStyle(grid);
      const rect = grid.getBoundingClientRect();
      return {
        minHeight: parseFloat(style.minHeight || '0') || 0,
        height: rect.height
      };
    });
    return {
      keys,
      maxReservedHeight: Math.max(0, ...grids.map(grid => Math.max(grid.minHeight, grid.height)))
    };
  });

  expect(state.keys.some(key => key.includes('SeriesGrid'))).toBe(true);
  expect(state.maxReservedHeight).toBeGreaterThanOrEqual(300);
});

test('DM-04 disabled delete tooltip is native title and does not create horizontal overflow', async ({ page }) => {
  await gotoApp(page, '/data-management');
  await page.locator('[title="Series contains samples"]').first().waitFor({ state: 'attached' });
  const state = await page.evaluate(() => ({
    popupText: document.querySelector('.rz-popup')?.textContent ?? '',
    scrollWidth: document.documentElement.scrollWidth,
    clientWidth: document.documentElement.clientWidth
  }));

  expect(state.popupText).not.toContain('Series contains samples');
  expect(state.scrollWidth).toBeLessThanOrEqual(state.clientWidth + 8);
});

test('BE-01 project view filtering by id succeeds with includes applied last', async ({ page }) => {
  const result = await apiPost<FetchDataResult>(page, '/api/views/projects', { filter: 'Id == 1', top: 5, skip: 0 });

  expect(result.ok).toBe(true);
  expect(result.json?.isSuccessful).not.toBe(false);
  expect(result.json?.totalCount).toBe(1);
  expect(Array.isArray(result.json?.entries[0]?.projectSeries)).toBe(true);
});

test('BE-02 opening /projects/1 loads project detail instead of not-found', async ({ page }) => {
  await gotoApp(page, '/projects/1');
  await expect(page.getByText('Back to Projects', { exact: true })).toBeVisible({ timeout: 30_000 });
  const text = await bodyText(page);

  expect(text).not.toContain('Page not found');
  for (const label of ['Overview', 'Series', 'Samples', 'SubSamples', 'Apm', 'Particles']) {
    expect(text).toContain(label);
  }
  await expectNoVisibleAppError(page);
});

test('BE-03 Sample External Code filtering works through data/query-builder-compatible API', async ({ page }) => {
  const first = await apiPost<FetchDataResult<SampleView>>(page, '/api/views/samples', { top: 1, skip: 0 });
  expect(first.ok).toBe(true);
  const code = first.json?.entries[0]?.externalCode;
  expect(code).toBeTruthy();

  const escaped = escapeDynamicLinqString(code!);
  const direct = await apiPost<FetchDataResult>(page, '/api/views/samples', {
    filter: `ExternalCode == "${escaped}"`,
    top: 20,
    skip: 0
  });
  const queryBuilder = await apiPost<FetchDataResult>(page, '/api/views/samples', {
    top: 20,
    skip: 0,
    presetFilterBox: { filters: { '2': `Sample.ExternalCode == "${escaped}"` } }
  });

  expect(direct.ok).toBe(true);
  expect(direct.json?.totalCount).toBeGreaterThan(0);
  expect(queryBuilder.ok).toBe(true);
  expect(queryBuilder.json?.totalCount).toBeGreaterThan(0);
});

test('BE-04 project-scoped grids and charts return data for project 1', async ({ page }) => {
  for (const endpoint of ['series', 'samples', 'subsamples', 'apm', 'particles']) {
    const result = await apiPost<FetchDataResult>(page, `/api/views/${endpoint}`, { projectId: 1, top: 5, skip: 0 });
    expect(result.ok, endpoint).toBe(true);
    expect(result.json?.totalCount, endpoint).toBeGreaterThan(0);
  }

  const apm = await apiGet<unknown[]>(page, '/api/charts/apm-bin-counts/1');
  const particle = await apiGet<unknown[]>(page, '/api/charts/particle-bin-counts/1');
  expect(apm.ok).toBe(true);
  expect(Array.isArray(apm.json)).toBe(true);
  expect(particle.ok).toBe(true);
  expect(Array.isArray(particle.json)).toBe(true);
});

test('UI-02 Evaluation query-builder shell renders expected controls', async ({ page }) => {
  await openQueryBuilder(page);
  const text = await bodyText(page);

  for (const label of ['Series', 'Sample', 'SubSample', 'Apm', 'Particle', 'Apply', 'Preset Filters']) {
    expect(text).toContain(label);
  }
  await expectNoVisibleAppError(page);
});

test('STEM-01 STEM tab exposes sample downloads and multi-file drop target', async ({ page }) => {
  await openStemPreview(page);
  const state = await page.evaluate(() => {
    const input = document.querySelector<HTMLInputElement>('input[type="file"]');
    const content = document.querySelector<HTMLElement>('.stem-dropzone-content');
    return {
      hasSampleDownloadCopy: /sample file/i.test(document.body.innerText),
      links: document.querySelectorAll('a[href]').length,
      multiple: input?.multiple ?? false,
      accept: input?.accept ?? '',
      pointerEvents: content ? getComputedStyle(content).pointerEvents : null
    };
  });

  expect(state.hasSampleDownloadCopy).toBe(true);
  expect(state.links).toBeGreaterThanOrEqual(3);
  expect(state.multiple).toBe(true);
  expect(state.accept).toContain('.csv');
  expect(state.accept).toContain('.tsv');
  expect(state.accept).toContain('.dat');
  expect(state.accept).toContain('.xlsb');
  expect(state.pointerEvents).toBe('none');
});

test('STEM-02 uploading two STEM CSV files previews both, deleting one removes only its rows', async ({ page }) => {
  await openStemPreview(page);
  await page.locator('input[type="file"]').setInputFiles([stemA, stemB]);
  await page.waitForFunction(() => document.body.innerText.includes('stem-a.csv') && document.body.innerText.includes('stem-b.csv'));
  await page.locator('button:has-text("Upload Files")').last().click();
  await page.waitForFunction(
    () => document.body.innerText.includes('Uploaded') && document.body.innerText.includes('LAB-A') && document.body.innerText.includes('LAB-B'),
    null,
    { timeout: 120_000 }
  );

  const compactMinHeight = await page.locator('.ne-compact-grid .rz-data-grid').first().evaluate(e => getComputedStyle(e).minHeight);
  await page.locator('.ne-compact-grid button:has-text("delete")').first().click();
  await confirmDialogYes(page);
  await page.waitForTimeout(2_500);
  const text = await bodyText(page);

  expect(compactMinHeight === '0px' || compactMinHeight === '0').toBe(true);
  expect(text).not.toMatch(/LAB-A|900001|900002|stem-a\.csv/);
  expect(text).toMatch(/LAB-B|910001|910002|stem-b\.csv/);
});

test('STEM-04 uploading TSV and DAT files previews both', async ({ page }) => {
  await openStemPreview(page);
  await page.locator('input[type="file"]').setInputFiles([stemTsv, stemDat]);
  await page.waitForFunction(() => document.body.innerText.includes('stem-tab.tsv') && document.body.innerText.includes('stem-tab.dat'));
  await page.locator('button:has-text("Upload Files")').last().click();
  await page.waitForFunction(
    () => document.body.innerText.includes('Uploaded') && document.body.innerText.includes('LAB-TSV') && document.body.innerText.includes('LAB-DAT'),
    null,
    { timeout: 120_000 }
  );
  await expectNoVisibleAppError(page);
});

test('STEM-03 invalid STEM file reports upload error without breaking the page', async ({ page }) => {
  await openStemPreview(page);
  await page.locator('input[type="file"]').setInputFiles(stemBad);
  await page.waitForFunction(() => document.body.innerText.includes('stem-bad.csv'));
  await page.locator('button:has-text("Upload Files")').last().click();
  await page.waitForFunction(() => /Error processing the file|Header.*not found|not found/i.test(document.body.innerText), null, { timeout: 30_000 });
  await expectNoVisibleAppError(page);
});

test('CRUD-01 Series create/delete API round trip keeps seeded grid usable', async ({ page }) => {
  const unique = `QA temporary series ${Date.now()}`;
  const created = await createSeries(page, { sgasComment: unique, workingPaperLink: '/qa/temp', isNu: true });
  try {
    const fetched = await apiPost<FetchDataResult>(page, '/api/views/series', { filter: `Id == ${created}`, top: 5, skip: 0 });
    expect(fetched.ok).toBe(true);
    expect(fetched.json?.totalCount).toBe(1);
  } finally {
    const deleted = await apiDelete(page, '/api/series', [created]);
    expect(deleted.ok).toBe(true);
  }

  const after = await apiPost<FetchDataResult>(page, '/api/views/series', { filter: `Id == ${created}`, top: 5, skip: 0 });
  expect(after.ok).toBe(true);
  expect(after.json?.totalCount).toBe(0);
});

test('UI-09 Series row expansion loads child sample grid without layout tear', async ({ page }) => {
  await gotoApp(page, '/data-management');
  await page.locator('.rz-row-toggler').first().click();
  await page.waitForFunction(() => document.body.innerText.toUpperCase().includes('EXTERNAL CODE'));
  const state = await page.evaluate(() => {
    const nested = [...document.querySelectorAll<HTMLElement>('.rz-data-grid .rz-data-grid')][0];
    return {
      hasNested: !!nested,
      nestedMinHeight: nested ? getComputedStyle(nested).minHeight : '0px',
      scrollWidth: document.documentElement.scrollWidth,
      clientWidth: document.documentElement.clientWidth
    };
  });

  expect(state.hasNested).toBe(true);
  expect(parseFloat(state.nestedMinHeight)).toBeGreaterThanOrEqual(100);
  expect(state.scrollWidth).toBeLessThanOrEqual(state.clientWidth + 8);
});

test('UI-04 preset-filter save button enables only after valid name and creates filter', async ({ page }) => {
  await openQueryBuilder(page);
  await page.locator('button:has-text("save")').last().click();
  await page.locator('input[name="FilterName"]').waitFor({ state: 'visible' });

  const disabledBefore = await isDisabled(page.locator('button:has-text("check")').last());
  const name = `QAFilter${Date.now().toString().slice(-10)}`;
  await page.locator('input[name="FilterName"]').fill(name);
  await expect.poll(async () => isDisabled(page.locator('button:has-text("check")').last())).toBe(false);

  expect(disabledBefore).toBe(true);
  await page.locator('button:has-text("check")').last().click();
  await page.waitForTimeout(1_200);

  const filters = await apiGet<Array<{ id: number; name: string }>>(page, '/api/preset-filters');
  const created = filters.json?.find(filter => filter.name === name);
  expect(created).toBeTruthy();
  const cleanup = await apiDelete(page, `/api/preset-filters/${created!.id}`);
  expect(cleanup.ok).toBe(true);
});

test('UI-08 remove File during large upload cancels immediately', async ({ page }, testInfo) => {
  const slowFile = testInfo.outputPath('stem-slow-cancel.csv');
  writeLargeStemCsv(slowFile, 256 * 1024);
  let markUploadStarted = () => { };
  const uploadStarted = new Promise<void>(resolve => {
    markUploadStarted = resolve;
  });
  let releaseUpload = () => { };
  const uploadRelease = new Promise<void>(resolve => {
    releaseUpload = resolve;
  });
  let uploadRouteDone = Promise.resolve();

  await page.route('**/api/stem/*/files', async route => {
    if (route.request().method() !== 'POST') {
      await route.continue();
      return;
    }

    uploadRouteDone = (async () => {
      markUploadStarted();
      await uploadRelease;
      await route.abort('aborted').catch(() => undefined);
    })();
    await uploadRouteDone;
  });

  await openStemPreview(page);
  await page.locator('input[type="file"]').setInputFiles(slowFile);
  await page.waitForFunction(() => document.body.innerText.includes('stem-slow-cancel.csv') && document.body.innerText.includes('Upload Files'));
  await page.locator('button:has-text("Upload Files")').last().click();
  await uploadStarted;

  try {
    await page.locator('.ne-compact-grid button:has-text("delete")').first().click();
    await confirmDialogYes(page);
    await expect(page.getByText('stem-slow-cancel.csv')).toHaveCount(0);

    const text = await bodyText(page);
    expect(text).not.toContain('stem-slow-cancel.csv');
    expect(text).not.toContain('LAB-SLOW');
  } finally {
    releaseUpload();
    await uploadRouteDone;
    await page.unrouteAll({ behavior: 'ignoreErrors' });
  }
});

test('ADD-01 removed /api/pmi route does not return SPA shell', async ({ page }) => {
  const result = await apiGet(page, '/api/pmi');

  expect(result.status).toBe(404);
  expect(result.text).not.toContain('<!DOCTYPE html>');
});

test('ADD-02 unknown /api route does not return SPA shell', async ({ page }) => {
  const result = await apiGet(page, '/api/not-a-real-endpoint');

  expect(result.status).toBe(404);
  expect(result.text).not.toContain('<!DOCTYPE html>');
});

test('ADD-03 removed /pmi client route lands on app not-found', async ({ page }) => {
  await gotoApp(page, '/pmi');
  await expect(page.getByText('Page not found')).toBeVisible();
});

test('ADD-04 Data Management reload is stable', async ({ page }) => {
  await gotoApp(page, '/data-management');
  await page.reload({ waitUntil: 'domcontentloaded' });
  await waitForApp(page);

  await expect(page.getByText('Series').first()).toBeVisible();
  await expect(page.getByText('STEM Preview')).toBeVisible();
  await expectNoVisibleAppError(page);
});

test('ADD-05 Evaluation reload is stable', async ({ page }) => {
  await gotoApp(page, '/evaluation');
  await page.reload({ waitUntil: 'domcontentloaded' });
  await waitForApp(page);

  await expect(page.getByText('Projects').first()).toBeVisible();
  await expect(page.getByText('Query Builder')).toBeVisible();
  await expectNoVisibleAppError(page);
});

test('ADD-06 sidebar toggle changes width and restores', async ({ page }) => {
  await gotoApp(page, '/data-management');
  const sidebar = page.locator('.rz-sidebar');
  const toggle = page.locator('button[aria-label="Toggle"]').first();
  const before = await sidebar.evaluate(e => e.getBoundingClientRect().width);

  await toggle.click();
  await expect.poll(() => sidebar.evaluate(e => e.getBoundingClientRect().width)).not.toBe(before);
  const mid = await sidebar.evaluate(e => e.getBoundingClientRect().width);
  await toggle.click();
  await expect.poll(() => sidebar.evaluate(e => e.getBoundingClientRect().width)).toBeCloseTo(before, 0);

  expect(mid).toBeLessThan(before / 2);
});

test('ADD-07 Home fits mobile viewport', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await gotoApp(page, '/');
  const state = await page.evaluate(() => ({
    scrollWidth: document.documentElement.scrollWidth,
    clientWidth: document.documentElement.clientWidth
  }));

  expect(state.scrollWidth).toBeLessThanOrEqual(state.clientWidth + 8);
});

test('ADD-08 missing project id routes to not-found', async ({ page }) => {
  await gotoApp(page, '/projects/999999999');
  await page.waitForURL(/\/not-found$/);
  await expect(page.getByText('Page not found')).toBeVisible();
});

test('ADD-09 project tabs load without visible errors', async ({ page }) => {
  await gotoApp(page, '/projects/1');
  for (const label of ['Series', 'Samples', 'SubSamples', 'Apm', 'Particles']) {
    await page.getByRole('tab', { name: label, exact: true }).click();
    await expectNoVisibleAppError(page);
  }

  await page.getByRole('tab', { name: 'Series', exact: true }).click();
  await expect(page.getByText('Series assigned to this project.', { exact: true })).toBeVisible({ timeout: 60_000 });
  const seriesGridMinHeight = await page.locator('.rz-data-grid').first().evaluate(e => getComputedStyle(e).minHeight);
  expect(seriesGridMinHeight === '0px' || seriesGridMinHeight === '0').toBe(true);

  await page.getByRole('tab', { name: 'Apm', exact: true }).click();
  const apmPanel = page.getByRole('tabpanel', { name: 'Apm', exact: true });
  await expect(apmPanel.getByText('APM measurements for this project. Grid filters define the result set used by the chart below.', { exact: true })).toBeVisible({ timeout: 60_000 });
  await expect(apmPanel.getByRole('columnheader', { name: /^U238\b/ })).toBeVisible({ timeout: 60_000 });
  expect(await bodyText(page)).not.toContain('<b>(total');

  await page.getByRole('tab', { name: 'Particles', exact: true }).click();
  const particlesPanel = page.getByRole('tabpanel', { name: 'Particles', exact: true });
  await expect(particlesPanel.getByText('Particle measurements for this project. Grid filters define the result set used by the chart below.', { exact: true })).toBeVisible({ timeout: 60_000 });
  await expect(particlesPanel.getByRole('columnheader', { name: /Particle Id/ })).toBeVisible({ timeout: 60_000 });
  expect(await bodyText(page)).not.toContain('<b>(total');
});

test('ADD-10 Back to Projects returns to Evaluation', async ({ page }) => {
  await gotoApp(page, '/projects/1');
  await page.getByText('Back to Projects', { exact: true }).click();
  await expect(page).toHaveURL(/\/evaluation/);
});

test('ADD-11 blank project name disables save', async ({ page }) => {
  await gotoApp(page, '/projects/1');
  await page.locator('#editProjectNameButton').click();
  await page.locator('#projectNameInput').fill('');
  await expect.poll(() => isDisabled(page.locator('#saveProjectNameButton'))).toBe(true);
});

test('ADD-12 project scoped endpoints return nonzero totals', async ({ page }) => {
  const totals: Record<string, number> = {};
  for (const endpoint of ['series', 'samples', 'subsamples', 'apm', 'particles']) {
    const result = await apiPost<FetchDataResult>(page, `/api/views/${endpoint}`, { projectId: 1, top: 5, skip: 0 });
    expect(result.ok, endpoint).toBe(true);
    totals[endpoint] = result.json?.totalCount ?? 0;
    expect(totals[endpoint], endpoint).toBeGreaterThan(0);
  }
});

test('ADD-13 project chart APIs return arrays', async ({ page }) => {
  const apm = await apiGet<unknown[]>(page, '/api/charts/apm-bin-counts/1');
  const particle = await apiGet<unknown[]>(page, '/api/charts/particle-bin-counts/1');

  expect(apm.ok).toBe(true);
  expect(Array.isArray(apm.json)).toBe(true);
  expect(particle.ok).toBe(true);
  expect(Array.isArray(particle.json)).toBe(true);
});

test('ADD-14 Series sort by id descending works', async ({ page }) => {
  const result = await apiPost<FetchDataResult<{ id: number }>>(page, '/api/views/series', { orderBy: 'Id desc', top: 5, skip: 0 });
  const ids = result.json?.entries.map(entry => entry.id) ?? [];

  expect(result.ok).toBe(true);
  expect(ids).toHaveLength(5);
  expect(ids.every((id, index) => index === 0 || ids[index - 1] >= id)).toBe(true);
});

test('ADD-15 project filter and order combination succeeds', async ({ page }) => {
  const result = await apiPost<FetchDataResult>(page, '/api/views/projects', {
    filter: 'SampleCount > 0',
    orderBy: 'Id desc',
    top: 5,
    skip: 0
  });

  expect(result.ok).toBe(true);
  expect(result.json?.totalCount).toBeGreaterThan(0);
  expect(Array.isArray(result.json?.entries[0]?.projectSeries)).toBe(true);
});

test('ADD-16 preset name availability detects duplicates', async ({ page }) => {
  const name = `QADupe${Date.now().toString().slice(-10)}`;
  const created = await apiPost<number>(page, '/api/preset-filters', { name, entries: [] });
  const id = Number(created.text);
  try {
    expect(created.ok).toBe(true);
    expect(id).toBeGreaterThan(0);
    const availability = await apiGet<boolean>(page, `/api/preset-filters/name-available?name=${encodeURIComponent(name)}&excludeId=0`);
    expect(availability.ok).toBe(true);
    expect(availability.json).toBe(false);
  } finally {
    if (Number.isFinite(id) && id > 0) {
      await apiDelete(page, `/api/preset-filters/${id}`);
    }
  }
});

test('ADD-17 STEM upload endpoint rejects missing file', async ({ page }) => {
  await gotoApp(page, '/data-management');
  const ids = { session: randomUUID(), fileId: randomUUID() };
  const result = await page.evaluate(async ({ session, fileId }) => {
    const form = new FormData();
    form.append('fileId', fileId);
    const response = await fetch(`/api/stem/${session}/files`, { method: 'POST', body: form });
    const text = await response.text();
    return { ok: response.ok, status: response.status, text };
  }, ids);

  expect(result.ok).toBe(false);
  expect(result.status).toBeGreaterThanOrEqual(400);
  expect(result.status).toBeLessThan(500);
});

test('ADD-18 Query Builder switches to Sample grid', async ({ page }) => {
  await openQueryBuilder(page);
  await page.locator('.rz-dropdown').first().click();
  await page.getByText('Sample', { exact: true }).last().click();
  await expect(page.getByText('SAMPLE TYPE')).toBeVisible();
  await expect(page.getByText('SAMPLING DATE')).toBeVisible();
  await expectNoVisibleAppError(page);
});

test('ADD-19 Query Builder Apply with empty filters keeps grid usable', async ({ page }) => {
  await openQueryBuilder(page);
  await page.getByRole('button', { name: /Apply query/i }).click();
  await expect(page.getByRole('row', { name: /\b10000\b/ })).toBeVisible();
  await expectNoVisibleAppError(page);
});

test('ADD-20 Series update API persists SGAS comment', async ({ page }) => {
  const created = await createSeries(page, { sgasComment: `QA update base ${Date.now()}`, workingPaperLink: '/qa/update' });
  const changed = `QA update changed ${Date.now()}`;
  try {
    const updated = await apiPut(page, '/api/series', {
      id: created,
      seriesType: 1,
      createdAt: new Date().toISOString(),
      sgasComment: changed,
      workingPaperLink: '/qa/update',
      isDu: true,
      isNu: false,
      analysisCompleteDate: null,
      sampleExternalCodes: '',
      sampleCount: 0,
      samples: [],
      projectSeries: []
    });
    expect(updated.ok).toBe(true);

    const fetched = await apiPost<FetchDataResult<SeriesView>>(page, '/api/views/series', { filter: `Id == ${created}`, top: 1, skip: 0 });
    expect(fetched.json?.entries[0]?.sgasComment).toBe(changed);
  } finally {
    await apiDelete(page, '/api/series', [created]);
  }
});

test('ADD-21 pending STEM file can be removed before upload', async ({ page }) => {
  await openStemPreview(page);
  await page.locator('input[type="file"]').setInputFiles(stemGood);
  await page.waitForFunction(() => document.body.innerText.includes('stem-good.csv') && document.body.innerText.includes('Not uploaded'));
  await page.locator('.ne-compact-grid button:has-text("delete")').first().click();
  await page.waitForTimeout(1_000);
  const text = await bodyText(page);

  expect(text).not.toContain('stem-good.csv');
  expect(text).not.toContain('LAB-X');
});

test('ADD-22 oversized STEM file is rejected client-side', async ({ page }) => {
  await openStemPreview(page);
  await page.evaluate(() => {
    const input = document.querySelector<HTMLInputElement>('input[type="file"]');
    if (!input) {
      throw new Error('Missing STEM file input');
    }

    const size = 65 * 1024 * 1024 + 1;
    const bytes = new Uint8Array(size);
    bytes[0] = 65;
    bytes[size - 1] = 90;
    const file = new File([bytes], 'synthetic-oversize-65mb.csv', { type: 'text/csv', lastModified: Date.now() });
    const transfer = new DataTransfer();
    transfer.items.add(file);
    input.files = transfer.files;
    input.dispatchEvent(new Event('change', { bubbles: true }));
  });

  await expect(page.getByText('synthetic-oversize-65mb.csv')).toBeVisible();
  await expect(page.getByText('Size exceeds 64 MB')).toBeVisible();
  await expect(page.locator('button:has-text("Upload Files")')).toHaveCount(0);
});

test('ADD-23 preview grid stays hidden before upload', async ({ page }) => {
  await openStemPreview(page);
  await page.locator('input[type="file"]').setInputFiles(stemGood);
  await page.waitForFunction(() => document.body.innerText.includes('stem-good.csv') && document.body.innerText.includes('Not uploaded'));
  const text = await bodyText(page);

  expect(text).not.toContain('LAB-X');
  expect(text).not.toContain('940001');
});

test('ADD-24 deleting last uploaded STEM file hides preview rows', async ({ page }) => {
  await openStemPreview(page);
  await page.locator('input[type="file"]').setInputFiles(stemGood);
  await page.locator('button:has-text("Upload Files")').last().click();
  await page.waitForFunction(() => document.body.innerText.includes('Uploaded') && document.body.innerText.includes('LAB-X'), null, { timeout: 120_000 });
  await page.locator('.ne-compact-grid button:has-text("delete")').first().click();
  await confirmDialogYes(page);
  await page.waitForTimeout(2_500);
  const text = await bodyText(page);

  expect(text).not.toContain('stem-good.csv');
  expect(text).not.toContain('LAB-X');
  expect(text).not.toContain('940001');
});

test('ADD-25 Series create API persists SGAS comment on insert', async ({ page }) => {
  const comment = `QA create comment ${Date.now()}`;
  const created = await createSeries(page, { sgasComment: comment, workingPaperLink: '/qa/create-comment', isNu: true });
  try {
    const fetched = await fetchSeriesById(page, created);
    expect(fetched?.sgasComment).toBe(comment);
  } finally {
    await apiDelete(page, '/api/series', [created]);
  }
});

test('ADD-26 Series update API persists working paper link and DU/NU flags', async ({ page }) => {
  const created = await createSeries(page, { sgasComment: `QA flags base ${Date.now()}`, workingPaperLink: '/qa/flags-base' });
  try {
    const changedComment = `QA flags changed ${Date.now()}`;
    const updated = await updateSeries(page, created, {
      sgasComment: changedComment,
      workingPaperLink: '/qa/flags-updated',
      isDu: true,
      isNu: true
    });
    expect(updated.ok).toBe(true);

    const fetched = await fetchSeriesById(page, created);
    expect(fetched).toMatchObject({
      sgasComment: changedComment,
      workingPaperLink: '/qa/flags-updated',
      isDu: true,
      isNu: true
    });
  } finally {
    await apiDelete(page, '/api/series', [created]);
  }
});

test('ADD-27 Series update API can clear nullable analysis date', async ({ page }) => {
  const created = await createSeries(page, {
    sgasComment: `QA nullable date ${Date.now()}`,
    workingPaperLink: '/qa/date-clear',
    analysisCompleteDate: '2026-05-02T00:00:00.000Z'
  });
  try {
    expect(await fetchSeriesById(page, created)).toHaveProperty('analysisCompleteDate');
    const updated = await updateSeries(page, created, { analysisCompleteDate: null, workingPaperLink: '/qa/date-clear-2' });
    expect(updated.ok).toBe(true);

    const fetched = await fetchSeriesById(page, created);
    expect(fetched?.analysisCompleteDate).toBeNull();
  } finally {
    await apiDelete(page, '/api/series', [created]);
  }
});

test('ADD-28 Series delete endpoint is idempotent for missing ids', async ({ page }) => {
  const deleted = await apiDelete(page, '/api/series', [2_147_483_640]);

  expect(deleted.ok).toBe(true);
});

test('ADD-29 Project name availability respects existing rows and exclude id', async ({ page }) => {
  const project = await apiPost<FetchDataResult<ProjectView>>(page, '/api/views/projects', { filter: 'Id == 1', top: 1, skip: 0 });
  const row = project.json?.entries[0];
  expect(row?.name).toBeTruthy();

  const duplicate = await apiGet<boolean>(page, `/api/projects/name-available?name=${encodeURIComponent(row!.name)}&excludeId=0`);
  const excluded = await apiGet<boolean>(page, `/api/projects/name-available?name=${encodeURIComponent(row!.name)}&excludeId=${row!.id}`);

  expect(duplicate.ok).toBe(true);
  expect(duplicate.json).toBe(false);
  expect(excluded.ok).toBe(true);
  expect(excluded.json).toBe(true);
});

test('ADD-30 Preset name availability respects exclude id', async ({ page }) => {
  const name = `QAExclude${Date.now().toString().slice(-10)}`;
  const created = await apiPost<number>(page, '/api/preset-filters', { name, entries: [] });
  const id = Number(created.text);
  try {
    expect(created.ok).toBe(true);
    const duplicate = await apiGet<boolean>(page, `/api/preset-filters/name-available?name=${encodeURIComponent(name)}&excludeId=0`);
    const excluded = await apiGet<boolean>(page, `/api/preset-filters/name-available?name=${encodeURIComponent(name)}&excludeId=${id}`);

    expect(duplicate.json).toBe(false);
    expect(excluded.json).toBe(true);
  } finally {
    if (Number.isFinite(id) && id > 0) {
      await apiDelete(page, `/api/preset-filters/${id}`);
    }
  }
});

test('ADD-31 Preset update renames an empty preset', async ({ page }) => {
  const name = `QAUpdate${Date.now().toString().slice(-10)}`;
  const updatedName = `${name}B`;
  const created = await apiPost<number>(page, '/api/preset-filters', { name, entries: [] });
  const id = Number(created.text);
  try {
    expect(created.ok).toBe(true);
    const updated = await apiPut(page, '/api/preset-filters', { id, name: updatedName, entries: [] });
    expect(updated.ok).toBe(true);

    const filters = await apiGet<PresetFilter[]>(page, '/api/preset-filters');
    const row = filters.json?.find(filter => filter.id === id);
    expect(row?.name).toBe(updatedName);
    expect(row?.entries).toEqual([]);
  } finally {
    if (Number.isFinite(id) && id > 0) {
      await apiDelete(page, `/api/preset-filters/${id}`);
    }
  }
});

test('ADD-32 Preset delete removes row from dropdown payload', async ({ page }) => {
  const name = `QADelete${Date.now().toString().slice(-10)}`;
  const created = await apiPost<number>(page, '/api/preset-filters', { name, entries: [] });
  const id = Number(created.text);

  expect(created.ok).toBe(true);
  const deleted = await apiDelete(page, `/api/preset-filters/${id}`);
  const filters = await apiGet<PresetFilter[]>(page, '/api/preset-filters');

  expect(deleted.ok).toBe(true);
  expect(filters.json?.some(filter => filter.id === id)).toBe(false);
});

test('ADD-33 Series priority ids are ordered before the normal sort', async ({ page }) => {
  const highest = await apiPost<FetchDataResult<SeriesView>>(page, '/api/views/series', { orderBy: 'Id desc', top: 1, skip: 0 });
  const lowest = await apiPost<FetchDataResult<SeriesView>>(page, '/api/views/series', { orderBy: 'Id asc', top: 1, skip: 0 });
  const highestId = highest.json?.entries[0]?.id;
  const priorityId = lowest.json?.entries[0]?.id;
  expect(highestId).toBeGreaterThan(priorityId!);

  const prioritized = await apiPost<FetchDataResult<SeriesView>>(page, '/api/views/series', {
    priorityIds: [priorityId],
    orderBy: 'Id desc',
    top: 1,
    skip: 0
  });

  expect(prioritized.ok).toBe(true);
  expect(prioritized.json?.entries[0]?.id).toBe(priorityId);
});

test('ADD-34 Series counts respect impossible filters', async ({ page }) => {
  const counts = await apiPost<SeriesCountsView>(page, '/api/views/series-counts', { filter: 'Id == -2147483648', top: 5, skip: 0 });

  expect(counts.ok).toBe(true);
  expect(counts.json).toMatchObject({ seriesCount: 0, sampleCount: 0, subSampleCount: 0, apmCount: 0, particleCount: 0 });
});

test('ADD-35 Series enum option endpoint returns series type values', async ({ page }) => {
  const options = await apiPost<number[]>(page, '/api/views/series/enum-options', {
    propertyName: 'SeriesType',
    query: { top: 20, skip: 0 }
  });

  expect(options.ok).toBe(true);
  expect(options.json?.length).toBeGreaterThan(0);
  expect(options.json?.every(value => Number.isInteger(value))).toBe(true);
});

test('ADD-36 Sample enum option endpoint returns sample type values', async ({ page }) => {
  const options = await apiPost<number[]>(page, '/api/views/samples/enum-options', {
    propertyName: 'SampleType',
    query: { top: 20, skip: 0 }
  });

  expect(options.ok).toBe(true);
  expect(options.json?.length).toBeGreaterThan(0);
  expect(options.json?.every(value => Number.isInteger(value))).toBe(true);
});

test('ADD-37 STEM entries endpoint without a session returns an empty result', async ({ page }) => {
  const result = await apiPost<FetchDataResult>(page, '/api/views/stem-entries', { top: 5, skip: 0 });

  expect(result.ok).toBe(true);
  expect(result.json?.totalCount).toBe(0);
  expect(result.json?.entries).toEqual([]);
});

test('ADD-38 STEM entries endpoint with an unknown session returns an empty result', async ({ page }) => {
  const result = await apiPost<FetchDataResult>(page, '/api/views/stem-entries', { stemSessionId: randomUUID(), top: 5, skip: 0 });

  expect(result.ok).toBe(true);
  expect(result.json?.totalCount).toBe(0);
  expect(result.json?.entries).toEqual([]);
});

test('ADD-39 Chart APIs return empty arrays for missing project ids', async ({ page }) => {
  const apm = await apiGet<ChartBinCounts[]>(page, '/api/charts/apm-bin-counts/999999999');
  const particle = await apiGet<ChartBinCounts[]>(page, '/api/charts/particle-bin-counts/999999999');

  expect(apm.ok).toBe(true);
  expect(apm.json).toEqual([]);
  expect(particle.ok).toBe(true);
  expect(particle.json).toEqual([]);
});

test('ADD-40 Project detail fits a mobile viewport', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await gotoApp(page, '/projects/1');
  await expect(page.getByText('Back to Projects', { exact: true })).toBeVisible({ timeout: 30_000 });
  const state = await page.evaluate(() => ({
    scrollWidth: document.documentElement.scrollWidth,
    clientWidth: document.documentElement.clientWidth
  }));

  expect(state.scrollWidth).toBeLessThanOrEqual(state.clientWidth + 8);
  await expectNoVisibleAppError(page);
});

test('ADD-41 query-aware chart APIs respect grid filters', async ({ page }) => {
  const query = { projectId: 1, filter: 'Id == -2147483648', top: 5, skip: 0 };
  const apm = await apiPost<ChartBinCounts[]>(page, '/api/charts/apm-bin-counts', query);
  const particle = await apiPost<ChartBinCounts[]>(page, '/api/charts/particle-bin-counts', query);

  expect(apm.ok).toBe(true);
  expect(apm.json).toEqual([]);
  expect(particle.ok).toBe(true);
  expect(particle.json).toEqual([]);
});

test('ADD-42 chart APIs accept Radzen nullable comparison filters', async ({ page }) => {
  const query = { projectId: 1, filter: 'x => ((x.U234 ?? null) > 1)' };
  const apm = await apiPost<ChartBinCounts[]>(page, '/api/charts/apm-bin-counts', query);
  const particle = await apiPost<ChartBinCounts[]>(page, '/api/charts/particle-bin-counts', query);

  expect(apm.ok).toBe(true);
  expect(Array.isArray(apm.json)).toBe(true);
  expect(particle.ok).toBe(true);
  expect(Array.isArray(particle.json)).toBe(true);
});

test('ADD-43 Preset create accepts enabled entries with null navigation payloads', async ({ page }) => {
  const name = `QAPresetEntry${Date.now().toString().slice(-10)}`;
  const created = await apiPost<number>(page, '/api/preset-filters', {
    id: 0,
    name,
    entries: [
      {
        id: 0,
        presetFilterEntryType: 2,
        logicalFilterOperator: 0,
        isEnabled: true,
        presetFilterId: 0,
        presetFilter: null,
        serializedDescriptors: '[{"Property":"Sample.Sequence","Type":"System.String","FilterValue":"b","FilterOperator":6,"LogicalFilterOperator":0,"Filters":null}]'
      }
    ]
  });
  const id = Number(created.text);

  try {
    expect(created.ok).toBe(true);
    expect(id).toBeGreaterThan(0);

    const filters = await apiGet<PresetFilter[]>(page, '/api/preset-filters');
    const row = filters.json?.find(filter => filter.id === id);

    expect(row?.entries).toHaveLength(1);
    expect(row?.entries[0]).toMatchObject({
      presetFilterEntryType: 2,
      isEnabled: true,
      serializedDescriptors: expect.stringContaining('Sample.Sequence')
    });
  } finally {
    if (Number.isFinite(id) && id > 0) {
      await apiDelete(page, `/api/preset-filters/${id}`);
    }
  }
});
async function themeState(page: Page) {
  return page.evaluate(() => ({
    href: [...document.querySelectorAll<HTMLLinkElement>('link[rel="stylesheet"]')]
      .map(link => link.href)
      .filter(href => /software.*\.css/i.test(href)),
    background: getComputedStyle(document.body).backgroundColor
  }));
}

async function isDisabled(locator: ReturnType<Page['locator']>) {
  return locator.evaluate(element => {
    const button = element as HTMLButtonElement;
    return button.disabled || button.getAttribute('aria-disabled') === 'true';
  });
}

async function createSeries(page: Page, overrides: Partial<SeriesView> = {}) {
  const result = await apiPost<number>(page, '/api/series', seriesPayload(overrides));
  expect(result.ok).toBe(true);
  const id = Number(result.text);
  expect(id).toBeGreaterThan(0);
  return id;
}

async function updateSeries(page: Page, id: number, overrides: Partial<SeriesView> = {}) {
  const current = await fetchSeriesById(page, id);
  expect(current).toBeTruthy();
  return apiPut(page, '/api/series', seriesPayload({ ...current!, ...overrides, id }));
}

async function fetchSeriesById(page: Page, id: number) {
  const fetched = await apiPost<FetchDataResult<SeriesView>>(page, '/api/views/series', { filter: `Id == ${id}`, top: 1, skip: 0 });
  expect(fetched.ok).toBe(true);
  return fetched.json?.entries[0] ?? null;
}

function seriesPayload(overrides: Partial<SeriesView> = {}) {
  return {
    id: overrides.id ?? 0,
    seriesType: overrides.seriesType ?? 1,
    createdAt: overrides.createdAt ?? new Date().toISOString(),
    sgasComment: overrides.sgasComment ?? '',
    workingPaperLink: overrides.workingPaperLink ?? '/qa/temp',
    isDu: overrides.isDu ?? false,
    isNu: overrides.isNu ?? false,
    analysisCompleteDate: overrides.analysisCompleteDate ?? null,
    sampleExternalCodes: '',
    sampleCount: overrides.sampleCount ?? 0,
    samples: [],
    projectSeries: []
  };
}


function writeLargeStemCsv(filePath: string, targetBytes: number) {
  const fd = openSync(filePath, 'w');
  try {
    let size = writeSync(fd, 'Identifier,LaboratoryCode,AnalysisDate,IsNu,U234,ErU234,U235,ErU235\n');
    const line = '930001,LAB-SLOW,2026-04-01,true,1.1,0.1,2.2,0.2\n';
    while (size < targetBytes) {
      size += writeSync(fd, line);
    }
  } finally {
    closeSync(fd);
  }

  expect(statSync(filePath).size).toBeGreaterThan(targetBytes - 1);
}
