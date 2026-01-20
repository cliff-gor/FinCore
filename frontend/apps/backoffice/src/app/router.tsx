import type { QueryClient } from '@tanstack/react-query';
import { createRouter, type RouterHistory } from '@tanstack/react-router';
import { routeTree } from '../routeTree.gen.ts';

export function createAppRouter(queryClient: QueryClient, history?: RouterHistory) {
  return createRouter({
    routeTree,
    context: { queryClient },
    defaultPreload: 'intent',
    scrollRestoration: true,
    // Tests inject a memory history; the browser history is the default otherwise.
    ...(history !== undefined ? { history } : {}),
  });
}

export type AppRouter = ReturnType<typeof createAppRouter>;

// Registers the router type so `Link`, `useNavigate` and search params are type-checked against the route tree.
declare module '@tanstack/react-router' {
  interface Register {
    router: AppRouter;
  }
}
