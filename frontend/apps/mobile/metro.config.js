// Monorepo-aware Metro config. Since SDK 52, `getDefaultConfig` detects the pnpm workspace and sets
// `watchFolders` / `nodeModulesPaths` for frontend/ automatically, so shared packages resolve from source.
const { getDefaultConfig } = require('expo/metro-config');
const { withNativeWind } = require('nativewind/metro');

const config = getDefaultConfig(__dirname);

module.exports = withNativeWind(config, { input: './global.css' });
