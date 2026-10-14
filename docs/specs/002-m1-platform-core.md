# Spec 002: M1 — Platform Core (Organization, Identity & Approvals, Sign-in)

**Status:** Approved (2026-10-14)
**Date:** 2026-10-14
**Master spec:** `docs/FinCore-Project-Spec.md` — §12, §13, §24, §30.3, §31.4, §35.4 (M1).
**Builds on:** spec 001 (M0, approved and merged).
**Sibling spec:** 003 — M1 design system & UI shells (written after this one is approved).

## Problem
After M0 the platform runs but nobody can really use it: there is no sign-in, module endpoints are open only in
Development and refuse everything elsewhere, institutions have no branches or holidays, and there is no way to
control who may do what. Every later milestone (customers, accounts, loans) needs staff identities, permissions,
branch scope and a maker-checker approval engine.

## Scope

### In
- M1 platform core, **code only** (no AWS; Cognito stays configuration-ready but unused).
- **Split with spec 003:** 002 delivers the backend modules, APIs, authentication and sign-in plumbing in all three
  clients, and proves the M1 exit criteria through API-level tests. All screens (Back Office shell, admin and
  approval screens) are spec 003, built on 002's frozen API contract.
- **Sign-in for all three clients:**
  - Back Office: full staff sign-in through the BFF (cookie session, OIDC with Keycloak), with permissions.
  - Portal and mobile: real customer sign-in (BFF cookie session / mobile Authorization Code + PKCE) that proves
    identity only. Customer records and customer-scoped authorization arrive with M2.
- **Maker-checker:** a generic approval engine, exercised in M1 by **staff role grants and approval-limit changes**
  (a second admin must approve). The engine is reusable for loans, journals and withdrawals later.
- **Staff provisioning:** an admin pre-registers a StaffUser in FinCore by email (institution, home branch, roles);
  the login account is created in the identity provider separately (realm export in dev). On first sign-in the
  provider's `sub` is linked to the StaffUser by verified email. No identity-provider admin API.
- Organization completed: branches (3-digit codes, one-level hierarchy), holiday calendars with configurable weekend
  days, institution lifecycle (suspend / reactivate / close).
- Identity & Approvals module: staff users, fixed roles and permissions, branch scope, approval limits, the generic
  maker-checker engine (including the cross-module event contract), and a platform-wide audit log.
- Real authentication in the API (two realms) replacing spec 001's Development-only anonymous access.
- Dev seed data for every persona and matching dev Keycloak realms.

### Out
- Design tokens, `packages/ui` components, mobile UI kit, Back Office shell visuals, Storybook / Design Gallery
  (spec 003).
- EOD orchestration and business-date advance via EOD (M5).
- Any AWS deployment.

## Domain Model

### Organization (completing master §12)
**Branch** (aggregate, `org.branches`)
| Field | Rule |
|---|---|
| `Id` | `BranchId` (Guid v7) |
| `InstitutionId` | owning institution; plain uuid within the same module (FK allowed: same schema) |
| `Code` | exactly **3 digits** (e.g. `001`), **unique per institution**, **immutable** — feeds the 13-digit account number from M4 (§15) |
| `ParentBranchId` | optional; parent must be in the same institution; **max depth 2** (a branch with a parent cannot itself be a parent); no cycles |
| `Name` | required, 2–100 chars |
| `Address` | optional free text, ≤ 300 chars (structured addresses arrive with Customers in M2) |
| `Status` | `Active` \| `Closed`; closing is allowed only when no active staff user has it as home branch |

**HolidayCalendar** (per institution)
- `WeekendDays`: configurable per institution, default Saturday + Sunday.
- `Holidays`: list of (date, name).
- `PUT /institutions/{id}/holidays` replaces the **future** holidays and the weekend days in one call; past holidays
  (before the current business date) are immutable.
- A shared `IHolidayCalendar` contract (Organization.Contracts) answers "is this a working day?" and
  "next working day after X" for other modules.

**Institution lifecycle** (extends spec 001)
- New endpoints: suspend, reactivate, close.
- `Suspended`: the institution's staff can read but not change data.
- `Closed`: final; only allowed when the institution has no active branches; its staff can no longer sign in.

