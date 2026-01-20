import { defineConfig } from 'orval';

/**
 * One output per backend module (master spec §30.4). Input is the committed OpenAPI snapshot at the repo root.
 * `pnpm api:generate` regenerates; `pnpm api:check` fails if the committed client drifts.
 */
const mutator = {
  path: './packages/api-client/src/http.ts',
  name: 'customFetch',
};

export default defineConfig({
  organization: {
    input: {
      target: '../openapi/organization.json',
    },
    output: {
      mode: 'split',
      target: './packages/api-client/src/generated/organization/index.ts',
      schemas: './packages/api-client/src/generated/organization/model',
      client: 'react-query',
      httpClient: 'fetch',
      clean: true,
      packageJson: './packages/api-client/package.json',
      formatter: 'prettier',
      override: {
        mutator,
        fetch: {
          includeHttpResponseReturnType: false,
        },
        query: {
          signal: true,
          version: 5,
        },
      },
    },
  },
});
