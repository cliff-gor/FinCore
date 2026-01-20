import { AppFooter } from '@fincore/ui';
import type { QueryClient } from '@tanstack/react-query';
import { createRootRouteWithContext, Outlet } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { buildInfo } from '../app/build-info.ts';

export interface RouterContext {
  queryClient: QueryClient;
}

export const Route = createRootRouteWithContext<RouterContext>()({
  component: RootLayout,
});

function RootLayout() {
  const { t } = useTranslation();
  return (
    <div className="flex min-h-screen flex-col bg-bg text-text">
      <header className="border-b border-border bg-surface-sunken px-6 py-4">
        <span className="font-display text-h2">{t('app.backofficeName')}</span>
      </header>
      <main className="mx-auto w-full max-w-[1440px] flex-1 px-6 py-8">
        <Outlet />
      </main>
      <AppFooter version={buildInfo.version} sha={buildInfo.sha} env={buildInfo.env} />
    </div>
  );
}
