import type { ConfigContext, ExpoConfig } from 'expo/config';
import pkg from './package.json';

/**
 * FinCore customer app (master spec §31). `version` comes from package.json (release-please bumps it);
 * build numbers are managed remotely by EAS (eas.json: appVersionSource remote + autoIncrement).
 */
export default ({ config }: ConfigContext): ExpoConfig => ({
  ...config,
  name: 'FinCore',
  slug: 'fincore',
  scheme: 'fincore',
  version: pkg.version,
  orientation: 'portrait',
  userInterfaceStyle: 'automatic',
  runtimeVersion: { policy: 'appVersion' },
  ios: {
    bundleIdentifier: 'com.fincore.app',
    supportsTablet: false,
  },
  android: {
    package: 'com.fincore.app',
  },
  web: {
    bundler: 'metro',
  },
  plugins: ['expo-router', 'expo-localization'],
  experiments: {
    typedRoutes: true,
  },
});
