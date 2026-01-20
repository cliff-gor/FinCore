import { render, screen } from '@testing-library/react';
import { createMemoryHistory } from '@tanstack/react-router';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { Providers } from '../app/providers.tsx';
import { createQueryClient } from '../app/queryClient.ts';
import { createAppRouter } from '../app/router.tsx';
import { server } from './msw/server.ts';

function renderApp(path = '/') {
  const queryClient = createQueryClient();
  const router = createAppRouter(queryClient, createMemoryHistory({ initialEntries: [path] }));
  render(<Providers queryClient={queryClient} router={router} />);
}

describe('home', () => {
  it('shows the signed-out state when /bff/user returns 401', async () => {
    renderApp();

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Welcome to FinCore' }),
    ).toBeDefined();
    expect((await screen.findByTestId('signed-out')).textContent).toContain('You are signed out.');
    expect(screen.getByRole('button', { name: 'Sign in' })).toBeDefined();
    expect(screen.getByTestId('app-footer').textContent).toMatch(/^v0\.1\.0 · \S+ · Development$/);
  });

  it('shows the signed-in user when the BFF returns a session', async () => {
    server.use(
      http.get('*/bff/user', () =>
        HttpResponse.json({ sub: '1', name: 'Amina', permissions: ['org.read'] }),
      ),
    );
    renderApp();

    expect(await screen.findByText('Signed in as Amina')).toBeDefined();
  });

  it('sends X-CSRF and X-Client-Version on BFF calls', async () => {
    const seen: Headers[] = [];
    server.use(
      http.get('*/bff/user', ({ request }) => {
        seen.push(request.headers);
        return HttpResponse.json({ status: 401 }, { status: 401 });
      }),
    );
    renderApp();

    await screen.findByTestId('signed-out');
    expect(seen[0]?.get('X-CSRF')).toBe('1');
    expect(seen[0]?.get('X-Client-Version')).toBe('backoffice/0.1.0');
  });
});
