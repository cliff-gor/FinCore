import { createMemoryHistory } from '@tanstack/react-router';
import { http, HttpResponse } from 'msw';
import { navigation } from '@fincore/auth';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
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

  it('shows the signed-in user and kind when the BFF returns a session', async () => {
    server.use(
      http.get('*/bff/user', () =>
        HttpResponse.json({
          id: 'staff-1',
          kind: 'staff',
          name: 'Amina',
          email: 'admin1.fcb@fincore.local',
          institution: { id: 'i-1', code: 'FCB', name: 'FinCore Bank', status: 'Active' },
          homeBranch: { id: 'b-1', code: '001', name: 'Head Office' },
          roles: ['SystemAdmin'],
          permissions: ['org.read'],
          allBranches: true,
          businessDate: '2026-10-14',
          enabledModules: ['organization', 'identity'],
        }),
      ),
    );
    renderApp();

    expect(await screen.findByText('Signed in as Amina (staff)')).toBeDefined();
    expect(screen.getByRole('button', { name: 'Sign out' })).toBeDefined();
  });

  it('signs out through the BFF with the CSRF header and goes to the logout URL', async () => {
    const assign = vi.spyOn(navigation, 'assign').mockImplementation(() => undefined);
    const logoutHeaders: Headers[] = [];
    server.use(
      http.get('*/bff/user', () =>
        HttpResponse.json({
          id: 'staff-1',
          kind: 'staff',
          name: 'Amina',
          email: 'admin1.fcb@fincore.local',
          institution: { id: 'i-1', code: 'FCB', name: 'FinCore Bank', status: 'Active' },
          homeBranch: { id: 'b-1', code: '001', name: 'Head Office' },
          roles: ['SystemAdmin'],
          permissions: ['org.read'],
          allBranches: true,
          businessDate: '2026-10-14',
          enabledModules: ['organization', 'identity'],
        }),
      ),
      http.post('*/bff/logout', ({ request }) => {
        logoutHeaders.push(request.headers);
        return HttpResponse.json({ logoutUrl: 'https://idp.test/logout?id_token_hint=x' });
      }),
    );
    renderApp();

    fireEvent.click(await screen.findByRole('button', { name: 'Sign out' }));

    await waitFor(() =>
      expect(assign).toHaveBeenCalledWith('https://idp.test/logout?id_token_hint=x'),
    );
    expect(logoutHeaders[0]?.get('X-CSRF')).toBe('1');
    assign.mockRestore();
  });

  it('explains a refused sign-in instead of offering to sign in again', async () => {
    server.use(
      http.get('*/bff/user', () =>
        HttpResponse.json(
          { status: 403, title: 'Forbidden', code: 'Identity.StaffNotRegistered' },
          { status: 403, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );
    renderApp();

    expect((await screen.findByTestId('access-refused')).textContent).toContain(
      'not registered in FinCore',
    );
    expect(screen.getByRole('button', { name: 'Sign out' })).toBeDefined();
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
