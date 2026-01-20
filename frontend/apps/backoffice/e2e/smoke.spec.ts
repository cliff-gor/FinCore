import { expect, test } from '@playwright/test';

test.beforeEach(async ({ page }) => {
  // M0 BFF answers 401 for /bff/user everywhere (spec 001).
  await page.route('**/bff/user', (route) =>
    route.fulfill({
      status: 401,
      contentType: 'application/problem+json',
      body: JSON.stringify({ status: 401, title: 'Unauthorized' }),
    }),
  );
});

test('home renders the signed-out state and the version footer', async ({ page }) => {
  await page.goto('/');

  await expect(page.getByRole('heading', { level: 1, name: 'Welcome to FinCore' })).toBeVisible();
  await expect(page.getByTestId('signed-out')).toContainText('You are signed out.');
  await expect(page.getByRole('button', { name: 'Sign in' })).toBeVisible();
  await expect(page.getByTestId('app-footer')).toHaveText(/^v0\.1\.0 · \S+ · \S+$/);

  const version = await page.request.get('/version.json');
  expect(version.ok()).toBe(true);
  expect(await version.json()).toMatchObject({ app: 'backoffice', version: '0.1.0' });
});
