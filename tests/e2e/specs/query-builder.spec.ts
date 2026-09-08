import { expect, test } from 'playwright/test';
import { expectNoVisibleAppError, openQueryBuilder } from '../support/app';

test('QB-01 filter edits wait for Apply query and enum conditions execute', async ({ page }) => {
  const requests: string[] = [];
  page.on('request', request => {
    if (request.method() === 'POST' && request.url().endsWith('/api/views/series')) {
      requests.push(request.postData() ?? '');
    }
  });
  await openQueryBuilder(page);
  await expect(page.getByRole('gridcell', { name: '10000', exact: true })).toBeVisible();
  const initialRequests = requests.length;
  const sample = page.getByRole('checkbox').nth(1);
  const filter = page.locator('.rz-datafilter');

  await sample.click();
  await expect(filter).toBeVisible();
  await sample.click();
  await expect(filter).toBeHidden();
  await sample.click();
  await filter.getByRole('button', { name: 'Button', exact: true }).click();
  await filter.locator('.rz-datafilter-property').first().click();
  await page.getByRole('option', { name: 'External Code', exact: true }).click();
  await filter.locator('.rz-datafilter-operator').first().click();
  await page.getByRole('option', { name: 'Equals', exact: true }).click();
  await filter.getByRole('textbox').fill('001');
  await filter.getByRole('button', { name: 'Button', exact: true }).click();
  await filter.locator('.rz-datafilter-property').nth(1).click();
  await page.getByRole('option', { name: 'Sample Type', exact: true }).click();
  const enumRow = filter.locator('li.rz-datafilter-item').filter({ has: page.locator('.rz-datafilter-property[aria-label="Sample Type"]') });
  await enumRow.getByRole('combobox').last().click();
  await page.getByRole('option', { name: 'Qc', exact: true }).click();
  await filter.getByRole('radio', { name: 'Or', exact: true }).click();

  expect(requests).toHaveLength(initialRequests);
  await expect(page.getByRole('gridcell', { name: '10000', exact: true })).toBeVisible();
  const responsePromise = page.waitForResponse(response => response.url().endsWith('/api/views/series') && response.request().method() === 'POST');
  await page.getByRole('button', { name: 'Apply query', exact: false }).click();
  const response = await responsePromise;
  const result = await response.json();
  expect(response.ok()).toBe(true);
  expect(result.isSuccessful).toBe(true);
  expect(result.totalCount).toBe(100_000);
  expect(result.entries.length).toBeGreaterThan(0);
  expect(requests.at(-1)).toContain('SampleType');
  await expectNoVisibleAppError(page);

  const appliedRequests = requests.length;
  await filter.getByRole('radio', { name: 'And', exact: true }).click();
  expect(requests).toHaveLength(appliedRequests);
  const narrowedPromise = page.waitForResponse(response => response.url().endsWith('/api/views/series') && response.request().method() === 'POST');
  await page.getByRole('button', { name: 'Apply query', exact: false }).click();
  const narrowed = await (await narrowedPromise).json();
  expect(narrowed.isSuccessful).toBe(true);
  expect(narrowed.totalCount).toBeGreaterThan(0);
  expect(narrowed.totalCount).toBeLessThan(result.totalCount);
  await expectNoVisibleAppError(page);
});
