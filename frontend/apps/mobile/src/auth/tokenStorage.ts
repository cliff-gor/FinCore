import * as SecureStore from 'expo-secure-store';

/** An access token held in memory with its absolute expiry (epoch milliseconds). */
export interface AccessToken {
  token: string;
  expiresAt: number;
}

const REFRESH_TOKEN_KEY = 'fincore.auth.refreshToken';

// Keychain item readable only while the device is unlocked and never migrated to another device through a
// backup, so a restored backup cannot resume the session (spec §31.4).
const secureOptions: SecureStore.SecureStoreOptions = {
  keychainAccessible: SecureStore.WHEN_UNLOCKED_THIS_DEVICE_ONLY,
};

// Module memory only: the access token is never persisted, so a cold start always goes through a refresh.
let accessToken: AccessToken | null = null;

/**
 * Where the mobile app keeps its tokens (spec 002 AC-5, master spec §31.4): the refresh token only in SecureStore
 * (iOS Keychain / Android Keystore), the short-lived access token only in memory.
 */
export const tokenStorage = {
  /** The in-memory access token, or `null` after a cold start or sign-out. */
  getAccessToken(): AccessToken | null {
    return accessToken;
  },

  setAccessToken(token: AccessToken | null): void {
    accessToken = token;
  },

  getRefreshToken(): Promise<string | null> {
    return SecureStore.getItemAsync(REFRESH_TOKEN_KEY, secureOptions);
  },

  /** Stores the newest refresh token; with rotation the previous one is already spent at the IdP. */
  setRefreshToken(token: string): Promise<void> {
    return SecureStore.setItemAsync(REFRESH_TOKEN_KEY, token, secureOptions);
  },

  /** Forgets both tokens (sign-out, or the IdP rejected the refresh token). */
  async clear(): Promise<void> {
    accessToken = null;
    await SecureStore.deleteItemAsync(REFRESH_TOKEN_KEY, secureOptions);
  },
};

/** The storage contract `refresh.ts` depends on; tests pass `tokenStorage` itself. */
export type TokenStorage = typeof tokenStorage;
