import { createTokenRefresher, REFRESH_MARGIN_MS, type TokenSet } from './refresh';
import type { AccessToken, TokenStorage } from './tokenStorage';

class Rejected extends Error {}

/** In-memory stand-in with the same contract as `tokenStorage`. */
function memoryStorage(refreshToken: string | null) {
  let access: AccessToken | null = null;
  let refresh = refreshToken;
  const storage: TokenStorage = {
    getAccessToken: () => access,
    setAccessToken: (token) => {
      access = token;
    },
    getRefreshToken: () => Promise.resolve(refresh),
    setRefreshToken: (token) => {
      refresh = token;
      return Promise.resolve();
    },
    clear: () => {
      access = null;
      refresh = null;
      return Promise.resolve();
    },
  };
  return storage;
}

const NOW = 1_000_000;

function setup(refreshToken: string | null, refresh: (rt: string) => Promise<TokenSet>) {
  const storage = memoryStorage(refreshToken);
  const onSessionEnded = jest.fn();
  const refreshFn = jest.fn(refresh);
  const refresher = createTokenRefresher({
    storage,
    refresh: refreshFn,
    isRejected: (e) => e instanceof Rejected,
    onSessionEnded,
    now: () => NOW,
  });
  return { storage, refresher, refreshFn, onSessionEnded };
}

describe('single-flight token refresh (spec 002 AC-5)', () => {
  it('shares one refresh between concurrent callers and stores the rotated refresh token', async () => {
    const { storage, refresher, refreshFn } = setup('refresh-1', async () => {
      await new Promise((r) => setTimeout(r, 10));
      return { accessToken: 'access-2', refreshToken: 'refresh-2', expiresAt: NOW + 300_000 };
    });

    const tokens = await Promise.all(Array.from({ length: 5 }, () => refresher.getAccessToken()));

    expect(refreshFn).toHaveBeenCalledTimes(1);
    expect(refreshFn).toHaveBeenCalledWith('refresh-1');
    expect(tokens).toEqual(Array(5).fill('access-2'));
    expect(await storage.getRefreshToken()).toBe('refresh-2');
    expect(storage.getAccessToken()?.token).toBe('access-2');
  });

  it('uses the in-memory token while it is valid for more than the margin', async () => {
    const { storage, refresher, refreshFn } = setup('refresh-1', jest.fn());
    storage.setAccessToken({ token: 'access-1', expiresAt: NOW + REFRESH_MARGIN_MS + 1 });

    expect(await refresher.getAccessToken()).toBe('access-1');
    expect(refreshFn).not.toHaveBeenCalled();
  });

  it('refreshes a token that expires within the margin', async () => {
    const { storage, refresher, refreshFn } = setup('refresh-1', () =>
      Promise.resolve({ accessToken: 'access-2', expiresAt: NOW + 300_000 }),
    );
    storage.setAccessToken({ token: 'access-1', expiresAt: NOW + REFRESH_MARGIN_MS });

    expect(await refresher.getAccessToken()).toBe('access-2');
    expect(refreshFn).toHaveBeenCalledTimes(1);
    // No new refresh token from the IdP: the stored one is kept.
    expect(await storage.getRefreshToken()).toBe('refresh-1');
  });

  it('returns null without calling the IdP when signed out', async () => {
    const { refresher, refreshFn } = setup(null, jest.fn());

    expect(await refresher.getAccessToken()).toBeNull();
    expect(refreshFn).not.toHaveBeenCalled();
  });

  it('ends the session when the IdP rejects the refresh token', async () => {
    const { storage, refresher, onSessionEnded } = setup('refresh-1', () =>
      Promise.reject(new Rejected('invalid_grant')),
    );

    expect(await refresher.getAccessToken()).toBeNull();
    expect(await storage.getRefreshToken()).toBeNull();
    expect(onSessionEnded).toHaveBeenCalledTimes(1);
  });

  it('keeps the session when the IdP is unreachable', async () => {
    const { storage, refresher, onSessionEnded } = setup('refresh-1', () =>
      Promise.reject(new Error('Network request failed')),
    );

    await expect(refresher.getAccessToken()).rejects.toThrow('Network request failed');
    expect(await storage.getRefreshToken()).toBe('refresh-1');
    expect(onSessionEnded).not.toHaveBeenCalled();
  });

  it('starts a new refresh after the previous one finished', async () => {
    let call = 0;
    const { refresher, refreshFn } = setup('refresh-1', () => {
      call += 1;
      return Promise.resolve({
        accessToken: `access-${call + 1}`,
        refreshToken: `refresh-${call + 1}`,
        expiresAt: NOW,
      });
    });

    expect(await refresher.refresh()).toBe('access-2');
    expect(await refresher.refresh()).toBe('access-3');
    expect(refreshFn).toHaveBeenNthCalledWith(2, 'refresh-2');
  });
});
