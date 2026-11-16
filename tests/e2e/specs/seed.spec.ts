import { expect, test } from '@playwright/test';
import { expectNoVisibleAppError, gotoApp } from '../support/app';

test.describe('Agent environment', () => {
  test('seed opens a ready application', { tag: ['@agent', '@smoke'] }, async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 1000 });
    await gotoApp(page, '/');

    await expect(page.getByText('Nuclear Evaluation').first()).toBeVisible();
    const navigation = page.getByRole('navigation', { name: 'Primary' });
    await expect(navigation.getByRole('link', { name: 'Data Management' })).toBeVisible();
    await expect(navigation.getByRole('link', { name: 'Evaluation', exact: true })).toBeVisible();
    await expectNoVisibleAppError(page);
  });
});