### Identity & Approvals (new module, master §13, schema `identity`)
**StaffUser** (aggregate): `Id`, `ExternalSubjectId` (IdP `sub`, set on first sign-in), `InstitutionId` (null for
PlatformAdmin), `HomeBranchId`, `Name`, `Email` (unique platform-wide, case-insensitive), `Status`
(`PendingFirstLogin` → `Active` ⇄ `Deactivated`), `Roles[]`, `ApprovalLimits[]`. Reactivation restores the user
with **no roles**; roles are granted again through approval.

**Tenancy**
- **PlatformAdmin** is a platform-level role not tied to an institution: it creates, suspends, reactivates and closes
  institutions and registers each institution's first SystemAdmin.
- Every other staff user belongs to **exactly one institution** and only ever sees that institution's data.

**Roles and permissions** — fixed in M1, seeded, not editable:
| Role | Permissions (from §13, plus M1 additions) |
|---|---|
| `PlatformAdmin` | platform.institutions.manage (create/suspend/reactivate/close institutions), registering an institution's first SystemAdmin, identity.approve in any institution (fallback checker) |
| `SystemAdmin` | org.manage (own institution's branches, holidays), identity.manage, identity.approve, products.manage |
| `BranchOfficer` | customers.create, kyc.capture, accounts.open, accounts.transact |
| `LoanOfficer` | loans.apply, loans.assess, loans.repay |
| `FieldOfficer` | mfi.groups.manage, mfi.collections.record |
| `CreditApprover` | loans.approve, cards.limit.approve |
| `ComplianceOfficer` | kyc.approve, aml.review |
| `Accountant` | ledger.journal.create, ledger.read, eod.run |
| `Auditor` | every `*.read` permission |
A staff user may hold several roles; effective permissions are the union.

**Permission freshness:** permissions are loaded from the Identity database per request (briefly cached) and the
cache entry for a user is cleared whenever that user's roles, limits, status or institution status change —
**revocation is immediate**.

**Branch scope:** each staff user has **one home branch**. The current-user context exposes institution, home
branch and whether the user holds `scope.all-branches`. Branch-owned data is filtered by it; in M1 that is branch
and staff listings (customers etc. follow in M2+).

> **Change to master spec (made in the same change as this spec):** creating, suspending, reactivating and closing
> institutions require the new `platform.institutions.manage` permission (PlatformAdmin only) instead of
> `org.manage` (§12); `PlatformAdmin` and `identity.approve` are added to §13.

**Maker-checker engine** (`ApprovalRequest`, generic for later loan/journal/withdrawal approvals)
- **Needs a checker in M1:** granting a role and raising an approval limit. **Applies immediately (no checker):**
  removing a role, lowering a limit, deactivating a staff user — reducing access never waits.
- New staff users are registered **with no roles**; roles are then granted through approval.
- **Bootstrap exception (added after approval, owner decision 2026-10-14):** only while an institution has **no
  active SystemAdmin**, a PlatformAdmin may register its first SystemAdmin **with** the SystemAdmin role, applied
  immediately and written to the audit log. Once one SystemAdmin exists, every grant goes through maker-checker.
- **Checker:** a different user holding `identity.approve` in the same institution (another SystemAdmin), or a
  PlatformAdmin as fallback so a one-admin institution is never deadlocked.
- **Four-eyes:** the maker can never approve their own request, and nobody can approve a change to their own roles
  or limits.
- **Expiry:** pending requests expire after **7 calendar days** (daily job). Wall-clock time is used because the
  business date cannot advance until EOD (M5).
- **Lifecycle:** `Pending → Approved | Rejected | Withdrawn | Expired | Failed` (Failed = approved but no longer
  applicable when executed; see edge cases). The maker may withdraw a pending request.
  Rejection requires a reason (shown to the maker); an approval comment is optional. Every decision records who and
  when (`approval_decisions`).
- **Multiple checkers:** each operation type has a required-checkers count and an optional amount threshold.
  M1 configuration: role grant → 1 checker; approval-limit increase → 1 checker, **2 checkers when the new limit is
  above 1,000,000** (configurable, in the institution's base currency). The request is approved when the required
  number of distinct eligible checkers have approved; one rejection rejects it.
- **PlatformAdmin completes alone (added after approval, owner decision 2026-10-14):** a PlatformAdmin's approval
  satisfies every required checker on its own. Without this, limits above the two-checker threshold could never be
  granted in a deployment with one PlatformAdmin, because no one else starts with enough authority.

**Approval limits** (`ApprovalLimit`)
- Types: `LoanApproval`, `CardLimit`, `ManualJournal`, `Withdrawal` (master §13).
- Each is a `decimal` maximum in the **institution's base currency**.
- To approve a request that raises someone's limit, the checker's own limit of that type must be **≥ the new limit** —
  nobody grants more authority than they hold. PlatformAdmin has no limits and is exempt, so an institution's first
  admin can be set up.

## API Contract
Conventions as in spec 001 (`/api/v1`, ProblemDetails with `code`, paging envelope, camelCase, enums as strings).
Two new OpenAPI documents: `organization` (extended) and `identity`.

### Organization
| Verb | Route | Purpose | Permission |
|---|---|---|---|
| POST | `/institutions` | create (changed permission, see master-spec change) | `platform.institutions.manage` |
| POST | `/institutions/{id}/suspend` · `/reactivate` · `/close` | lifecycle | `platform.institutions.manage` |
| GET | `/institutions`, `/institutions/{id}` | as spec 001; staff see only their own institution | `org.read` |
| POST | `/institutions/{id}/branches` | create branch `{ code, name, address, parentBranchId? }` | `org.manage` |
| GET | `/institutions/{id}/branches?page=&pageSize=` | list (branch-scoped) | `org.read` |
| GET | `/institutions/{id}/branches/{branchId}` | get | `org.read` |
| PUT | `/institutions/{id}/branches/{branchId}` | update name/address/parent (code immutable) | `org.manage` |
| POST | `/institutions/{id}/branches/{branchId}/close` | close (no active staff with it as home branch) | `org.manage` |
| GET | `/institutions/{id}/holidays?year=` | weekend days + holidays | `org.read` |
| PUT | `/institutions/{id}/holidays` | replace future holidays + weekend days | `org.manage` |
| GET | `/institutions/{id}/business-date` | as spec 001 | authenticated |

### Identity & Approvals
| Verb | Route | Purpose | Permission |
|---|---|---|---|
| GET | `/me` | current user (shape below) | authenticated (staff or customer) |
| POST | `/staff` | pre-register staff `{ email, name, homeBranchId }` (no roles). PlatformAdmin passes `{ institutionId, bootstrapSystemAdmin: true }` for an institution's first SystemAdmin (bootstrap exception) | `identity.manage` or `platform.institutions.manage` |
| GET | `/staff?search=&branchId=&page=` | list (branch-scoped) | `identity.manage` |
| GET | `/staff/{id}` | get incl. roles, limits, status | `identity.manage` |
| POST | `/staff/{id}/roles` | request role grant → **202** + approval request | `identity.manage` |
| DELETE | `/staff/{id}/roles/{role}` | remove role (immediate) | `identity.manage` |
| PUT | `/staff/{id}/approval-limits/{type}` | raise → **202** approval request; lower → 200 immediate | `identity.manage` |
| POST | `/staff/{id}/deactivate` · `/reactivate` | deactivate immediate; reactivate restores with **no roles** | `identity.manage` |
| GET | `/approvals?status=Pending` | inbox: only requests the caller may decide | `identity.approve` |
| GET | `/approvals?mine=true` | requests the caller made | authenticated staff |
| GET | `/approvals/{id}` | full payload, decisions, status | maker, eligible checker, or `identity.approve` |
| POST | `/approvals/{id}/approve` | `{ comment? }` | eligible checker |
| POST | `/approvals/{id}/reject` | `{ reason }` (required) | eligible checker |
| POST | `/approvals/{id}/withdraw` | maker only, while Pending | maker |

**`/me` and `/bff/user` (staff):** `{ id, kind: "staff", name, email, institution { id, code, name, status },
homeBranch { id, code, name }, roles[], permissions[], allBranches, businessDate, enabledModules[] }`.
PlatformAdmin: `institution` and `homeBranch` are null. **Customers (M1):** `{ id, kind: "customer", name, email }`.

### BFF (Back Office, Portal)
| Route | Behavior |
|---|---|
| `GET /bff/login?returnUrl=` | starts OIDC sign-in; local return URLs only (as spec 001) |
| `GET /signin-oidc` | OIDC callback (framework) |
| `POST /bff/logout` | ends the server-side session, revokes the refresh token, signs out at the IdP |
| `GET /bff/user` | 401 when signed out; otherwise the `/me` shape |
| `/api/*` | proxied with the session's access token attached; CSRF header still required |

## Authorization
- **Permissions** come from the Identity module via a claims transformation (staff tokens): roles → permissions,
  cached per user, cache cleared on any change to that user, their institution status or roles (immediate revocation).
- **Spec 001's Development-only anonymous access is removed**: every module endpoint requires a signed-in user with
  the listed permission in every environment.
- **Institution isolation:** staff only see and change data of their own institution (checked centrally from the
  current-user context); PlatformAdmin is the only cross-institution role.
- **Branch scope:** listings of branch-owned data filter to the home branch unless the user holds
  `scope.all-branches` (granted to SystemAdmin and Auditor).
- **Suspended institution:** a central requirement on every state-changing endpoint returns 403
  `Organization.InstitutionSuspended` for that institution's staff.
- **Customers** (customer realm tokens) can call only `/me` in M1; every staff endpoint rejects them (403).

## Edge Cases & Failure Modes
| Case | Behavior |
|---|---|
| IdP email matches no pre-registered staff user | Sign-in refused: 403 `Identity.StaffNotRegistered`; Back Office shows "contact your administrator" |
| IdP email not verified | 403 `Identity.EmailNotVerified` |
| Email changed at the IdP after linking | No effect: once linked, the IdP `sub` is the key; email is never re-matched |
| Two staff users pre-registered with the same email | Email is **unique platform-wide** (case-insensitive): one email = one staff user = one institution → 409 `Identity.StaffEmailTaken` |
| Staff of a Closed institution, or a deactivated staff user, signs in | 403 `Identity.StaffInactive`; existing sessions stop working on the next request (permission cache cleared) |
| Write request by staff of a Suspended institution | 403 `Organization.InstitutionSuspended`, enforced centrally on every state-changing endpoint |
| Approval granted but no longer applicable (target deactivated, role already held, limit already ≥ new value) | Re-validated at execution; request ends `Failed` with a reason code; nothing changes |
| Two checkers decide the same request concurrently | `xmin` concurrency: the second gets 409 `Identity.ApprovalAlreadyDecided` |
| Maker tries to approve own request / anyone approves a change to their own access | 403 `Identity.FourEyesViolation` |
| Checker's limit lower than the requested new limit | 403 `Identity.ApprovalLimitExceeded` |
| Request decided after expiry | 409 `Identity.ApprovalExpired` |
| Branch code duplicate within institution | 409 `Organization.BranchCodeTaken` (unique index, race-safe as in spec 001) |
| Parent branch in another institution, itself has a parent, or would create a cycle | 400 `errors.parentBranchId` |
| Close institution with active branches | 409 `Organization.InstitutionHasActiveBranches` |
| Holiday update touching a date before the current business date | 400 `errors.holidays` (past holidays are immutable) |
| Removing a role / lowering a limit / deactivating a user | Applies immediately, no approval; permission cache for that user cleared |

## Non-Functional Requirements
- All spec 001 NFRs carry over (warnings as errors, ≥ 80 % domain coverage, architecture tests, §34.1 comments).
- **Permission cache:** in-memory `HybridCache`, 5-minute entries per user, explicitly cleared on any access change.
  Single API instance in M1; a distributed backplane (Redis) is added when the API scales out.
- **Audit log:** a platform-wide `AuditBehavior` writes one row per successful command to `platform.audit_log`:
  user, institution, command name, entity id, correlation id, request hash, UTC time. **No payloads** (PII).
- **Rate limiting:** only `/bff/login` (10 requests/min per IP) in M1; the rest waits for the M9 hardening milestone.
- **Security:** BFF cookies `__Host-`, HttpOnly, Secure, SameSite=Strict; tokens never reach browser JavaScript;
  mobile refresh tokens only in SecureStore.
- **Repository path:** the checkout path must not contain `#` (Vite dev server and Vitest break on it). The owner
  renames the local folder before spec 003's UI work; the README documents the rule.

## Integrations

### Integration events (minimal set — each has a current or planned consumer)
| Event | Publisher | Consumer(s) |
|---|---|---|
| `InstitutionCreated` (existing) | Organization | Identity (local institution-status copy) |
| `InstitutionStatusChanged` | Organization | Identity (status copy; clears permission caches of that institution's staff) |
| `BranchCreated`, `BranchClosed` | Organization | Reporting (M8) — published now, no consumer yet in M1 |
| `StaffAccessChanged` | Identity | Identity itself (cache invalidation); Notifications later |
| `ApprovalRequested` | any requesting module | Identity creates the approval request |
| `ApprovalRequestDecided` | Identity | the requesting module (§10 catalog) |

**Cross-module approval contract (decided now, used from M3):** a module that needs approval publishes
`ApprovalRequested { operationType, module, entityId, amount?, currency?, payload, makerId }`; Identity creates the
request, applies the four-eyes, limit and checker-count rules, and publishes `ApprovalRequestDecided { requestId,
entityId, operationType, outcome, decidedBy[], reason? }`, which the requesting module handles idempotently. M1
proves this path end to end with a **test-only operation**; Identity's own role and limit approvals use the same
engine in-process.

**Institution status in Identity:** Identity keeps its own small `identity.institutions` table updated from
Organization's events, so sign-in and write checks never call Organization synchronously.

### Identity provider (Keycloak locally, Cognito-ready)
- **Two realms**, mirroring the two Cognito pools on AWS (§33.3): `fincore-staff` (clients `fincore-api`,
  `fincore-backoffice`) and `fincore-customers` (clients `fincore-api-customers` audience, `fincore-portal`,
  `fincore-mobile`). Replaces M0's single `fincore` realm, so a customer token can never pass as a staff token.
- The API trusts both issuers and tells staff from customers by issuer.
- No identity-provider admin API is used (staff accounts are created in the IdP separately).

### BFF sessions (Back Office, Portal)
- OIDC Authorization Code + PKCE against the realm for that app; the cookie (`__Host-`, HttpOnly, Secure,
  SameSite=Strict) holds only a session id.
- Access and refresh tokens are stored **server-side in Postgres** (platform schema), encrypted with ASP.NET Core
  Data Protection whose keys are also stored in Postgres — sessions survive restarts, work across gateway instances
  and can be revoked server-side (§30.3, §33.5).
- Access tokens live 5 minutes and are refreshed silently by the BFF.

### Session lifetimes
| Who | Idle (sliding) | Absolute max |
|---|---|---|
| Staff (Back Office) | 30 min | 10 h |
| Customers (Portal, mobile) | 15 min | 8 h |

### Mobile sign-in (M1 part of §31.4)
- Authorization Code + PKCE via the system browser; refresh token in SecureStore (Keychain/Keystore), access token in
  memory only; single-flight refresh; sign-out clears storage and revokes the refresh token.
- **Later:** biometric app lock, step-up, device registration and push (with the features that need them, M4/M8).

## Delivery & Version Control
- Branch `wp/M1-platform-core`; commits per §35.7; spec 001 conventions. Master spec §12/§13 updated in the same change.
- Skill discovery for M1 (§35.2.4) is recorded in `docs/skills.md` at kickoff.

## Acceptance Criteria

**Sign-in & identity**
- **AC-1** Given a pre-registered staff user whose IdP account has the same verified email, when they sign in to the Back Office for the first time, then their IdP `sub` is linked, status becomes `Active`, and `/bff/user` returns the staff `/me` shape with their institution, home branch, roles and permissions.
- **AC-2** Given an IdP account whose email matches no staff user (or is unverified), when it signs in, then FinCore returns 403 `Identity.StaffNotRegistered` (or `Identity.EmailNotVerified`) and no session with permissions is created.
- **AC-3** Given a signed-in staff user, then the session cookie is `__Host-`, HttpOnly, Secure, SameSite=Strict, the browser never receives a token, `/api` calls are proxied with the access token, and after 30 minutes idle (or 10 h total) `/bff/user` returns 401.
- **AC-4** Given a customer in the `fincore-customers` realm, when they sign in to the Portal (BFF) or the mobile app (PKCE), then `/me` returns `{ kind: "customer" }`; any staff endpoint returns 403; a staff-realm endpoint never accepts the customer token.
- **AC-5** Given the mobile app signed in, then the refresh token is stored only in SecureStore, the access token is refreshed single-flight when it expires, and sign-out revokes the refresh token and clears storage.
- **AC-6** Given any environment including Development, when a module endpoint is called without a valid token or without the listed permission, then 401 / 403 (spec 001's dev-only anonymous access is gone).

**Organization**
- **AC-7** Given a PlatformAdmin, when they create, suspend, reactivate and close institutions, then each transition succeeds only from a valid state; closing with active branches → 409 `Organization.InstitutionHasActiveBranches`; a SystemAdmin calling these gets 403.
- **AC-8** Given a SystemAdmin, when they create branches `001` and `002` and branch `003` with parent `001`, then codes are unique per institution (duplicate → 409 `Organization.BranchCodeTaken`, also under concurrency); a parent in another institution, a parent that itself has a parent, or a non-3-digit code → 400.
- **AC-9** Given an institution, when a SystemAdmin PUTs weekend days Fri+Sat and three holidays, then `IHolidayCalendar` reports those days as non-working and gives the correct next working day; a holiday before the current business date → 400.
- **AC-10** Given a Suspended institution, when its staff call any state-changing endpoint, then 403 `Organization.InstitutionSuspended`, while reads still work; after reactivation writes work again. Given a Closed institution, its staff cannot sign in (403 `Identity.StaffInactive`).

**Identity & approvals**
- **AC-11** Given SystemAdmin A, when A registers staff user U (no roles) and requests the `LoanOfficer` role for U, then the API returns 202 with an approval request and U has no new permission yet.
- **AC-12** Given that request, when A tries to approve it → 403 `Identity.FourEyesViolation`; when SystemAdmin B approves it, then U gains the role and its permissions on U's **next request** (cache cleared), and the decision is recorded with who/when.
- **AC-13** Given a request to raise U's `LoanApproval` limit to 500,000, when checker B's own limit is 200,000 → 403 `Identity.ApprovalLimitExceeded`; when checker C with 1,000,000 approves → applied. A raise to 2,000,000 needs **two** distinct eligible checkers; one rejection (with a reason) rejects it.
- **AC-14** Given a pending request, when the maker withdraws it → `Withdrawn`; when 7 days pass → `Expired` by the daily job; deciding an expired request → 409 `Identity.ApprovalExpired`.
- **AC-15** Given two checkers approving a single-checker request at the same moment, then exactly one succeeds and the other gets 409 `Identity.ApprovalAlreadyDecided`.
- **AC-16** Given an approved role grant whose target user was deactivated in the meantime, then the request ends `Failed` with a reason and nothing changes.
- **AC-17** Given removing a role, lowering a limit or deactivating a user, then it applies immediately (no approval) and the user's very next request reflects it.
- **AC-18** Given `/approvals?status=Pending` for checker B, then it lists only requests B may decide (not B's own, not about B, not above B's limit); `?mine=true` lists the caller's own requests.
- **AC-19** Given a branch-scoped user without `scope.all-branches`, when listing staff or branches, then only their home branch's records are returned; a SystemAdmin sees all branches of their institution; nobody except PlatformAdmin sees another institution's data.
- **AC-20** Given the test-only operation, when a module publishes `ApprovalRequested`, then Identity creates a request, and after approval publishes `ApprovalRequestDecided`, which the test module handles exactly once.

**Platform**
- **AC-21** Given any successful command, then one `platform.audit_log` row records user, institution, command, entity id, correlation id and time, and contains no request payload.
- **AC-22** Given Development with empty databases, then the seeder creates 1 PlatformAdmin, for FCB and FCMFI each 2 branches (001 HQ, 002), 2 SystemAdmins and one user per other role, Kenyan 2026 holidays, and 2 customers in the customer realm; matching Keycloak users exist in the two realm exports; re-running creates no duplicates.
- **AC-23** Given the M1 exit scenario as an end-to-end API test: PlatformAdmin creates a Bank and an MFI and their first SystemAdmins; each SystemAdmin creates branches and registers staff; a role grant is approved by a second admin; the new role works — all through the real HTTP API with **signed test tokens** (an in-test issuer whose signing key the test host trusts, carrying the same claims Keycloak issues). Keycloak itself is exercised manually through the AppHost.
- **AC-24** `dotnet build -warnaserror`, all tests, architecture tests (Identity module boundaries included), coverage gate, OpenAPI snapshots (`organization.json`, `identity.json`) and the regenerated `packages/api-client` are clean in CI.

## Deferred Decisions
| Decision | Chosen fallback for M1 | Revisit trigger |
|---|---|---|
| Custom (editable) roles | Fixed seeded roles | Owner request |
| Approval expiry in business days | 7 calendar days | EOD advances the business date (M5) |
| Distributed permission cache | In-memory HybridCache, single API instance | API scales to > 1 instance (M9) |
| Rate limiting beyond `/bff/login` | None | M9 hardening |
| Biometric app lock, step-up, device registration, push | Not built | M4 (transfers) / M8 (notifications) |
| Customer records and customer-scoped authorization | Customers are identity-only | M2 |
| Multi-institution staff | Not supported (email unique platform-wide) | Owner request |
| Multi-currency approval limits | Base currency only | Multi-currency products |

## Deferred Decisions
_None yet_

## Open Questions
_None._
