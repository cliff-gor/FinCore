import { getHttpConfig } from '@fincore/api-client';
import { appVersionHeader, configureApi, DEFAULT_API_URL } from './configureApi';

describe('configureApi', () => {
  it('uses bearer mode against the Mobile Gateway with the app version headers', async () => {
    configureApi(() => 'abc');
    const config = getHttpConfig();

    expect(config.baseUrl).toBe(process.env.EXPO_PUBLIC_API_URL ?? DEFAULT_API_URL);
    expect(config.csrfHeader).toBeUndefined();
    expect(config.clientVersion).toMatch(/^mobile\/\d+\.\d+\.\d+$/);
    expect(config.headers?.['X-App-Version']).toMatch(/^mobile\/\d+\.\d+\.\d+ \(\w+\)$/);
    expect(await config.getAuthHeader?.()).toBe('Bearer abc');
  });

  it('sends no Authorization header without a token', async () => {
    configureApi();
    expect(await getHttpConfig().getAuthHeader?.()).toBeUndefined();
  });

  it('formats X-App-Version as mobile/<version> (<build>)', () => {
    expect(appVersionHeader({ version: '0.1.0', build: '45' })).toBe('mobile/0.1.0 (45)');
  });
});
