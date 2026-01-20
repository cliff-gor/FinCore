import { isApiProblem, request } from '@fincore/api-client/http';
import { useQuery } from '@tanstack/react-query';

// TODO(M1): M0's BFF always answers 401; real claims arrive with the OIDC sign-in.
/**
 * Claims returned by `GET /bff/user` (master spec §30.3). `businessDate` is the institution's business date, which
 * date pickers default to instead of the browser clock (spec §30.4).
 */
export interface BffUser {
  sub: string;
  name: string;
  permissions: string[];
  institutionId?: string;
  branchId?: string;
  businessDate?: string;
}

/** Session state as a discriminated union, so `user` is only reachable once `status` is `signed-in`. */
export type Session =
  | { status: 'loading'; user: null }
  | { status: 'signed-out'; user: null }
  | { status: 'signed-in'; user: BffUser }
  | { status: 'error'; user: null; error: unknown };

/** TanStack Query key for the session; invalidate it after sign-in or sign-out. */
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

/** Current BFF session, shared through the query cache by every component that asks. */
export function useSession(): Session {
  const query = useQuery({
    queryKey: sessionQueryKey,
    queryFn: fetchSession,
    staleTime: 60_000,
    // A failure is reported straight away as "session unavailable" instead of retrying behind a spinner.
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
  // `//evil.example` is protocol-relative, i.e. another origin, so it is rejected along with absolute URLs.
  const safe = returnUrl.startsWith('/') && !returnUrl.startsWith('//') ? returnUrl : '/';
  return `/bff/login?returnUrl=${encodeURIComponent(safe)}`;
}

/** Full-page navigation to the BFF login (OIDC runs server-side); returns to the current page by default. */
export function signIn(returnUrl?: string): void {
  const current = `${window.location.pathname}${window.location.search}${window.location.hash}`;
  window.location.assign(loginUrl(returnUrl ?? current));
}
