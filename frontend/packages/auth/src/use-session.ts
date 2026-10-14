import { isApiProblem, request } from '@fincore/api-client/http';
import { useQuery } from '@tanstack/react-query';

/** The signed-in staff member's institution (`null` for a PlatformAdmin, who belongs to none). */
export interface SessionInstitution {
  id: string;
  code: string;
  name: string;
  status: string;
}

/** The staff member's home branch (`null` for a PlatformAdmin). */
export interface SessionBranch {
  id: string;
  code: string;
  name: string;
}

/**
 * Staff shape of `GET /bff/user` (= the API's `/me`, spec 002 "API Contract"). `businessDate` is the institution's
 * business date, which date pickers default to instead of the browser clock (spec §30.4).
 */
export interface StaffUser {
  id: string;
  kind: 'staff';
  name: string;
  email: string;
  institution: SessionInstitution | null;
  homeBranch: SessionBranch | null;
  roles: string[];
  permissions: string[];
  allBranches: boolean;
  businessDate: string | null;
  enabledModules: string[];
}

/** Customer shape of `GET /bff/user` in M1: identity only; customer records arrive with M2 (spec 002). */
export interface CustomerUser {
  id: string;
  kind: 'customer';
  name: string;
  email: string;
}

/** `GET /bff/user`, discriminated by `kind`. */
export type BffUser = StaffUser | CustomerUser;

/** 403 codes meaning "signed in at the identity provider, but FinCore will not let you in" (spec 002 Edge Cases). */
export type NotRegisteredCode = 'Identity.StaffNotRegistered' | 'Identity.EmailNotVerified';
/** 403 code for a deactivated staff user or a closed institution. */
export type InactiveCode = 'Identity.StaffInactive';

/**
 * Session state as a discriminated union, so `user` is only reachable once `status` is `signed-in`.
 * `not-registered` and `inactive` carry the API's code so the screen can say what the user should do.
 */
export type Session =
  | { status: 'loading'; user: null }
  | { status: 'signed-out'; user: null }
  | { status: 'signed-in'; user: BffUser }
  | { status: 'not-registered'; user: null; code: NotRegisteredCode }
  | { status: 'inactive'; user: null; code: InactiveCode }
  | { status: 'error'; user: null; error: unknown };

/** What `fetchSession` resolved to; 401 and the known 403 codes are answers, not errors. */
export type SessionResult =
  | { kind: 'user'; user: BffUser }
  | { kind: 'signed-out' }
  | { kind: 'refused'; code: NotRegisteredCode | InactiveCode };

/** TanStack Query key for the session; invalidate it after sign-in or sign-out. */
export const sessionQueryKey = ['bff', 'user'] as const;

const notRegisteredCodes: readonly string[] = [
  'Identity.StaffNotRegistered',
  'Identity.EmailNotVerified',
] satisfies NotRegisteredCode[];
const inactiveCode: InactiveCode = 'Identity.StaffInactive';

function isRefusalCode(code: string | undefined): code is NotRegisteredCode | InactiveCode {
  return code !== undefined && (notRegisteredCodes.includes(code) || code === inactiveCode);
}

/** Fetches the BFF session. 401 means signed out; a 403 with an Identity code means FinCore refused the user. */
export async function fetchSession(): Promise<SessionResult> {
  try {
    return { kind: 'user', user: await request<BffUser>('/bff/user') };
  } catch (error) {
    if (isApiProblem(error) && error.status === 401) return { kind: 'signed-out' };
    if (isApiProblem(error) && error.status === 403 && isRefusalCode(error.code)) {
      return { kind: 'refused', code: error.code };
    }
    throw error;
  }
}

/** Maps a fetched session result onto the `Session` state the UI renders. */
export function toSession(result: SessionResult): Session {
  switch (result.kind) {
    case 'user':
      return { status: 'signed-in', user: result.user };
    case 'signed-out':
      return { status: 'signed-out', user: null };
    case 'refused':
      return result.code === inactiveCode
        ? { status: 'inactive', user: null, code: result.code }
        : { status: 'not-registered', user: null, code: result.code };
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
  return toSession(query.data);
}

/**
 * Full-page navigation used by `signIn` and `signOut`. An object (not a bare function) so tests can replace
 * `assign` with a spy; jsdom cannot navigate.
 */
export const navigation = {
  assign(url: string): void {
    window.location.assign(url);
  },
};

/** Builds the BFF login URL. Only local paths are accepted as return URLs (open-redirect safe). */
export function loginUrl(returnUrl: string): string {
  // `//evil.example` is protocol-relative, i.e. another origin, so it is rejected along with absolute URLs. The BFF
  // applies the same rule again; this copy keeps the SPA from even building such a link.
  const safe =
    returnUrl.startsWith('/') && !returnUrl.startsWith('//') && !returnUrl.startsWith('/\\')
      ? returnUrl
      : '/';
  return `/bff/login?returnUrl=${encodeURIComponent(safe)}`;
}

/** Full-page navigation to the BFF login (OIDC runs server-side); returns to the current page by default. */
export function signIn(returnUrl?: string): void {
  const current = `${window.location.pathname}${window.location.search}${window.location.hash}`;
  navigation.assign(loginUrl(returnUrl ?? current));
}

/** Body of `POST /bff/logout`. */
interface LogoutResponse {
  logoutUrl: string;
}

/**
 * Signs out: the BFF revokes the refresh token, deletes the server-side session and the cookie, and answers with the
 * identity provider's end-session URL, which this navigates to so the provider's SSO session ends too (spec 002).
 * The request goes through the api-client wrapper, which adds the `X-CSRF: 1` header the BFF requires.
 */
export async function signOut(): Promise<void> {
  const { logoutUrl } = await request<LogoutResponse>('/bff/logout', { method: 'POST' });
  navigation.assign(safeLogoutUrl(logoutUrl));
}

// The URL comes from our own BFF, but navigating to a `javascript:` or `data:` URL would run script, so anything
// that is not http(s) falls back to the app root.
function safeLogoutUrl(url: string | undefined): string {
  if (url === undefined || url.length === 0) return '/';
  try {
    const parsed = new URL(url, window.location.origin);
    return parsed.protocol === 'https:' || parsed.protocol === 'http:' ? parsed.toString() : '/';
  } catch {
    return '/';
  }
}
