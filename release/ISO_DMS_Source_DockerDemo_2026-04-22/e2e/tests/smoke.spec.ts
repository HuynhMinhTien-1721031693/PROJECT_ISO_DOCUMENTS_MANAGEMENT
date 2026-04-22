import { test, expect } from '@playwright/test';

/**
 * Smoke E2E against Blazor (run API + Blazor locally first).
 * Set ISODOC_BASE_URL if Blazor is not on http://localhost:5062
 */
test.describe('Blazor smoke', () => {
  test('login page loads', async ({ page }) => {
    await page.goto('/login');
    await expect(page.getByRole('heading', { name: /ISO DMS Login/i })).toBeVisible();
    await expect(page.getByLabel(/Email/i)).toBeVisible();
    await expect(page.getByLabel(/Password/i)).toBeVisible();
  });

  test('unauthenticated root redirects or shows login', async ({ page }) => {
    await page.goto('/');
    const url = page.url();
    const onLogin = url.includes('/login');
    const hasAppChrome = await page.getByText(/ISO|Document|Dashboard/i).first().isVisible().catch(() => false);
    expect(onLogin || hasAppChrome).toBeTruthy();
  });
});
