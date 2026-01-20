import { execSync } from 'node:child_process';
import type { Plugin } from 'vite';

/** Build metadata shown in the footer and published as /version.json (master spec §33.7.3). */
export interface BuildInfo {
  app: string;
  version: string;
  commit: string;
  builtAtUtc: string;
  environment: string;
}

function readGitSha(): string {
  try {
    const sha = execSync('git rev-parse --short HEAD', { stdio: ['ignore', 'pipe', 'ignore'] })
      .toString()
      .trim();
    return sha.length > 0 ? sha : 'dev';
  } catch {
    return 'dev';
  }
}

export function readBuildInfo(app: string, version: string, environment?: string): BuildInfo {
  return {
    app,
    version,
    commit: process.env.GIT_SHA ?? readGitSha(),
    builtAtUtc: new Date().toISOString(),
    environment: environment !== undefined && environment.length > 0 ? environment : 'Development',
  };
}

/**
 * Injects __APP_VERSION__, __GIT_SHA__ and __BUILD_TIME__ and emits /version.json on build.
 */
export function fincoreVersionPlugin(info: BuildInfo): Plugin {
  return {
    name: 'fincore-version',
    config() {
      return {
        define: {
          __APP_VERSION__: JSON.stringify(info.version),
          __GIT_SHA__: JSON.stringify(info.commit),
          __BUILD_TIME__: JSON.stringify(info.builtAtUtc),
        },
      };
    },
    generateBundle() {
      this.emitFile({
        type: 'asset',
        fileName: 'version.json',
        source: `${JSON.stringify(info, null, 2)}\n`,
      });
    },
  };
}
