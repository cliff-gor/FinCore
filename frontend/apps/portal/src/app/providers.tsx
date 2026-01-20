import { QueryClientProvider, type QueryClient } from '@tanstack/react-query';
import { RouterProvider } from '@tanstack/react-router';
import { I18nextProvider } from 'react-i18next';
import { i18n } from './i18n.ts';
import type { AppRouter } from './router.tsx';

export interface ProvidersProps {
  queryClient: QueryClient;
  router: AppRouter;
}

/** App-wide providers: TanStack Query, i18n and the router (theme/toaster arrive with real screens). */
export function Providers({ queryClient, router }: ProvidersProps) {
  return (
    <QueryClientProvider client={queryClient}>
      <I18nextProvider i18n={i18n}>
        <RouterProvider router={router} />
      </I18nextProvider>
    </QueryClientProvider>
  );
}
