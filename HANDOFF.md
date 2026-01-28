# FinCore — Handoff

**Read this first.** This file is the single starting point for anyone — a person or any AI coding assistant — picking
up FinCore on any machine. It says where the project stands, what to read, which rules apply and what to do next.

> **Keep it current.** Every change that lands on a branch updates this file in the same commit series
> (see [Update protocol](#update-protocol)). If this file and the code disagree, the code and the approved specs win —
> then fix this file.

_Last updated: 2026-10-14 · by: project owner_

---

## 1. Current state at a glance

| Item | Value |
|---|---|
| Repository | https://github.com/cliff-gor/FinCore |
| Default branch | `main` (M0 merged) |
| Active branch | `wp/M1-platform-core` (spec 002 complete, pushed, **not yet merged**) |
| Latest commit on active branch | `5952ec3 fix(identity): close two races in first sign-in and the institution status copy` (followed by the CI/handoff commit — check `git log -1`) |
| Milestone | **M1 — Platform core & design system**, split into spec 002 (done) and spec 003 (not started) |
| Next work package | Write **spec 003: M1 design system & UI shells** (tokens, `packages/ui`, mobile UI kit, Back Office shell, admin & approval screens, Storybook / Design Gallery) |
| Build | `dotnet build FinCore.slnx -warnaserror` green |
| Tests | 197 .NET tests green (locally on Postgres 16; awaiting a CI rerun on 17); frontend lint / typecheck / build / Playwright green; mobile Jest + expo-doctor green |

### Milestone status
| Milestone | Spec | Status |
|---|---|---|
| M0 Foundations & walking skeleton | `docs/specs/001-m0-foundations.md` | ✅ Done, merged to `main` |
| M1a Platform core (Organization, Identity & Approvals, sign-in) | `docs/specs/002-m1-platform-core.md` | ✅ Done on `wp/M1-platform-core` |
| M1b Design system & UI shells | `docs/specs/003-…` (to be written) | ⏳ Next |
| M2 – M9 | master spec §35.4 | Not started |

---

## 2. Read these, in this order

1. **This file.**
2. [`CLAUDE.md`](CLAUDE.md) / [`AGENTS.md`](AGENTS.md) — working rules for coding assistants (identical content).
3. [`docs/FinCore-Project-Spec.md`](docs/FinCore-Project-Spec.md) — the master specification (source of truth).
   Most-used sections: §6 architecture rules, §8 module anatomy, §10 inter-module communication, §28 API conventions,
   §32 design system, §34 coding & comment conventions, §35 build plan, §35.6 Definition of Done, §35.7 commits.
4. The **approved milestone specs** in [`docs/specs/`](docs/specs/) — the latest one is the contract for current work.
5. The **ADRs** in [`docs/adr/`](docs/adr/) — why the architecture is the way it is (0001–0005).
6. [`docs/dev-accounts.md`](docs/dev-accounts.md) — dev users for Keycloak and the seeders.
7. [`docs/skills.md`](docs/skills.md) — which assistant skills are approved per area.
8. [`README.md`](README.md) — how to run, test and the local URLs.

---

## 3. Getting set up on a new machine

```bash
git clone https://github.com/cliff-gor/FinCore.git
cd FinCore
git checkout wp/M1-platform-core        # or main once M1 is merged
git config core.hooksPath .githooks     # enables the commit-message hook
dotnet tool restore
(cd frontend && corepack enable && pnpm install)
dotnet build FinCore.slnx -warnaserror
dotnet test --solution FinCore.slnx     # needs Docker for integration tests
```

**Known machine-specific issues**
- The checkout path **must not contain `#`** (Vite dev server and Vitest break on it). Example: use
  `~/code/FinCore`, not `~/C#-Projects/FinCore`.
- Integration tests use Testcontainers with `postgres:17-alpine`. If that image can't be pulled, set
  `FINCORE_TEST_POSTGRES_IMAGE=postgres:16-alpine` (and `TESTCONTAINERS_RYUK_DISABLED=true` if the Ryuk image is missing).
- Requirements: .NET 10 SDK (pinned in `global.json`), Node 22/24 with Corepack, Docker.

---

## 4. Rules that must never be broken

### 4.1 Process (master §35.1, §35.5)
- **Spec first.** Every milestone (or half-milestone) gets a spec in `docs/specs/NNN-*.md` that the owner approves
  before code. Code follows the approved spec; changes go through the spec first and are recorded in it.
- **One work package at a time; no scope invention; no new dependencies without owner approval.**
- **Ask the owner** when a business rule, money calculation, security or data-model question is unclear.
- **Done = Definition of Done** (master §35.6), not "it compiles".

### 4.2 Architecture (master §6, §8, §10, §11; ADRs 0001–0005)
- Modular monolith: each module has five projects — `Domain`, `Application`, `Infrastructure`, `Presentation`, `Contracts`.
- A module references **only other modules' `.Contracts`**. `Domain` and `Contracts` depend only on `SharedKernel`.
  Enforced by `tests/FinCore.ArchitectureTests`.
- Modules communicate through **read-only contract APIs** and **integration events via the transactional outbox**
  (ADR-0003). No cross-module transactions, no cross-schema foreign keys.
- One PostgreSQL database, **one schema per module**, one `DbContext` per module, migrations per module.
- Business failures return `Result`; endpoints map them to RFC 9457 ProblemDetails with a stable `code`.
- Handlers, validators and EF configurations are `internal sealed`.
- Authentication: the identity provider proves *who*; FinCore's Identity module decides *what* (ADR-0005). Every module
  endpoint requires a signed-in user with a permission; staff only see their own institution.

### 4.3 Money and dates
- Money is `decimal` (C#) / `decimal.js` (TypeScript) — never `double`, `float` or JS `number`. Banker's rounding (ADR-0004).
- Financial logic uses the institution's business date (`IBusinessDateProvider`), never the wall clock.
- Money-moving endpoints require an `Idempotency-Key`.

### 4.4 Code comments (master §34.1)
Explain *why*, not *what*: business and money rules with their spec reference, concurrency, idempotency, security and
deliberate deviations. XML docs / TSDoc on public APIs. `TODO(M<n>)` must name the milestone. No commented-out code.

### 4.5 Commits and branches (master §35.7)
- One work package = one branch `wp/<milestone>-<name>` = one pull request into `main`; the owner merges.
- Conventional Commits with the plain-English body:
  ```
  <type>(<scope>): <summary>

  What changed:
  - …

  Why:
  …

  How to check:
  - …

  Refs: WP-…, spec …
  ```
- Types: `feat`, `fix`, `test`, `refactor`, `docs`, `chore`, `ci`, `infra`, `style`, `perf`.
- Generated code (EF migrations, OpenAPI snapshots, `packages/api-client`) goes in its own `chore` commit.
- Commits use the repository's configured git identity. Never change git config, never pass `--author`, never use
  `--no-verify`. The commit-msg hook rejects attribution trailers.

---

## 5. Repository map (where things live)

| Path | What |
|---|---|
| `src/BuildingBlocks/` | SharedKernel, dispatcher & behaviors, outbox/inbox, platform DB, auth plumbing |
| `src/Modules/Organization/` | Institutions, branches, holiday calendars, business date |
| `src/Modules/Identity/` | Staff, roles, permissions, approval limits, maker-checker, claims transformation |
| `src/Bootstrapper/FinCore.Api/` | The single API host (composition root) |
| `src/Gateway/FinCore.Bff/` | BFF for Back Office & Portal (server-side sessions) + Mobile Gateway |
| `src/Tools/FinCore.Migrator/` | Applies every module's migrations (deployments) |
| `src/Aspire/` | AppHost (local orchestration) and ServiceDefaults |
| `tests/` | Unit, architecture, integration (Testcontainers) and gateway tests |
| `frontend/` | pnpm monorepo: `apps/backoffice`, `apps/portal`, `apps/mobile`, `packages/*` |
| `openapi/` | Committed OpenAPI snapshots (input for the frontend client) |
| `deploy/` | docker-compose, Keycloak realms (staff, customers), Postgres init |
| `infra/` | Terraform (skeleton; nothing applied) |
| `.github/` | CI, release-please, disabled staging CD |

---

## 6. Open items and known gaps

| Item | Status / next step |
|---|---|
| CI rerun after the race fixes | Owner to push and confirm the `dotnet` and `mobile` jobs are green |
| Merge `wp/M1-platform-core` into `main` | Owner to review and merge once CI is green |
| Spec 003 (design system & UI shells) | Not started — next work package |
| Real Keycloak sign-in end to end | Not yet verified (image pulls failed on the last machine); realms are in `deploy/keycloak/` |
| Full stack through Aspire | Not yet run end to end |
| Vitest locally | Blocked by `#` in the checkout path; runs in CI |
| Integration tests on Postgres 17 | Verified on 16 locally; CI uses 17 |
| Mobile app on a real device / simulator | Not yet run |
| AWS deployment | Out of scope until an AWS spec is written (Terraform skeleton only) |

---

## 7. Decisions log (most recent first)

| Date | Decision | Where recorded |
|---|---|---|
| 2026-10-14 | `expo-doctor` runs on demand (`pnpm dlx`), not as a project dependency | `.github/workflows/ci.yml` |
| 2026-10-14 | A PlatformAdmin's approval completes any request alone | spec 002, ADR-0005 |
| 2026-10-14 | Bootstrap exception: PlatformAdmin registers an institution's first SystemAdmin directly | spec 002 |
| 2026-10-14 | Integration tests use signed test tokens instead of Keycloak containers | spec 002 AC-23 |
| 2026-10-14 | Two Keycloak realms (staff, customers) mirroring two Cognito pools | spec 002, ADR-0005 |
| 2026-10-14 | M1 split into spec 002 (platform core) and spec 003 (design system & UI) | spec 002 |
| 2026-10-14 | Code comment standard added | master §34.1 |
| 2026-10-13 | M0 scope, Organization thin slice, outbox full loop, MTP test runner | spec 001 |

---

## Update protocol

Whoever makes a change — person or assistant — updates this file **before the work is pushed**:

1. **Section 1:** active branch, latest commit (`git log --oneline -1`), milestone status, next work package, test counts.
2. **Section 6:** add, update or close open items.
3. **Section 7:** add any decision the owner made (one line, with where it is recorded).
4. **`_Last updated_`** line at the top.
5. Commit it with the work: `docs: update handoff for <what changed>` (or include it in the last commit of the series).

A new assistant session should start by reading this file, then run:

```bash
git status
git log --oneline -10
git branch -a
```

and compare the result with Section 1 before doing anything else.
