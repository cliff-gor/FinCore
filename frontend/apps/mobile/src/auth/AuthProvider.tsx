import { request } from '@fincore/api-client';
import { useQueryClient } from '@tanstack/react-query';
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react';
import { authTokens, onSessionEnded, revokeRefreshToken, signInWithBrowser } from './authSession';
import { tokenStorage } from './tokenStorage';

/**
 * Session state machine (master spec §31.2). `restoring` is the cold-start check for a stored refresh token; the
 * home screen waits for it so it never flashes "signed out" for a signed-in user.
 */
export type AuthStatus = 'restoring' | 'signed-out' | 'signing-in' | 'signed-in' | 'error';

/** What `useAuth` returns. */
export interface AuthState {
  status: AuthStatus;
  error: unknown;
  signIn: () => Promise<void>;
  signOut: () => Promise<void>;
}

const AuthContext = createContext<AuthState | null>(null);

/** Customer `/me` in M1: identity only (spec 002). */
export interface Me {
  id: string;
  kind: 'customer' | 'staff';
  name: string;
  email: string;
}

/** Fetches `/api/v1/me` through the Mobile Gateway with the bearer token. */
export function fetchMe(): Promise<Me> {
  return request<Me>('/api/v1/me');
}

/** Provides sign-in state; must sit inside the QueryClientProvider (sign-out clears the cache). */
export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [status, setStatus] = useState<AuthStatus>('restoring');
  const [error, setError] = useState<unknown>(null);

  useEffect(() => {
    let active = true;
    // A stored refresh token means a previous session; one refresh proves it is still valid.
    authTokens
      .refresh()
      .then((token) => active && setStatus(token === null ? 'signed-out' : 'signed-in'))
      .catch((e: unknown) => {
        if (!active) return;
        setError(e);
        setStatus('error');
      });
    const unsubscribe = onSessionEnded(() => {
      queryClient.clear();
      setStatus('signed-out');
    });
    return () => {
      active = false;
      unsubscribe();
    };
  }, [queryClient]);

  const signIn = useCallback(async () => {
    setError(null);
    setStatus('signing-in');
    try {
      const tokens = await signInWithBrowser();
      if (tokens === null) {
        setStatus('signed-out');
        return;
      }
      await authTokens.accept(tokens);
      setStatus('signed-in');
    } catch (e) {
      setError(e);
      setStatus('error');
    }
  }, []);

  const signOut = useCallback(async () => {
    // Spec §31.4: revoke the refresh token, clear SecureStore and the query cache. Revocation is best effort, the
    // device is cleared regardless.
    const refreshToken = await tokenStorage.getRefreshToken();
    if (refreshToken !== null) await revokeRefreshToken(refreshToken);
    await tokenStorage.clear();
    queryClient.clear();
    setError(null);
    setStatus('signed-out');
  }, [queryClient]);

  const value = useMemo(
    () => ({ status, error, signIn, signOut }),
    [status, error, signIn, signOut],
  );
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

/** Current sign-in state and actions. */
export function useAuth(): AuthState {
  const value = useContext(AuthContext);
  if (value === null) throw new Error('useAuth must be used inside <AuthProvider>.');
  return value;
}
