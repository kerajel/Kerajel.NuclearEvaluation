import { expect, test } from '@playwright/test';
import { expectNoVisibleAppError, gotoApp } from '../support/app';

test.describe('Agent environment', () => {
  test('seed opens a ready application', { tag: ['@agent', '@smoke'] }, async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 1000 });
    await gotoApp(page, '/');

    await expect(page.getByText('Nuclear Evaluation').first()).toBeVisible();
    await expect(page.getByText('Data Management', { exact: true })).toBeVisible();
    await expect(page.getByText('Evaluation', { exact: true })).toBeVisible();
    await expectNoVisibleAppError(page);
  });
});
