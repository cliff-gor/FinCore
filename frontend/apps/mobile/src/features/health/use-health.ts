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

export type HealthState = 'checking' | 'healthy' | 'unhealthy' | 'update-required';

export function toHealthState(query: ReturnType<typeof useHealth>): HealthState {
  if (query.isSuccess) return 'healthy';
  if (query.isError) {
    return isApiProblem(query.error) && query.error.status === 426
      ? 'update-required'
      : 'unhealthy';
  }
  return 'checking';
}
