/** Build metadata injected by Vite (see @fincore/config/vite). */
export const buildInfo = {
  version: __APP_VERSION__,
  sha: __GIT_SHA__,
  builtAt: __BUILD_TIME__,
  env: import.meta.env.VITE_APP_ENV ?? 'Development',
} as const;
