import { isApiProblem } from '@fincore/api-client';
import { QueryClient } from '@tanstack/react-query';

export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 30_000,
        refetchOnWindowFocus: false,
        // Client errors (4xx) are not retried; transient failures get two retries.
        retry: (failureCount, error) =>
          !(isApiProblem(error) && error.status >= 400 && error.status < 500) && failureCount < 2,
      },
    },
  });
}
