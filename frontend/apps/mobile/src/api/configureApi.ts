import { configureHttp } from '@fincore/api-client';
import Constants from 'expo-constants';

/**
 * Mobile Gateway (FinCore.Bff, Mobile profile, :7202). Override with EXPO_PUBLIC_API_URL:
 * Android emulators reach the host machine at https://10.0.2.2:7202; physical devices need the LAN IP or a tunnel.
 */
export const DEFAULT_API_URL = 'https://localhost:7202';

export function getApiUrl(): string {
  const fromEnv = process.env.EXPO_PUBLIC_API_URL;
  return fromEnv !== undefined && fromEnv.length > 0 ? fromEnv : DEFAULT_API_URL;
}

export interface AppVersionInfo {
  version: string;
  build: string;
}

/** Store version from app config; build number from the native config when EAS has stamped it. */
export function getAppVersionInfo(): AppVersionInfo {
  const config = Constants.expoConfig;
  const version = config?.version ?? '0.0.0';
  const build = config?.ios?.buildNumber ?? config?.android?.versionCode?.toString() ?? '0';
  return { version, build };
}

/** `X-App-Version: mobile/<version> (<build>)` — the gateway answers 426 below the minimum version. */
export function appVersionHeader({ version, build }: AppVersionInfo): string {
  return `mobile/${version} (${build})`;
}

/** Bearer mode: no cookies or CSRF. The access token getter is wired in M1 (expo-auth-session). */
export function configureApi(getAccessToken: () => string | null = () => null): void {
  const info = getAppVersionInfo();
  configureHttp({
    baseUrl: getApiUrl(),
    getAuthHeader: () => {
      const token = getAccessToken();
      return token === null ? undefined : `Bearer ${token}`;
    },
    clientVersion: `mobile/${info.version}`,
    headers: { 'X-App-Version': appVersionHeader(info) },
  });
}
