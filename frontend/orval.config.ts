import { defineConfig } from 'orval';

/**
 * One output per backend module (master spec §30.4). Input is the committed OpenAPI snapshot at the repo root.
 * `pnpm api:generate` regenerates; `pnpm api:check` fails if the committed client drifts.
 */
const mutator = {
  path: './packages/api-client/src/http.ts',
  name: 'customFetch',
};

/** Same generator settings for every module; only the input document and output folder differ. */
const moduleOutput = (module: string) => ({
  input: {
    target: `../openapi/${module}.json`,
  },
  output: {
    mode: 'split' as const,
    target: `./packages/api-client/src/generated/${module}/index.ts`,
    schemas: `./packages/api-client/src/generated/${module}/model`,
    client: 'react-query' as const,
    httpClient: 'fetch' as const,
    clean: true,
    packageJson: './packages/api-client/package.json',
    formatter: 'prettier' as const,
    override: {
      mutator,
      fetch: {
        includeHttpResponseReturnType: false,
      },
      query: {
        signal: true,
        version: 5 as const,
      },
    },
  },
});

export default defineConfig({
  organization: moduleOutput('organization'),
  identity: moduleOutput('identity'),
});
