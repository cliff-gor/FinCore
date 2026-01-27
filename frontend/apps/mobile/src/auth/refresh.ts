import type { TokenStorage } from './tokenStorage';

/** Tokens returned by the identity provider; `refreshToken` is absent when the provider keeps the old one. */
export interface TokenSet {
  accessToken: string;
  refreshToken?: string;
  /** Absolute access-token expiry, epoch milliseconds. */
  expiresAt: number;
}

/** Refresh this long before expiry, so a token never expires while a request is in flight. */
export const REFRESH_MARGIN_MS = 60_000;

/** Dependencies of the refresher; `authSession.ts` supplies the expo-auth-session implementations. */
export interface TokenRefresherOptions {
  storage: TokenStorage;
  /** Exchanges a refresh token for new tokens at the IdP. */
  refresh: (refreshToken: string) => Promise<TokenSet>;
  /** True when the IdP definitely refused the refresh token (e.g. `invalid_grant`), as opposed to a network error. */
  isRejected: (error: unknown) => boolean;
  /** Called after a rejected refresh has cleared storage, so the UI can return to signed-out. */
  onSessionEnded?: () => void;
  now?: () => number;
}

/** Hands out a valid access token, refreshing it at most once at a time. */
export interface TokenRefresher {
  /** A valid access token, refreshing first when needed; `null` when signed out or the session ended. */
  getAccessToken(): Promise<string | null>;
  /** Forces a refresh (shared with any refresh already running). */
  refresh(): Promise<string | null>;
  /** Stores tokens from sign-in. */
  accept(tokens: TokenSet): Promise<void>;
}

/**
 * Single-flight refresh (spec 002 AC-5). The app fires several queries at once; with refresh-token rotation each
 * refresh token is single-use, so parallel refreshes would race and all but one would be rejected, signing the user
 * out. Every caller therefore shares one in-flight refresh.
 */
export function createTokenRefresher(options: TokenRefresherOptions): TokenRefresher {
  const { storage, now = Date.now } = options;
  let inFlight: Promise<string | null> | null = null;

  async function accept(tokens: TokenSet): Promise<void> {
    // Persist the rotated refresh token before exposing the new access token: if the app is killed in between, the
    // next start must use the new refresh token, the old one is already spent.
    if (tokens.refreshToken !== undefined) await storage.setRefreshToken(tokens.refreshToken);
    storage.setAccessToken({ token: tokens.accessToken, expiresAt: tokens.expiresAt });
  }

  async function runRefresh(): Promise<string | null> {
    const refreshToken = await storage.getRefreshToken();
    if (refreshToken === null) return null;
    try {
      const tokens = await options.refresh(refreshToken);
      await accept(tokens);
      return tokens.accessToken;
    } catch (error) {
      if (!options.isRejected(error)) throw error; // offline or IdP down: keep the session, the caller may retry
      await storage.clear();
      options.onSessionEnded?.();
      return null;
    }
  }

  function refresh(): Promise<string | null> {
    inFlight ??= runRefresh().finally(() => {
      inFlight = null;
    });
    return inFlight;
  }

  return {
    accept,
    refresh,
    async getAccessToken() {
      const current = storage.getAccessToken();
      if (current !== null && current.expiresAt - now() > REFRESH_MARGIN_MS) return current.token;
      return refresh();
    },
  };
}
