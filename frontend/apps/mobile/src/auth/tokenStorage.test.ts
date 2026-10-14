import * as SecureStore from 'expo-secure-store';
import { tokenStorage } from './tokenStorage';

jest.mock('expo-secure-store', () => {
  const store = new Map<string, string>();
  return {
    WHEN_UNLOCKED_THIS_DEVICE_ONLY: 'WHEN_UNLOCKED_THIS_DEVICE_ONLY',
    getItemAsync: jest.fn((key: string) => Promise.resolve(store.get(key) ?? null)),
    setItemAsync: jest.fn((key: string, value: string) => {
      store.set(key, value);
      return Promise.resolve();
    }),
    deleteItemAsync: jest.fn((key: string) => {
      store.delete(key);
      return Promise.resolve();
    }),
  };
});

const secureStore = jest.mocked(SecureStore);

describe('tokenStorage (spec 002 AC-5)', () => {
  afterEach(async () => {
    await tokenStorage.clear();
    jest.clearAllMocks();
  });

  it('keeps the refresh token only in SecureStore, device-only', async () => {
    await tokenStorage.setRefreshToken('refresh-1');

    expect(secureStore.setItemAsync).toHaveBeenCalledWith(
      'fincore.auth.refreshToken',
      'refresh-1',
      { keychainAccessible: 'WHEN_UNLOCKED_THIS_DEVICE_ONLY' },
    );
    expect(await tokenStorage.getRefreshToken()).toBe('refresh-1');
  });

  it('keeps the access token in memory and never writes it to SecureStore', () => {
    tokenStorage.setAccessToken({ token: 'access-1', expiresAt: 1 });

    expect(tokenStorage.getAccessToken()).toEqual({ token: 'access-1', expiresAt: 1 });
    expect(secureStore.setItemAsync).not.toHaveBeenCalled();
  });

  it('clear forgets both tokens', async () => {
    tokenStorage.setAccessToken({ token: 'access-1', expiresAt: 1 });
    await tokenStorage.setRefreshToken('refresh-1');

    await tokenStorage.clear();

    expect(tokenStorage.getAccessToken()).toBeNull();
    expect(await tokenStorage.getRefreshToken()).toBeNull();
    expect(secureStore.deleteItemAsync).toHaveBeenCalledWith('fincore.auth.refreshToken', {
      keychainAccessible: 'WHEN_UNLOCKED_THIS_DEVICE_ONLY',
    });
  });
});
