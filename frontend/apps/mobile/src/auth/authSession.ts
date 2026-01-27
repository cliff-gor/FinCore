import * as AuthSession from 'expo-auth-session';
import * as WebBrowser from 'expo-web-browser';
import { createTokenRefresher, type TokenSet } from './refresh';
import { tokenStorage } from './tokenStorage';

// Web only: completes the popup flow when this bundle is loaded in the redirect window; a no-op on iOS/Android.
WebBrowser.maybeCompleteAuthSession();

/**
 * Customer realm in the local Keycloak (spec 002: `fincore-customers`). Override with EXPO_PUBLIC_OIDC_ISSUER;
 * emulators and devices need an address they can reach, and it must match the issuer FinCore.Api trusts.
 */
export const DEFAULT_OIDC_ISSUER = 'http://localhost:8080/realms/fincore-customers';

/** Public client with PKCE required (Keycloak realm export; master spec §31.4). No secret exists for it. */
export const OIDC_CLIENT_ID = 'fincore-mobile';

/** OIDC issuer; EXPO_PUBLIC_ variables are inlined into the bundle at build time. */
export function getIssuer(): string {
  const fromEnv = process.env.EXPO_PUBLIC_OIDC_ISSUER;
  return fromEnv !== undefined && fromEnv.length > 0 ? fromEnv : DEFAULT_OIDC_ISSUER;
}

/**
 * `fincore://auth/callback` in builds; in Expo Go the `exp://…/--/auth/callback` form, which the dev realm also
 * allows (`exp://*`, development only).
 */
export function getRedirectUri(): string {
  return AuthSession.makeRedirectUri({ scheme: 'fincore', path: 'auth/callback' });
}

// The discovery document rarely changes; one fetch per app run. A failed fetch is not cached, so the next sign-in
// retries.
let discoveryPromise: Promise<AuthSession.DiscoveryDocument> | null = null;
function discovery(): Promise<AuthSession.DiscoveryDocument> {
  discoveryPromise ??= AuthSession.fetchDiscoveryAsync(getIssuer()).catch((error: unknown) => {
    discoveryPromise = null;
    throw error;
  });
  return discoveryPromise;
}

function toTokenSet(response: AuthSession.TokenResponse): TokenSet {
  // issuedAt is in seconds; a provider that omits expires_in gets the realm default of 5 minutes (spec 002).
  const expiresInSeconds = response.expiresIn ?? 300;
  return {
    accessToken: response.accessToken,
    ...(response.refreshToken !== undefined ? { refreshToken: response.refreshToken } : {}),
    expiresAt: (response.issuedAt + expiresInSeconds) * 1000,
  };
}

/**
 * Authorization Code + PKCE (S256) in the system browser (spec 002 "Mobile sign-in"). Resolves `null` when the user
 * cancels or closes the browser.
 */
export async function signInWithBrowser(): Promise<TokenSet | null> {
  const doc = await discovery();
  const redirectUri = getRedirectUri();
  const request = new AuthSession.AuthRequest({
    clientId: OIDC_CLIENT_ID,
    redirectUri,
    responseType: AuthSession.ResponseType.Code,
    // No offline_access: the refresh token stays bound to the IdP session and its 15 min idle / 8 h limits.
    scopes: ['openid', 'profile', 'email'],
    usePKCE: true,
    codeChallengeMethod: AuthSession.CodeChallengeMethod.S256,
  });
  const result = await request.promptAsync(doc);
  if (result.type !== 'success') {
    if (result.type === 'error') throw result.error ?? new Error('Sign-in failed');
    return null;
  }

  const response = await AuthSession.exchangeCodeAsync(
    {
      clientId: OIDC_CLIENT_ID,
      redirectUri,
      code: result.params.code ?? '',
      // The verifier proves this app started the flow; a stolen code alone cannot be exchanged.
      extraParams:
        request.codeVerifier !== undefined ? { code_verifier: request.codeVerifier } : {},
    },
    doc,
  );
  return toTokenSet(response);
}

async function refreshWithIdp(refreshToken: string): Promise<TokenSet> {
  return toTokenSet(
    await AuthSession.refreshAsync({ clientId: OIDC_CLIENT_ID, refreshToken }, await discovery()),
  );
}

/**
 * Revokes the refresh token at the IdP (RFC 7009) so a copy cannot outlive sign-out. Best effort: sign-out still
 * clears the device when the IdP is unreachable.
 */
export async function revokeRefreshToken(refreshToken: string): Promise<void> {
  try {
    await AuthSession.revokeAsync(
      {
        clientId: OIDC_CLIENT_ID,
        token: refreshToken,
        tokenTypeHint: AuthSession.TokenTypeHint.RefreshToken,
      },
      await discovery(),
    );
  } catch {
    // Ignored on purpose: the local tokens are deleted either way and the IdP session times out on its own.
  }
}

// `invalid_grant` and other OAuth error responses are final; anything else (offline, timeout) may be transient.
function isRejected(error: unknown): boolean {
  return error instanceof AuthSession.TokenError;
}

const sessionEndedListeners = new Set<() => void>();

/** Subscribes to "the IdP ended the session" (refresh token rejected); returns the unsubscribe function. */
export function onSessionEnded(listener: () => void): () => void {
  sessionEndedListeners.add(listener);
  return () => sessionEndedListeners.delete(listener);
}

/** The app's single token refresher, shared by the API client and the AuthProvider. */
export const authTokens = createTokenRefresher({
  storage: tokenStorage,
  refresh: refreshWithIdp,
  isRejected,
  onSessionEnded: () => {
    for (const listener of sessionEndedListeners) listener();
  },
});
