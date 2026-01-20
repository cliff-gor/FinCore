# Spec 001: M0 — Foundations & Walking Skeleton

**Status:** Approved (2026-10-13)
**Date:** 2026-10-13
**Master spec:** `docs/FinCore-Project-Spec.md` (v1.0) — this spec narrows it to Milestone M0 and resolves its gaps.

## Problem
FinCore (portfolio/learning modular-monolith banking + MFI platform, .NET 10 / C# 14) starts from an empty folder.
Every later milestone (M1–M9) needs working plumbing across all layers first, so that integration problems surface
in week 2, not month 6 (master spec §35.3).

## Scope

### In
- M0 only, **code only**: everything needed for the walking skeleton to build, test and run locally.
- **Organization module — thin real slice:** `Institution` aggregate, EF Core `OrganizationDbContext` (`org` schema),
  initial migration, `POST/GET /institutions`, business-date query. Proves persistence, outbox write and
  Result → ProblemDetails end to end. Branches, holidays and EOD stay in M1.
- **Frontend — all three shells:** pnpm + Turborepo monorepo; `packages/config`; `packages/api-client`
  (`http.ts` with cookie+CSRF and bearer modes, Orval config); empty Back Office and Portal (Vite, React 19,
  TanStack Router + Query, version footer); Expo app shell calling `/health` through the Mobile Gateway.
- **Auth — wiring, not enforcement:** Keycloak + dev realm export in docker-compose/Aspire; JWT bearer configured in
  the API; permission-policy infrastructure built; M0 endpoints allowed anonymously **in Development only**;
  BFF `/bff/login|logout|user` stubbed.
- **DevOps — CI + local stack + IaC skeleton:** `ci.yml`, `release-please.yml` + config/manifest, docker-compose,
  Aspire AppHost, container publish for api/gateway/migrator, Terraform `bootstrap/` + module skeletons
  (`terraform validate` passes). `cd-staging.yml` written but disabled (manual-dispatch only) until AWS is set up.

### Out
- Milestones M1–M9 (each gets its own spec).
- Any actual AWS deployment: no `terraform apply`, no AWS account use, no GitHub environment setup.
- Organization branches, holidays, EOD runs, exchange rates (M1).
- Working OIDC login and permission enforcement (M1).
- `cd-production.yml`, `deploy-manual.yml`, `mobile-release.yml`, reusable `_*.yml` deploy workflows, and full
  Terraform module implementations beyond skeletons (separate AWS spec).

## Domain Model

### Institution (aggregate, `org.institutions`)
| Field | Rule |
|---|---|
| `Id` | `InstitutionId` (Guid v7) |
| `Code` | 3–6 uppercase alphanumerics, globally unique, **immutable** |
| `BaseCurrency` | ISO-4217, must exist and be active in `org.currencies` |
| `CurrentBusinessDate` | `DateOnly`, **required in the create request** (`initialBusinessDate`); never derived from the server clock |
| `Name` | required, 2–200 chars |
| `Type` | required, `Bank` \| `Mfi` |
| `LicenseNumber` | optional, ≤ 50 chars |
| `Status` | enum `Active` \| `Suspended` \| `Closed`; **created as `Active`**; transition endpoints are M1 |
| audit | `CreatedAtUtc`, `CreatedBy`, `ModifiedAtUtc`, `ModifiedBy` (interceptor) + `xmin` concurrency token |

### Dev seed (Development only, idempotent by `Code`)
- **FinCore Bank**: code `FCB`, type Bank, KES, business date 2026-10-13
- **FinCore Microfinance**: code `FCMFI`, type Mfi, KES, business date 2026-10-13
- Branches, products, users and the chart of accounts arrive with their own milestones.

### Currency (reference, `org.currencies`)
Seeded: **KES, USD, EUR, GBP, UGX, TZS** with correct ISO minor units (UGX 0 per ISO-4217; others 2).
Dev seed data defaults to **KES**.

### Events
- `InstitutionCreatedDomainEvent` → mapped to `InstitutionCreatedIntegrationEvent` (Organization.Contracts).
- **Full pipeline in M0:** written to `org.outbox_messages` in the same transaction → `ProcessOutboxJob`
  (Quartz, every 2 s) publishes to the in-memory bus → a logging subscriber handles it inside
  `IdempotentEventHandlerDecorator`, recording `(message_id, handler_name)` in `inbox_messages`.

## API Contract
Conventions per master §28: `/api/v1`, camelCase JSON, enums as strings, `DateOnly` as `yyyy-MM-dd`,
RFC 9457 ProblemDetails with `code` + `traceId`, `X-Correlation-Id` echoed, `X-FinCore-Version` on every response.
OpenAPI document `/openapi/organization.json`, browsable at `/scalar` (Development).

### Organization (`/api/v1`)
| Verb | Route | Request | Success | Errors | Policy |
|---|---|---|---|---|---|
| POST | `/institutions` | `{ code, name, type, baseCurrency, licenseNumber?, initialBusinessDate }` | 201 + `Location`, `InstitutionResponse` | 400 validation (incl. unsupported currency in `errors.baseCurrency`, weekend date); 409 `Organization.InstitutionCodeTaken` | `org.manage` |
| GET | `/institutions?page=&pageSize=` | page ≥ 1, pageSize 1–100 (default 25) | 200 `{ items, page, pageSize, totalCount }` ordered by `code` | 400 | `org.read` |
| GET | `/institutions/{id}` | — | 200 `InstitutionResponse` | 404 `Organization.InstitutionNotFound` | `org.read` |
| GET | `/institutions/{id}/business-date` | — | 200 `{ institutionId, businessDate }` | 404 | authenticated |

`InstitutionResponse`: `{ id, code, name, type, baseCurrency, licenseNumber, status, currentBusinessDate, createdAtUtc }`.
> `GET /institutions` (paged) is an addition to master §12 — the master spec is updated in the same change.

### Platform (anonymous in every environment)
| Route | Behavior |
|---|---|
| `GET /health/live` | process is up |
| `GET /health/ready` | Postgres reachable, outbox backlog below threshold |
| `GET /api/version` | master §33.7.3 shape: `product`, `commit`, `builtAtUtc`, `environment`, `apiContract` = `v1`, `minimumMobileVersion` (config, default `0.1.0`), `modules[]` with `name`, `enabled`, `schema` (latest applied migration) |

### Gateway (`FinCore.Bff`, profiles BackOffice / Portal / Mobile)
| Route | M0 behavior |
|---|---|
| `GET /bff/user` | **401 everywhere** (no fake users) |
| `GET /bff/login?returnUrl=` | stub: redirects to `returnUrl` (local paths only) — real OIDC in M1 |
| `POST /bff/logout` | stub: 204 |
| `/api/*` (BackOffice/Portal) | YARP proxy to the API; requires `X-CSRF: 1` else 400 |
| `/api/*`, `/health` (Mobile) | YARP passthrough; parses `X-App-Version: mobile/x.y.z (build)`; below `minimumMobileVersion` → **426 Upgrade Required** |

### Idempotency (building block, no production endpoint uses it in M0)
`IdempotencyFilter` + `IIdempotencyStore` (Postgres implementation): key + request hash + stored response,
24 h retention; replay → stored response; same key with different body → 422; missing key on a filtered endpoint → 400.
Verified by integration tests against a **test-only** endpoint registered in the test host.

## Authorization
- Permission policies (`PermissionAuthorizationHandler`, `Permissions` constants) are built and attached to every
  Organization endpoint per the table above.
- JWT bearer authentication configured from `Auth:Authority` / `Auth:Audience` (Keycloak locally).
- **Development only:** Organization endpoints allow anonymous access (explicit, environment-checked switch in code,
  not configuration).
- **Every other environment: fail closed.** No permission source exists until Identity (M1), so requests get 401/403.
- `/health/*` and `/api/version` are anonymous everywhere.
- BFF CSRF header check is active in M0; cookie session / OIDC / token storage are M1.

## Edge Cases & Failure Modes
| Case | Behavior |
|---|---|
| `code` in lowercase / padded | Trimmed and uppercased before validation; uniqueness effectively case-insensitive |
| Duplicate `code` (sequential) | 409 `Organization.InstitutionCodeTaken` |
| Duplicate `code` (concurrent race) | Unique index on `code`; Postgres unique-violation mapped to the same 409 |
| Unsupported / inactive currency | 400, `errors.baseCurrency` |
| `initialBusinessDate` on Sat/Sun | 400, `errors.initialBusinessDate` (holiday calendar is M1) |
| Unknown institution id | 404 `Organization.InstitutionNotFound` |
| Concurrent update of an aggregate | `xmin` token → 409 (no update endpoints in M0; covered by base infrastructure test) |
| Outbox publish fails | Retry with exponential backoff (~2 s, 4 s, 8 s, 16 s); after **5 attempts** set `dead_lettered_on_utc` + store error, log at Error, increment `fincore.outbox.dead_lettered` |
| Outbox backlog > 1000 pending | `/health/ready` reports Degraded |
| Same integration event delivered twice | Inbox `(message_id, handler_name)` → second delivery skipped |
| Idempotency key replay / mismatch / missing | Stored response / 422 / 400 (see API Contract) |
| Dev seeder runs repeatedly | No-op for existing codes (idempotent) |
| Unhandled exception | `GlobalExceptionHandler` → 500 ProblemDetails with `traceId`, no stack trace or PII outside Development |
| Mobile app below minimum version | 426 Upgrade Required |
| Web `/api` call without `X-CSRF: 1` | 400 from the BFF |

## Non-Functional Requirements
- **Build:** `dotnet build` with warnings as errors, analyzers `latest-recommended`, Central Package Management, SDK pinned by `global.json`.
- **Coverage gate from M0:** CI fails below **80 % line coverage** on Domain projects (SharedKernel, Organization.Domain).
- **Architecture rules enforced by tests** (NetArchTest): Domain depends only on SharedKernel; modules reference other
  modules only via `.Contracts`; Contracts depend only on SharedKernel; handlers/validators/EF configs are `internal sealed`.
- **Frontend:** TypeScript `strict`, no `any`; ESLint + `tsc --noEmit` clean; web Vitest + Playwright smoke; mobile Jest + `expo-doctor` clean.
- **Observability:** Serilog structured logs (Seq locally), correlation id on every log line, OpenTelemetry traces/metrics
  via ServiceDefaults with `service.version`; custom metrics `fincore.outbox.pending`, `fincore.outbox.dead_lettered`.
- **Security:** no secrets in repo (dev-only Keycloak passwords in the realm export, clearly marked); PII-free logs;
  security headers on the BFF.
- **Performance/volume:** no budgets in M0 (plumbing only); revisited in M9 load testing.

## Integrations
- **Local stack (docker-compose and Aspire AppHost):** PostgreSQL 17 (with `init-schemas.sql` creating module schemas
  + per-module roles), Keycloak, Seq. Redis, RabbitMQ and MailPit are added by the milestones that first need them.
- **Keycloak dev realm `fincore`:** clients `fincore-api` (audience), `fincore-backoffice` and `fincore-portal`
  (confidential), `fincore-mobile` (public, PKCE, redirect `fincore://auth/callback`); one dev user `sysadmin`.
- **Mobile:** Expo shell proven by `tsc`, Jest, `expo-doctor` and running locally (Expo Go / simulator) showing the
  gateway `/health` result. `eas.json` profiles written; EAS builds deferred until an Expo account exists.
- **Published events:** `InstitutionCreatedIntegrationEvent` (consumed in M0 only by the logging subscriber).
- **GitHub:** `ci.yml` and `release-please.yml` active; `cd-staging.yml` present but `workflow_dispatch`-only.
- **AWS:** none (code skeleton only).
- **Aspire AppHost** orchestrates Postgres, Keycloak, Seq, the API, the gateway in its three profiles
  (BackOffice :7200, Portal :7201, Mobile :7202) and both Vite apps (:5173, :5174). Expo runs separately.
- **API client generation:** a build step exports `openapi/organization.json` from the API without a running server;
  the snapshot is committed; Orval generates `packages/api-client` from it; CI fails if snapshot or client drifts.
- **Module switches:** `Modules:<Name>:Enabled` honored by `ModuleRegistry` (endpoints, jobs, event subscriptions
  skipped when disabled; migrations still run); reported in `/api/version`. Organization enabled by default.

## Delivery & Version Control
- Branch `wp/M0-foundations`, one commit per logical step, master §35.7 format, using the repo's configured git
  identity, **no AI attribution trailers** (enforced by `.claude/settings.json` and `.githooks/commit-msg`). No push (no remote).
- **Owner-approved exception to master §35.7.1 (dates only):** M0 commits are backdated to **2026-01-20**
  (sequential times that day) via `GIT_AUTHOR_DATE` / `GIT_COMMITTER_DATE`. Identity and no-attribution rules are unchanged.
- The master spec is saved verbatim as `docs/FinCore-Project-Spec.md`; the only change is adding paged
  `GET /institutions` to §12, made in the same change.

## Acceptance Criteria

**Repo & build**
- **AC-1** Given a fresh clone, when `dotnet build FinCore.slnx` runs, then it succeeds with zero warnings.
- **AC-2** Given the repo, when `dotnet test` runs (Docker available), then all unit, architecture and integration tests pass and Domain line coverage is ≥ 80 %.
- **AC-3** Given a class in `Organization.Domain` that references EF Core or another module's non-Contracts project, when architecture tests run, then they fail.
- **AC-4** Given a commit message containing `Co-Authored-By: Claude`, when committing with hooks enabled, then the commit is rejected.

**Shared kernel**
- **AC-5** Given `Money` values in different currencies, when added, then `CurrencyMismatchException` is thrown; given KES 10.005, when rounded with the default policy, then the result is KES 10.00 (banker's rounding).
- **AC-6** Given a business failure, when a handler returns `Result.Failure(error)`, then the endpoint returns RFC 9457 ProblemDetails with the matching status, `code` and `traceId`.

**Organization slice**
- **AC-7** Given a valid request `{ code: "fcb2", name: "Test Bank", type: "Bank", baseCurrency: "KES", initialBusinessDate: "2026-10-14" }` in Development, when `POST /api/v1/institutions`, then 201 with `Location`, `code` = `FCB2`, `status` = `Active`, `currentBusinessDate` = `2026-10-14`.
- **AC-8** Given an institution with code `FCB2` exists, when another is created with `fcb2` (sequentially or concurrently), then 409 `Organization.InstitutionCodeTaken`.
- **AC-9** Given `baseCurrency` = `JPY`, or `initialBusinessDate` on a Saturday, or `code` = `AB` / `TOOLONG1`, or `name` of 1 char, then 400 with the field named in `errors`.
- **AC-10** Given institutions exist, when `GET /institutions?page=1&pageSize=1`, then 200 with one item ordered by code and the correct `totalCount`; `pageSize=101` → 400.
- **AC-11** Given an unknown id, when `GET /institutions/{id}` or `/business-date`, then 404 `Organization.InstitutionNotFound`; given a known id, `/business-date` returns `{ institutionId, businessDate }`.
- **AC-12** Given the Production environment and no token, when calling any Organization endpoint, then 401; `/health/live`, `/health/ready` and `/api/version` still return 200.
- **AC-13** Given Development with an empty database, when the API starts, then migrations apply, currencies KES/USD/EUR/GBP/UGX(0 minor units)/TZS exist, and FCB + FCMFI are seeded; a restart creates no duplicates.

**Event pipeline & reliability**
- **AC-14** Given an institution is created, then an `InstitutionCreated` outbox row is written in the same transaction, and within 10 s it is processed and the logging subscriber's inbox row exists.
- **AC-15** Given the same integration event is delivered twice, then the subscriber's side effect happens once.
- **AC-16** Given a subscriber that always throws, then after 5 attempts the outbox message is dead-lettered with its error recorded.
- **AC-17** Given the test-only idempotent endpoint, when called twice with the same key and body, then the second response equals the first and the handler ran once; same key + different body → 422; no key → 400.

**Platform endpoints**
- **AC-18** Given the API is running, when `GET /api/version`, then it returns product, commit, environment, `apiContract: "v1"`, `minimumMobileVersion`, and `modules` containing Organization with `enabled: true` and its latest migration id; every response has `X-FinCore-Version`.
- **AC-19** Given `Modules:Organization:Enabled=false`, then Organization routes return 404, its jobs/subscriptions are not registered, and `/api/version` reports `enabled: false`.
- **AC-20** Given a request with `X-Correlation-Id: abc`, then the response echoes it and the request's logs carry it.

**Gateway**
- **AC-21** Given the BackOffice profile, when `GET /bff/user`, then 401; when `GET /api/v1/institutions` without `X-CSRF: 1`, then 400; with it, the call is proxied to the API.
- **AC-22** Given the Mobile profile, when a request carries `X-App-Version: mobile/0.0.9 (1)` and minimum is `0.1.0`, then 426; with `mobile/0.1.0 (1)`, `/health` is proxied and returns 200.

**Frontend**
- **AC-23** Given `frontend/`, when `pnpm install --frozen-lockfile && pnpm lint && pnpm typecheck && pnpm test && pnpm build` run, then all succeed.
- **AC-24** Given the committed OpenAPI snapshot, when `pnpm api:generate` runs, then `packages/api-client` has no diff; the snapshot export also produces no diff.
- **AC-25** Given Back Office and Portal running against the gateway, when opened, then each renders an empty home with a signed-out state (from `/bff/user` 401) and a footer `v<version> · <sha> · <env>`; one Playwright smoke test per app passes.
- **AC-26** Given the Expo app running locally against the Mobile Gateway, when opened, then the home screen shows the `/health` status; `tsc`, Jest smoke test and `expo-doctor` pass.

**Local stack & DevOps code**
- **AC-27** Given Docker, when `dotnet run --project src/Aspire/FinCore.AppHost`, then Postgres, Keycloak (realm `fincore`, 4 clients, `sysadmin`), Seq, API, 3 gateway profiles and both Vite apps start and are healthy; `deploy/docker-compose.yml` brings up Postgres, Keycloak and Seq equivalently.
- **AC-28** Given `infra/`, when `terraform fmt -check` and `terraform validate` run for `bootstrap/` and `envs/staging` (backend disabled), then both pass; nothing is applied.
- **AC-29** Given the workflows, then `ci.yml` covers backend, web, mobile, infra validation and commit-message checks; `release-please.yml` + config/manifest match master §33.7.2; `cd-staging.yml` exists with `workflow_dispatch` only.
- **AC-30** Given the repo root, then `CLAUDE.md`, `AGENTS.md`, `.claude/settings.json`, ADRs 0001–0004, `docs/skills.md` (skill discovery result), `.github/pull_request_template.md` and `docs/FinCore-Project-Spec.md` exist.

## Deferred Decisions
| Decision | Chosen fallback for M0 | Revisit trigger |
|---|---|---|
| Real OIDC login, cookie sessions, token storage | Stubs; 401 from `/bff/user` | M1 spec |
| Permission source (claims transformation) | None → fail closed outside Development | M1 Identity |
| Redis / RabbitMQ / MailPit in local stack | Not included | First milestone needing each |
| Persona users in Keycloak | Only `sysadmin` | M1 Identity |
| EAS dev-client builds | Not built; `eas.json` written | Owner creates Expo account |
| AWS deployment, full Terraform modules, CD/prod workflows | Skeleton + `cd-staging.yml` dispatch-only | Separate AWS spec |
| Holiday-aware business-date validation | Weekends only | M1 holiday calendar |

## Open Questions
_None._
