import { isApiProblem, request } from '@fincore/api-client';
import { useQuery } from '@tanstack/react-query';

export const healthQueryKey = ['health', 'live'] as const;

/** `GET /health/live` through the Mobile Gateway (anonymous). */
export function useHealth() {
  return useQuery({
    queryKey: healthQueryKey,
    queryFn: ({ signal }) => request<unknown>('/health/live', { signal }),
  });
}

/** What the health card shows. */
export type HealthState = 'checking' | 'healthy' | 'unhealthy' | 'update-required';

export function toHealthState(query: ReturnType<typeof useHealth>): HealthState {
  if (query.isSuccess) return 'healthy';
  if (query.isError) {
    // 426 means the gateway rejected this app version as below the minimum (spec §31.7): a blocking
    // update prompt, not an outage.
    return isApiProblem(query.error) && query.error.status === 426
      ? 'update-required'
      : 'unhealthy';
  }
  return 'checking';
}
