import { expect, test } from '@playwright/test';

test('home renders the signed-out state and the version footer', async ({ page }) => {
  // The BFF is stubbed: signed out, /bff/user answers 401 (spec 002).
  await page.route('**/bff/user', (route) =>
    route.fulfill({
      status: 401,
      contentType: 'application/problem+json',
      body: JSON.stringify({ status: 401, title: 'Unauthorized', code: 'Bff.NotSignedIn' }),
    }),
  );
  await page.goto('/');

  await expect(page.getByRole('heading', { level: 1, name: 'Welcome to FinCore' })).toBeVisible();
  await expect(page.getByTestId('signed-out')).toContainText('You are signed out.');
  await expect(page.getByRole('button', { name: 'Sign in' })).toBeVisible();
  await expect(page.getByTestId('app-footer')).toHaveText(/^v0\.1\.0 · \S+ · \S+$/);

  const version = await page.request.get('/version.json');
  expect(version.ok()).toBe(true);
  expect(await version.json()).toMatchObject({ app: 'portal', version: '0.1.0' });
});

test('home shows the signed-in customer and signs out through the BFF', async ({ page }) => {
  await page.route('**/bff/user', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        id: 'customer-1',
        kind: 'customer',
        name: 'Amina',
        email: 'customer1@example.test',
      }),
    }),
  );
  let csrf: string | undefined;
  await page.route('**/bff/logout', (route) => {
    csrf = route.request().headers()['x-csrf'];
    // Back to the app root, standing in for the identity provider's end-session redirect.
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ logoutUrl: '/?signed-out' }),
    });
  });
  await page.goto('/');

  await expect(page.getByTestId('signed-in')).toContainText('Signed in as Amina (customer)');
  await page.getByRole('button', { name: 'Sign out' }).click();

  await expect(page).toHaveURL(/\?signed-out$/);
  expect(csrf).toBe('1');
});

test('home explains when FinCore refuses the signed-in account', async ({ page }) => {
  await page.route('**/bff/user', (route) =>
    route.fulfill({
      status: 403,
      contentType: 'application/problem+json',
      body: JSON.stringify({
        status: 403,
        title: 'Forbidden',
        code: 'Identity.StaffNotRegistered',
      }),
    }),
  );
  await page.goto('/');

  await expect(page.getByTestId('access-refused')).toContainText('contact your administrator');
});
