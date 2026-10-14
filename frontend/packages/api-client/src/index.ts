export * from './http.ts';
export * from './generated/organization/index.ts';
// Each module's generated client defines its own models (e.g. ProblemDetails), so later modules are exposed as
// namespaces or via their subpath (`@fincore/api-client/identity`) instead of being merged into one flat export.
export * as identityApi from './generated/identity/index.ts';
