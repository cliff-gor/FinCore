import { isApiProblem, request } from '@fincore/api-client/http';
import { useQuery } from '@tanstack/react-query';

/** Claims returned by `GET /bff/user` (master spec §30.3). M0's BFF always answers 401. */
export interface BffUser {
  sub: string;
  name: string;
  permissions: string[];
  institutionId?: string;
  branchId?: string;
  businessDate?: string;
}

export type Session =
  | { status: 'loading'; user: null }
  | { status: 'signed-out'; user: null }
  | { status: 'signed-in'; user: BffUser }
  | { status: 'error'; user: null; error: unknown };

export const sessionQueryKey = ['bff', 'user'] as const;

/** Fetches the BFF session; a 401 means "signed out", not an error. */
export async function fetchSession(): Promise<BffUser | null> {
  try {
    return await request<BffUser>('/bff/user');
  } catch (error) {
    if (isApiProblem(error) && error.status === 401) return null;
    throw error;
  }
}

export function useSession(): Session {
  const query = useQuery({
    queryKey: sessionQueryKey,
    queryFn: fetchSession,
    staleTime: 60_000,
    retry: false,
  });

  if (query.data === undefined) {
    if (query.isError) return { status: 'error', user: null, error: query.error };
    return { status: 'loading', user: null };
  }
  return query.data === null
    ? { status: 'signed-out', user: null }
    : { status: 'signed-in', user: query.data };
}

/** Builds the BFF login URL. Only local paths are accepted as return URLs (open-redirect safe). */
export function loginUrl(returnUrl: string): string {
  const safe = returnUrl.startsWith('/') && !returnUrl.startsWith('//') ? returnUrl : '/';
  return `/bff/login?returnUrl=${encodeURIComponent(safe)}`;
}

export function signIn(returnUrl?: string): void {
  const current = `${window.location.pathname}${window.location.search}${window.location.hash}`;
  window.location.assign(loginUrl(returnUrl ?? current));
}
