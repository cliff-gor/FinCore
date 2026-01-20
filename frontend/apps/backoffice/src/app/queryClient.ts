import { isApiProblem } from '@fincore/api-client';
import { QueryClient } from '@tanstack/react-query';

/** One client per app (and per test), so cached data never leaks between test cases. */
export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 30_000,
        // Freshness comes from invalidating query keys after mutations (spec §30.4), not from tab switches.
        refetchOnWindowFocus: false,
        // Client errors (4xx) are not retried; transient failures get two retries.
        retry: (failureCount, error) =>
          !(isApiProblem(error) && error.status >= 400 && error.status < 500) && failureCount < 2,
      },
    },
  });
}
