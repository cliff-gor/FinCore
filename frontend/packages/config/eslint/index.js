// Shared ESLint building blocks for the FinCore frontend (flat config).
import js from '@eslint/js';
import globals from 'globals';
import reactHooks from 'eslint-plugin-react-hooks';
import tseslint from 'typescript-eslint';

/** Packages that are DOM / BFF-cookie specific and must never be imported by the mobile app (spec §31.3). */
export const webOnlyPackages = ['@fincore/ui', '@fincore/auth'];

/** Build output and generated code are never linted; generated files are checked by `pnpm api:check` instead. */
export const ignores = {
  ignores: [
    '**/node_modules/**',
    '**/dist/**',
    '**/build/**',
    '**/coverage/**',
    '**/.turbo/**',
    '**/.expo/**',
    '**/playwright-report/**',
    '**/test-results/**',
    '**/routeTree.gen.ts',
    '**/expo-env.d.ts',
    'packages/api-client/src/generated/**',
  ],
};

/** Strict TypeScript rules shared by every package and app. */
export const base = [
  js.configs.recommended,
  ...tseslint.configs.strict,
  {
    languageOptions: {
      ecmaVersion: 2023,
      sourceType: 'module',
      globals: { ...globals.browser, ...globals.node },
    },
    rules: {
      '@typescript-eslint/no-explicit-any': 'error',
      '@typescript-eslint/consistent-type-imports': 'error',
      // A leading underscore marks a deliberately unused binding (e.g. the `_Problem` type parameter Orval requires).
      '@typescript-eslint/no-unused-vars': [
        'error',
        { argsIgnorePattern: '^_', varsIgnorePattern: '^_' },
      ],
    },
  },
];

/** Hook rules as errors, not warnings: a stale closure in a money form is a correctness bug. */
export const react = {
  files: ['**/*.tsx'],
  plugins: { 'react-hooks': reactHooks },
  rules: {
    'react-hooks/rules-of-hooks': 'error',
    'react-hooks/exhaustive-deps': 'error',
  },
};

/** Fails the lint when the mobile app imports a web-only package, enforcing the spec §31.3 sharing table. */
export const mobileBoundaries = {
  files: ['apps/mobile/**/*.{ts,tsx,js}'],
  rules: {
    'no-restricted-imports': [
      'error',
      {
        paths: webOnlyPackages.map((name) => ({
          name,
          message: `${name} is web-only (DOM / BFF cookie session) and must not be used by the mobile app.`,
        })),
        patterns: webOnlyPackages.map((name) => ({
          group: [`${name}/*`],
          message: `${name} is web-only and must not be used by the mobile app.`,
        })),
      },
    ],
  },
};

/** Metro, Babel and Tailwind configs in the Expo app are loaded with `require`, so they stay CommonJS. */
export const commonJsFiles = {
  files: ['**/*.cjs', 'apps/mobile/*.js', 'apps/mobile/**/*.config.js'],
  languageOptions: { sourceType: 'commonjs', globals: { ...globals.node } },
  rules: { '@typescript-eslint/no-require-imports': 'off' },
};
