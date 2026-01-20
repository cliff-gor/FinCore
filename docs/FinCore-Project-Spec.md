# FinCore — Unified Financial Services Platform

> A modular-monolith financial platform built with **.NET 10** and **C# 14**, covering Customer & KYC onboarding, Bank/MFI organization management, Deposit Accounts, Loans, Microfinance Groups, Credit Cards, and a shared double-entry General Ledger.

> **Disclaimer:** FinCore is a learning and portfolio project. It simulates real financial processes but is not certified for production use, regulatory compliance (KYC/AML, PCI DSS, Basel, IFRS 9), or handling real customer money.

---

## Table of Contents

**Part 1 — README**
1. [Overview](#1-overview)
2. [Key Features](#2-key-features)
3. [Technology Stack](#3-technology-stack)
4. [Getting Started](#4-getting-started)
5. [Running Tests](#5-running-tests)

**Part 2 — Architecture**

6. [Architectural Style](#6-architectural-style)
7. [Solution & Folder Structure](#7-solution--folder-structure)
8. [Module Anatomy (Clean Architecture per Module)](#8-module-anatomy-clean-architecture-per-module)
9. [Building Blocks & Shared Kernel](#9-building-blocks--shared-kernel)
10. [Inter-Module Communication](#10-inter-module-communication)
11. [Data Architecture](#11-data-architecture)

**Part 3 — Modules in Detail**

12. [Organization Module (Banks, MFIs, Branches)](#12-organization-module)
13. [Identity & Approvals Module](#13-identity--approvals-module)
14. [Customers & KYC Module](#14-customers--kyc-module)
15. [Accounts Module (incl. Account Opening)](#15-accounts-module)
16. [General Ledger Module](#16-general-ledger-module)
17. [Loans Module](#17-loans-module)
18. [Microfinance Module](#18-microfinance-module)
19. [Cards Module](#19-cards-module)
20. [Notifications Module](#20-notifications-module)
21. [Reporting Module](#21-reporting-module)

**Part 4 — Cross-Cutting Concerns**

22. [End-to-End Business Flows](#22-end-to-end-business-flows)
23. [Financial Calculations](#23-financial-calculations)
24. [Security & Compliance](#24-security--compliance)
25. [Reliability: Idempotency, Concurrency, Outbox](#25-reliability-idempotency-concurrency-outbox)
26. [Background Jobs & End-of-Day Processing](#26-background-jobs--end-of-day-processing)
27. [Observability](#27-observability)
28. [API Conventions](#28-api-conventions)
29. [Testing Strategy](#29-testing-strategy)
30. [Front End (React)](#30-front-end-react)
31. [Customer Mobile App (React Native + Expo)](#31-customer-mobile-app-react-native--expo)
32. [Design System (Fintech UI)](#32-design-system-fintech-ui)
33. [DevOps & AWS Deployment](#33-devops--aws-deployment)
34. [Coding Conventions](#34-coding-conventions)
35. [Development Strategy & Build Plan](#35-development-strategy--build-plan)
36. [Path to Microservices](#36-path-to-microservices)
37. [Glossary](#37-glossary)

---

# PART 1 — README

## 1. Overview

FinCore is a single deployable application (a **modular monolith**) that lets one or more financial institutions — commercial **Banks** and **Microfinance Institutions (MFIs)** — run their core operations:

- Onboard customers through a digital **account opening** flow with **KYC** verification and risk rating.
- Manage **deposit accounts** (savings, current, fixed deposit) with interest accrual.
- Originate, approve, disburse and service **loans** with amortization schedules, repayments, penalties and collections.
- Run **group lending** the way MFIs do: centers, groups, meetings, collection sheets and portfolio-at-risk.
- Issue and service **credit cards**: limits, authorizations, billing cycles, statements, interest and minimum payments.
- Record every money movement in a shared **double-entry General Ledger** with trial balance and reconciliation.

Each business capability lives in its own **module** with strict boundaries, its own database schema, and a public contract. Modules talk to each other only through those contracts and integration events, so any module can later be extracted into a microservice with minimal rework.

### Who uses it

| Persona | What they do |
|---|---|
| Customer (self-service) | Uses the iOS/Android mobile app (or web portal) to open accounts, apply for loans and cards, transfer money, and view balances and statements |
| Branch Officer | Onboards customers, captures KYC documents, opens accounts |
| Loan Officer | Captures loan applications, runs assessments, manages collections |
| Field Officer (MFI) | Manages groups and centers, records meeting collections |
| Credit Approver | Approves loans and card limits within an approval limit (checker) |
| Compliance Officer | Reviews KYC/AML alerts, approves high-risk customers |
| Finance / Accountant | Manages chart of accounts, manual journals, trial balance, EOD |
| System Administrator | Manages institutions, branches, users, roles, products |

## 2. Key Features

**Organization**
- Multi-institution (Bank and MFI types), branches, business-date calendar, holidays, currencies.

**Identity & Approvals**
- OIDC authentication (Keycloak locally, Amazon Cognito on AWS), role- and permission-based authorization, branch scoping.
- Generic **maker-checker** approval engine with monetary approval limits.

**Customers & KYC**
- Individual and business customers, contact and address history.
- KYC document upload and verification, simulated identity and sanctions/PEP screening.
- Customer risk rating (Low / Medium / High) driving due-diligence level.

**Accounts**
- Savings, current and fixed-deposit products, configurable per institution.
- Digital account-opening workflow (application → KYC → approval → account active).
- Deposits, withdrawals, internal transfers, holds/liens, statements, dormancy.
- Daily interest accrual and periodic capitalization.

**General Ledger**
- Chart of accounts, immutable double-entry journal, posting rules per transaction type.
- Trial balance, account balances by business date, sub-ledger reconciliation.

**Loans**
- Loan products (flat / declining-balance interest, frequencies, fees, grace periods).
- Application, credit scoring, approval, disbursement to a deposit account.
- Amortization schedules, repayments with allocation order, penalties, prepayment.
- Delinquency tracking (days past due, aging buckets), write-off, restructuring.

**Microfinance**
- Centers, groups, members, group meetings, joint-liability group loans.
- Collection sheets for field officers, compulsory savings, PAR reporting.

**Cards**
- Card products, credit limit assignment, card issuance (tokenized, simulated PAN).
- Authorization simulation with holds, clearing/posting, disputes.
- Billing cycle, statement generation, interest on revolving balances, minimum payment, late fees, rewards points.

**Platform**
- Transactional outbox, idempotent APIs, optimistic concurrency, full audit trail.
- End-of-day batch, background jobs, notifications (email/SMS stubs), reporting read models.
- OpenTelemetry tracing, structured logging, health checks, .NET Aspire local orchestration.

## 3. Technology Stack

| Concern | Choice | Notes |
|---|---|---|
| Runtime | .NET 10 (LTS), C# 14 | `global.json` pins the SDK |
| Solution format | `.slnx` | XML solution format, default for new solutions in .NET 10 |
| Web API | ASP.NET Core 10 Minimal APIs | Endpoints grouped per module |
| API docs | Built-in `Microsoft.AspNetCore.OpenApi` + Scalar UI | OpenAPI 3.1 |
| Validation | FluentValidation | Wired through a pipeline behavior |
| Request dispatch | In-house lightweight dispatcher (`ICommandHandler<,>`, `IQueryHandler<,>`) | Avoids the commercial license that applies to MediatR v13+; you can swap in a source-generated mediator if preferred |
| ORM | EF Core 10 + Npgsql | One `DbContext` per module |
| Database | PostgreSQL 17 | One schema per module |
| Cache | `HybridCache` (+ Redis locally / optional) | In-memory only on AWS to save resources (§33.5) |
| Messaging (in-process) | Channel-based in-memory event bus + transactional outbox/inbox | Swap to RabbitMQ later |
| Messaging (later) | RabbitMQ | Check the license of any bus library you choose (e.g. MassTransit v9+ is commercial) |
| Background jobs | Quartz.NET | EOD, accruals, statement cycles, outbox processing |
| Auth | OIDC/JWT: Keycloak locally, Amazon Cognito on AWS | Code is IdP-agnostic; permissions come from the Identity module |
| Logging | Serilog → Seq (dev) | Structured logs, correlation IDs |
| Telemetry | OpenTelemetry (traces, metrics) | Exported via Aspire dashboard / OTLP |
| Local orchestration | .NET Aspire AppHost | Spins up Postgres, Redis, Keycloak, Seq, RabbitMQ |
| Front end | React 19 + TypeScript + Vite (pnpm monorepo: Back Office + Customer Portal) | Full details in §30 |
| Mobile app | React Native + Expo (Expo Router, EAS Build/Update) | Customer app for iOS & Android — details in §31 |
| BFF / gateway | ASP.NET Core + YARP | Cookie-session BFF for web apps; token-passthrough Mobile Gateway for the app |
| Tests | xUnit v3, Shouldly, NSubstitute, Bogus, Testcontainers, Respawn, NetArchTest | Shouldly used instead of FluentAssertions v8+ (commercial) |
| CI/CD | GitHub Actions + GitHub OIDC → AWS | Path-filtered deploys to Staging on merge; full deploy to Production on release (§33.8) |
| Versioning | SemVer from Conventional Commits via release-please; GitHub Releases | Two trains: platform and mobile (§33.7) |
| Cloud | AWS: ECS Fargate (ARM64), RDS PostgreSQL, S3 + CloudFront, ALB, Cognito, SES, CloudWatch | Lean footprint, details in §33 |
| Infrastructure as Code | Terraform (S3 backend with native locking) | `infra/` folder |
| UI design | "Soft Ledger" design system (Urbanist + Inter, pine ink + mint, warm neutrals, honey highlight, pastel aura gradients) with shared tokens for web and mobile | Merged from Lendora, Payno, Orla, ZIXO and Northpoint references, details in §32 |
| Containers | Docker / `dotnet publish` container support | |

> Library licenses change over time. Verify the current license of every third-party package before adopting it.

## 4. Getting Started

### Prerequisites
- .NET 10 SDK
- Node.js LTS (22 or 24) with Corepack enabled (`corepack enable` provides pnpm)
- For mobile: Android Studio (emulator) and/or Xcode on macOS (iOS simulator); an Expo account for EAS builds
- Docker Desktop (or Podman)
- An IDE: Visual Studio 2026, JetBrains Rider, or VS Code with C# Dev Kit

### Clone and run (Aspire)

```bash
git clone https://github.com/<you>/FinCore.git
cd FinCore
dotnet restore
dotnet run --project src/Aspire/FinCore.AppHost
```

The Aspire dashboard opens and starts PostgreSQL, Redis, Keycloak, Seq, the API, the two BFF hosts and the two React apps (Back Office and Customer Portal). Run `pnpm install` in `frontend/` once before the first start. Each module applies its own EF Core migrations on startup in the Development environment only.

### Run without Aspire

```bash
docker compose -f deploy/docker-compose.yml up -d
dotnet run --project src/Bootstrapper/FinCore.Api
dotnet run --project src/Gateway/FinCore.Bff --launch-profile BackOffice
dotnet run --project src/Gateway/FinCore.Bff --launch-profile Portal
cd frontend && pnpm install && pnpm dev
```

### Default URLs (Development)

| Service | URL |
|---|---|
| API | https://localhost:7100 |
| API reference (Scalar) | https://localhost:7100/scalar |
| Back Office (Vite dev) | http://localhost:5173 |
| Customer Portal (Vite dev) | http://localhost:5174 |
| BackOffice BFF | https://localhost:7200 |
| Portal BFF | https://localhost:7201 |
| Mobile Gateway | https://localhost:7202 (emulator: `https://10.0.2.2:7202`) |
| Expo dev server | http://localhost:8081 |
| Keycloak | http://localhost:8080 |
| Seq | http://localhost:5341 |

### Seed data
In Development the `DevSeeder` creates: one Bank ("FinCore Bank") and one MFI ("FinCore Microfinance"), two branches each, default products, a chart of accounts, and test users for every persona (passwords in `deploy/keycloak/realm-export.json`, dev only).

### Building with an AI coding assistant
Work is owner-led: follow the milestone plan in §35, hand tasks to the assistant as work packages (§35.8), have it commit in the readable format of §35.7, and make sure it loads the skills for its area (§35.2) before coding.

### Deploy to AWS
See §33.12 for the first-deployment checklist. After setup, merging to `main` deploys the changed parts to Staging; merging the release-please Release PR creates a versioned GitHub Release (`platform-vX.Y.Z` / `mobile-vX.Y.Z`) and deploys it to Production after your approval (§33.7–33.8).

## 5. Running Tests

```bash
dotnet test                                  # everything
dotnet test tests/FinCore.ArchitectureTests  # boundary rules only
dotnet test --filter "Category=Integration"  # needs Docker (Testcontainers)
```

---

# PART 2 — ARCHITECTURE

## 6. Architectural Style

FinCore combines four ideas:

1. **Modular Monolith.** One deployable process, many modules. Each module is a vertical slice of the business (Loans, Cards, …) with its own projects, database schema and public contract. Boundaries are enforced by project references and architecture tests, not just convention.
2. **Clean Architecture inside each module.** Domain → Application → Infrastructure → Presentation, with dependencies pointing inward.
3. **Domain-Driven Design.** Aggregates protect invariants (e.g., a `Loan` can't be repaid beyond its outstanding balance), value objects model money and identifiers, domain events capture what happened.
4. **CQRS (lightweight).** Commands change state through aggregates; queries read through optimized projections (Dapper or EF `AsNoTracking`). Same database, separate code paths.

### High-level view

```
                         ┌──────────────────────────────┐
  Back Office (React) ─► BFF ─►                         │
  Customer Portal (React) ─► BFF ─► FinCore.Api (host)  │
  Mobile app (Expo) ─► Mobile Gateway ─►                │
  External / simulators ───────►  Minimal API endpoints │
                         └──────────────┬───────────────┘
                                        │ in-process dispatch
 ┌──────────────┬──────────────┬────────┴─────┬──────────────┬──────────────┐
 │ Organization │  Identity &  │  Customers   │   Accounts   │    Loans     │
 │              │  Approvals   │   & KYC      │              │              │
 ├──────────────┼──────────────┼──────────────┼──────────────┼──────────────┤
 │ Microfinance │    Cards     │   General    │ Notifications│  Reporting   │
 │              │              │   Ledger     │              │              │
 └──────┬───────┴──────┬───────┴──────┬───────┴──────┬───────┴──────┬───────┘
        │   integration events via Outbox → In-memory bus (later RabbitMQ)
        ▼                                                             ▼
 ┌─────────────────────────────────────────────────────────────────────────┐
 │ PostgreSQL: schemas org | identity | customers | accounts | ledger |    │
 │             loans | mfi | cards | notifications | reporting           │
 └─────────────────────────────────────────────────────────────────────────┘
```

### Module dependency rules

| Rule | Enforced by |
|---|---|
| A module's Domain references only `FinCore.SharedKernel` | Project refs + NetArchTest |
| A module may reference **only the `.Contracts` project** of another module | NetArchTest |
| No module touches another module's `DbContext` or schema | Separate DbContexts + DB roles per schema |
| Host (`FinCore.Api`) references each module's `.Infrastructure` and `.Presentation` only to register them | Composition root |
| Cross-module writes happen through integration events, never shared transactions | Code review + tests |

### Module map and ownership

| Module | Owns (source of truth) | Depends on (Contracts) |
|---|---|---|
| Organization | Institutions, branches, business date, holidays, currencies | — |
| Identity & Approvals | Staff profiles, roles, permissions, approval requests | Organization |
| Customers & KYC | Customers, KYC cases, documents, risk ratings | Organization |
| Accounts | Deposit products, accounts, account transactions, holds, opening applications | Organization, Customers |
| General Ledger | Chart of accounts, journal entries, GL balances | Organization |
| Loans | Loan products, applications, loans, schedules, repayments | Organization, Customers, Accounts |
| Microfinance | Centers, groups, memberships, meetings, collection sheets | Organization, Customers, Loans, Accounts |
| Cards | Card products, card accounts, cards, authorizations, statements | Organization, Customers, Accounts |
| Notifications | Templates, outbound messages | All (events only) |
| Reporting | Read models / projections | All (events only) |

> **Sub-ledger vs General Ledger:** Accounts, Loans and Cards keep their own *sub-ledgers* (customer-level balances), which are authoritative for customer balances. The General Ledger holds *control accounts* (e.g., "Customer Savings Deposits", "Loans Receivable") and is authoritative for financial statements. An EOD reconciliation job proves the two agree.

## 7. Solution & Folder Structure

```
FinCore/
├── .github/
│   ├── pull_request_template.md           # readable PR summary (§35.7.3)
│   └── workflows/
│       ├── ci.yml                         # .NET build + test + coverage + commit-message check
│       ├── release-please.yml             # Release PRs, versions, GitHub Releases (§33.7)
│       ├── cd-staging.yml                 # merge to main → deploy changed components to Staging
│       ├── cd-production.yml              # platform release → full deploy to Production (approval)
│       ├── deploy-manual.yml              # pick env + components + version (redeploy/rollback)
│       ├── mobile-release.yml             # mobile release → EAS Build + Submit
│       └── _*.yml                         # reusable steps: infra, build/deploy backend, web, mobile, smoke
│       └── container.yml                  # build & push images on tag
├── infra/                                 # Terraform — full tree in §33.6
│   ├── bootstrap/                         # state bucket + GitHub OIDC roles (run once)
│   ├── modules/                           # network, ecs-service, rds-postgres, static-site, cognito, …
│   └── envs/
│       ├── staging/
│       └── production/
├── deploy/
│   ├── ecs/                               # task definition templates used by the CD workflows
│   ├── scripts/                           # run-migrations.sh, smoke-test.sh
│   ├── docker-compose.yml                 # Postgres, Redis, Keycloak, Seq, RabbitMQ
│   ├── keycloak/
│   │   └── realm-export.json              # dev realm, clients, test users
│   └── postgres/
│       └── init-schemas.sql               # schemas + per-module DB roles
├── docs/
│   ├── adr/                               # Architecture Decision Records
│   │   ├── 0001-modular-monolith.md
│   │   ├── 0002-schema-per-module.md
│   │   ├── 0003-outbox-pattern.md
│   │   └── 0004-money-and-rounding.md
│   ├── diagrams/                          # C4 / Mermaid diagrams
│   ├── skills.md                          # approved skills per track (§35.2.4)
│   └── FinCore-Project-Spec.md            # this document
├── src/
│   ├── Aspire/
│   │   ├── FinCore.AppHost/               # .NET Aspire orchestration
│   │   │   └── AppHost.cs
│   │   └── FinCore.ServiceDefaults/       # OTel, health checks, resilience
│   │       └── Extensions.cs
│   ├── Bootstrapper/
│   │   └── FinCore.Api/                   # the single host / composition root
│   │       ├── Program.cs
│   │       ├── ModuleRegistry.cs          # discovers & registers IModule
│   │       ├── Middleware/
│   │       │   ├── CorrelationIdMiddleware.cs
│   │       │   └── GlobalExceptionHandler.cs
│   │       ├── DevSeeding/
│   │       │   └── DevSeeder.cs
│   │       ├── appsettings.json
│   │       └── appsettings.Development.json
│   ├── BuildingBlocks/
│   │   ├── FinCore.SharedKernel/          # pure C#, no framework deps
│   │   │   ├── Domain/
│   │   │   │   ├── Entity.cs
│   │   │   │   ├── AggregateRoot.cs
│   │   │   │   ├── ValueObject.cs
│   │   │   │   ├── IDomainEvent.cs
│   │   │   │   └── StronglyTypedId.cs
│   │   │   ├── Money/
│   │   │   │   ├── Money.cs
│   │   │   │   ├── Currency.cs
│   │   │   │   └── RoundingPolicy.cs
│   │   │   ├── Results/
│   │   │   │   ├── Result.cs
│   │   │   │   ├── Error.cs
│   │   │   │   └── ErrorType.cs
│   │   │   └── Time/
│   │   │       ├── IBusinessDateProvider.cs
│   │   │       └── DateRange.cs
│   │   ├── FinCore.BuildingBlocks.Application/
│   │   │   ├── Messaging/
│   │   │   │   ├── ICommand.cs / ICommandHandler.cs
│   │   │   │   ├── IQuery.cs / IQueryHandler.cs
│   │   │   │   └── IDispatcher.cs
│   │   │   ├── Behaviors/
│   │   │   │   ├── ValidationBehavior.cs
│   │   │   │   ├── LoggingBehavior.cs
│   │   │   │   ├── TransactionBehavior.cs
│   │   │   │   └── AuditBehavior.cs
│   │   │   ├── EventBus/
│   │   │   │   ├── IIntegrationEvent.cs
│   │   │   │   ├── IEventBus.cs
│   │   │   │   └── IIntegrationEventHandler.cs
│   │   │   ├── Security/
│   │   │   │   └── ICurrentUser.cs
│   │   │   └── Pagination/
│   │   │       └── PagedResult.cs
│   │   ├── FinCore.BuildingBlocks.Infrastructure/
│   │   │   ├── Persistence/
│   │   │   │   ├── ModuleDbContext.cs     # base: audit fields, outbox, concurrency
│   │   │   │   ├── Interceptors/
│   │   │   │   │   ├── AuditableEntityInterceptor.cs
│   │   │   │   │   └── DomainEventsToOutboxInterceptor.cs
│   │   │   │   └── Converters/            # Money, StronglyTypedId converters
│   │   │   ├── Outbox/
│   │   │   │   ├── OutboxMessage.cs
│   │   │   │   └── ProcessOutboxJob.cs
│   │   │   ├── Inbox/
│   │   │   │   ├── InboxMessage.cs
│   │   │   │   └── IdempotentEventHandlerDecorator.cs
│   │   │   ├── EventBus/
│   │   │   │   ├── InMemoryEventBus.cs    # System.Threading.Channels
│   │   │   │   └── RabbitMqEventBus.cs    # later (microservices, §36)
│   │   │   ├── Idempotency/
│   │   │   │   └── IdempotencyStore.cs
│   │   │   ├── Jobs/
│   │   │   │   └── QuartzModuleExtensions.cs
│   │   │   └── Encryption/
│   │   │       └── PiiEncryptionConverter.cs
│   │   └── FinCore.BuildingBlocks.Web/
│   │       ├── IModule.cs                 # AddModule / MapEndpoints contract
│   │       ├── IEndpoint.cs
│   │       ├── ResultExtensions.cs        # Result → ProblemDetails
│   │       ├── IdempotencyFilter.cs
│   │       └── Authorization/
│   │           ├── Permissions.cs
│   │           └── PermissionAuthorizationHandler.cs
│   ├── Modules/
│   │   ├── Organization/
│   │   │   ├── FinCore.Modules.Organization.Domain/
│   │   │   ├── FinCore.Modules.Organization.Application/
│   │   │   ├── FinCore.Modules.Organization.Infrastructure/
│   │   │   ├── FinCore.Modules.Organization.Presentation/
│   │   │   └── FinCore.Modules.Organization.Contracts/
│   │   ├── Identity/            (same 5-project layout)
│   │   ├── Customers/           (same 5-project layout)
│   │   ├── Accounts/            (same 5-project layout)
│   │   ├── Ledger/              (same 5-project layout)
│   │   ├── Loans/               (same 5-project layout — expanded in §8)
│   │   ├── Microfinance/        (same 5-project layout)
│   │   ├── Cards/               (same 5-project layout)
│   │   ├── Notifications/       (same 5-project layout)
│   │   └── Reporting/           (same 5-project layout)
│   ├── Tools/
│   │   └── FinCore.Migrator/              # applies every module's migrations (one-off ECS task)
│   └── Gateway/
│       └── FinCore.Bff/                   # Backend-for-Frontend (one project, deployed per app)
│           ├── Program.cs                 # cookie + OIDC auth, YARP proxy, SPA hosting
│           ├── BffEndpoints.cs            # /bff/login, /bff/logout, /bff/user
│           ├── Security/
│           │   ├── CsrfHeaderMiddleware.cs
│           │   ├── TokenStore.cs          # server-side token storage & refresh
│           │   └── SecurityHeaders.cs     # CSP, frame-ancestors, etc.
│           ├── Properties/launchSettings.json  # BackOffice, Portal & Mobile profiles
│           ├── appsettings.BackOffice.json
│           ├── appsettings.Portal.json
│           ├── appsettings.Mobile.json        # token-passthrough gateway mode
│           └── wwwroot/                   # built SPA copied here for production
├── frontend/                              # React pnpm monorepo — full tree in §30.2
│   ├── apps/
│   │   ├── backoffice/                    # staff SPA
│   │   ├── portal/                        # customer web SPA
│   │   └── mobile/                        # customer iOS/Android app (Expo) — full tree in §31.2
│   └── packages/
│       ├── api-client/                    # generated from OpenAPI (Orval) — web & mobile
│       ├── ui/                            # shadcn/ui + financial widgets (web only)
│       ├── auth/                          # BFF session hooks, <Can> guard (web only)
│       ├── permissions/                   # permission constants (web & mobile)
│       ├── i18n/                          # translations (web & mobile)
│       ├── money/                         # decimal.js helpers (web & mobile)
│       ├── design-tokens/                 # colors, type, spacing → Tailwind & NativeWind (§32.9)
│       └── config/                        # shared tsconfig / eslint / tailwind tokens
├── tests/
│   ├── FinCore.ArchitectureTests/
│   ├── FinCore.IntegrationTests.Shared/   # WebApplicationFactory, Testcontainers fixtures
│   ├── FinCore.EndToEndTests/             # full business flows over HTTP
│   └── Modules/
│       ├── Loans/
│       │   ├── FinCore.Modules.Loans.UnitTests/
│       │   └── FinCore.Modules.Loans.IntegrationTests/
│       ├── Cards/ ...
│       └── (one pair per module)
├── CLAUDE.md                              # AI assistant rules (§35.1) + skills per area (§35.2)
├── AGENTS.md                              # same rules for other coding agents
├── .claude/
│   ├── settings.json                      # no AI co-author/attribution on commits & PRs (§35.7)
│   └── skills/                            # owner-installed skills (C#/.NET chosen by owner)
├── .githooks/
│   └── commit-msg                         # rejects AI attribution lines
├── CHANGELOG.md                           # generated from commits at each release tag
├── .editorconfig
├── .gitignore
├── Directory.Build.props                  # TargetFramework net10.0, Nullable, TreatWarningsAsErrors
├── Directory.Packages.props               # Central Package Management
├── global.json                            # pins .NET 10 SDK
├── version.txt                            # platform version (managed by release-please)
├── release-please-config.json
├── .release-please-manifest.json
├── FinCore.slnx
└── README.md
```

### Root build files

**`global.json`**
```json
{
  "sdk": { "version": "10.0.100", "rollForward": "latestFeature" }
}
```

**`Directory.Build.props`**
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
</Project>
```

### Scaffolding commands (example for one module)

```bash
dotnet new sln -n FinCore            # produces FinCore.slnx on .NET 10
M=Loans
for L in Domain Application Infrastructure Presentation Contracts; do
  dotnet new classlib -o src/Modules/$M/FinCore.Modules.$M.$L
  dotnet sln add src/Modules/$M/FinCore.Modules.$M.$L --solution-folder Modules/$M
done
dotnet add src/Modules/$M/FinCore.Modules.$M.Application reference src/Modules/$M/FinCore.Modules.$M.Domain
dotnet add src/Modules/$M/FinCore.Modules.$M.Infrastructure reference src/Modules/$M/FinCore.Modules.$M.Application
dotnet add src/Modules/$M/FinCore.Modules.$M.Presentation reference src/Modules/$M/FinCore.Modules.$M.Application
```

## 8. Module Anatomy (Clean Architecture per Module)

Every module has five projects:

| Project | Contains | May reference |
|---|---|---|
| `.Domain` | Aggregates, entities, value objects, domain events, domain services, errors | SharedKernel |
| `.Application` | Commands, queries, handlers, validators, integration-event handlers, repository interfaces | Domain, BuildingBlocks.Application, other modules' `.Contracts` |
| `.Infrastructure` | DbContext, EF configurations, migrations, repositories, jobs, external adapters, `XxxModule : IModule` | Application, BuildingBlocks.Infrastructure |
| `.Presentation` | Minimal API endpoints, request/response DTOs | Application, BuildingBlocks.Web |
| `.Contracts` | Integration events + public module API interface + DTOs exposed to other modules | SharedKernel only |

### Expanded example — Loans module

```
src/Modules/Loans/
├── FinCore.Modules.Loans.Domain/
│   ├── Products/
│   │   ├── LoanProduct.cs                 # aggregate
│   │   ├── LoanProductId.cs
│   │   ├── InterestMethod.cs              # Flat | DecliningBalance
│   │   ├── RepaymentFrequency.cs          # Weekly | BiWeekly | Monthly
│   │   └── FeeDefinition.cs
│   ├── Applications/
│   │   ├── LoanApplication.cs             # aggregate
│   │   ├── LoanApplicationId.cs
│   │   ├── LoanApplicationStatus.cs
│   │   ├── CreditAssessment.cs
│   │   └── Collateral.cs / Guarantor.cs
│   ├── Loans/
│   │   ├── Loan.cs                        # aggregate
│   │   ├── LoanId.cs
│   │   ├── LoanStatus.cs
│   │   ├── RepaymentSchedule.cs
│   │   ├── Installment.cs
│   │   ├── LoanTransaction.cs
│   │   ├── AllocationOrder.cs             # Penalty → Fees → Interest → Principal
│   │   └── DelinquencyBucket.cs
│   ├── Services/
│   │   ├── IAmortizationCalculator.cs
│   │   ├── FlatAmortizationCalculator.cs
│   │   ├── DecliningBalanceAmortizationCalculator.cs
│   │   └── CreditScoringService.cs
│   ├── Events/
│   │   ├── LoanApplicationSubmittedDomainEvent.cs
│   │   ├── LoanApprovedDomainEvent.cs
│   │   ├── LoanDisbursedDomainEvent.cs
│   │   └── LoanRepaymentReceivedDomainEvent.cs
│   └── Errors/
│       └── LoanErrors.cs
├── FinCore.Modules.Loans.Application/
│   ├── Abstractions/
│   │   ├── ILoanRepository.cs
│   │   ├── ILoanApplicationRepository.cs
│   │   └── ILoansUnitOfWork.cs
│   ├── Products/
│   │   ├── CreateLoanProduct/ (Command, Handler, Validator)
│   │   └── GetLoanProducts/   (Query, Handler, Response)
│   ├── Applications/
│   │   ├── SubmitLoanApplication/
│   │   ├── AssessLoanApplication/
│   │   ├── ApproveLoanApplication/
│   │   └── RejectLoanApplication/
│   ├── Loans/
│   │   ├── DisburseLoan/
│   │   ├── RecordRepayment/
│   │   ├── PrepayLoan/
│   │   ├── RestructureLoan/
│   │   ├── WriteOffLoan/
│   │   ├── GetLoanById/
│   │   ├── GetRepaymentSchedule/
│   │   └── PreviewSchedule/
│   ├── IntegrationEventHandlers/
│   │   ├── CustomerKycApprovedHandler.cs
│   │   └── ApprovalRequestDecidedHandler.cs
│   └── Jobs/
│       ├── AccruePenaltiesCommand.cs
│       └── UpdateDelinquencyCommand.cs
├── FinCore.Modules.Loans.Infrastructure/
│   ├── Persistence/
│   │   ├── LoansDbContext.cs              # HasDefaultSchema("loans")
│   │   ├── Configurations/
│   │   ├── Repositories/
│   │   └── Migrations/
│   ├── Jobs/
│   │   ├── PenaltyAccrualJob.cs
│   │   └── DelinquencyJob.cs
│   ├── ContractsImpl/
│   │   └── LoansModuleApi.cs              # implements ILoansModuleApi
│   └── LoansModule.cs                     # IModule implementation
├── FinCore.Modules.Loans.Presentation/
│   ├── Products/ProductEndpoints.cs
│   ├── Applications/ApplicationEndpoints.cs
│   └── Loans/LoanEndpoints.cs
└── FinCore.Modules.Loans.Contracts/
    ├── ILoansModuleApi.cs
    ├── Dtos/LoanSummaryDto.cs
    └── IntegrationEvents/
        ├── LoanApprovedIntegrationEvent.cs
        ├── LoanDisbursedIntegrationEvent.cs
        ├── LoanRepaymentReceivedIntegrationEvent.cs
        ├── LoanBecameDelinquentIntegrationEvent.cs
        └── LoanWrittenOffIntegrationEvent.cs
```

### The `IModule` contract

```csharp
public interface IModule
{
    string Name { get; }
    IServiceCollection AddModule(IServiceCollection services, IConfiguration config);
    IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder app);
}

// Program.cs
builder.AddServiceDefaults();
builder.Services.AddBuildingBlocks(builder.Configuration);
builder.Services.AddModules(builder.Configuration,
    typeof(OrganizationModule).Assembly, typeof(IdentityModule).Assembly,
    typeof(CustomersModule).Assembly,    typeof(AccountsModule).Assembly,
    typeof(LedgerModule).Assembly,       typeof(LoansModule).Assembly,
    typeof(MicrofinanceModule).Assembly, typeof(CardsModule).Assembly,
    typeof(NotificationsModule).Assembly, typeof(ReportingModule).Assembly);

var app = builder.Build();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapModules();          // each module maps under /api/v1/{module}
app.MapDefaultEndpoints(); // health checks
app.Run();
```

### A command slice end-to-end

```csharp
// Application/Loans/DisburseLoan/DisburseLoanCommand.cs
public sealed record DisburseLoanCommand(LoanId LoanId, Guid TargetAccountId, DateOnly ValueDate)
    : ICommand;

// Application/Loans/DisburseLoan/DisburseLoanCommandHandler.cs
internal sealed class DisburseLoanCommandHandler(
    ILoanRepository loans,
    IAccountsModuleApi accounts,          // from Accounts.Contracts (read-only check)
    IBusinessDateProvider businessDate,
    ILoansUnitOfWork uow) : ICommandHandler<DisburseLoanCommand>
{
    public async Task<Result> Handle(DisburseLoanCommand cmd, CancellationToken ct)
    {
        var loan = await loans.GetAsync(cmd.LoanId, ct);
        if (loan is null) return LoanErrors.NotFound(cmd.LoanId);

        var account = await accounts.GetAccountSummaryAsync(cmd.TargetAccountId, ct);
        if (account is null || account.CustomerId != loan.CustomerId)
            return LoanErrors.InvalidDisbursementAccount;

        var result = loan.Disburse(cmd.TargetAccountId, cmd.ValueDate, businessDate.Today);
        if (result.IsFailure) return result;

        await uow.SaveChangesAsync(ct);   // domain event → outbox in same transaction
        return Result.Success();
    }
}
```

## 9. Building Blocks & Shared Kernel

### Money

Money is **never** a `double`. It is a value object over `decimal` plus an ISO-4217 currency.

```csharp
public sealed record Money(decimal Amount, Currency Currency)
{
    public static Money Zero(Currency c) => new(0m, c);

    public Money Add(Money other)      { EnsureSameCurrency(other); return this with { Amount = Amount + other.Amount }; }
    public Money Subtract(Money other) { EnsureSameCurrency(other); return this with { Amount = Amount - other.Amount }; }
    public Money Round(RoundingPolicy p) => this with { Amount = Math.Round(Amount, Currency.MinorUnits, p.Mode) };

    private void EnsureSameCurrency(Money o)
    {
        if (o.Currency != Currency) throw new CurrencyMismatchException(Currency, o.Currency);
    }
}
```

**Rounding rules (ADR-0004):**
- Store amounts as `numeric(19,4)` in the DB; round to currency minor units (e.g., 2) at posting time.
- Default `MidpointRounding.ToEven` (banker's rounding); configurable per product.
- Interest is calculated at full precision daily and rounded only when posted.
- Rounding differences on the final installment are absorbed by that installment.

### Result pattern

Business failures return `Result` / `Result<T>` with a typed `Error` (Validation, NotFound, Conflict, Forbidden, BusinessRule). Exceptions are reserved for truly exceptional situations. The Presentation layer maps errors to RFC 9457 `ProblemDetails`.

### Strongly-typed IDs

`readonly record struct LoanId(Guid Value)` — prevents passing a `CustomerId` where a `LoanId` is expected. GUIDs are generated with `Guid.CreateVersion7()` (time-ordered, index-friendly).

### Business date

All financial logic uses `IBusinessDateProvider.Today`, owned by the Organization module, **not** `DateTime.UtcNow`. The business date advances only when EOD completes. Audit timestamps use `TimeProvider` (UTC).

### Base entity and auditing

Every aggregate carries `CreatedAtUtc`, `CreatedBy`, `ModifiedAtUtc`, `ModifiedBy`, and a concurrency token (PostgreSQL `xmin`). An EF interceptor fills these from `ICurrentUser` and `TimeProvider`.

## 10. Inter-Module Communication

Two mechanisms, nothing else:

### 1. Synchronous queries through Contracts (read-only)

Each module exposes an interface in its `.Contracts` project, implemented in its `.Infrastructure`:

```csharp
public interface ICustomersModuleApi
{
    Task<CustomerSummaryDto?> GetCustomerAsync(Guid customerId, CancellationToken ct);
    Task<bool> IsKycVerifiedAsync(Guid customerId, CancellationToken ct);
}
```

Rules: read-only, returns DTOs (never domain entities), no cross-module transactions. When extracted to microservices, this interface becomes an HTTP/gRPC client with the same signature.

### 2. Asynchronous integration events (state changes)

```
Aggregate raises DomainEvent
  → DomainEventsToOutboxInterceptor maps it to an IntegrationEvent
  → stored in module's outbox table IN THE SAME TRANSACTION
  → ProcessOutboxJob (every 1–2 s) publishes to IEventBus
  → subscribing modules' handlers run inside IdempotentEventHandlerDecorator
  → handler records message id in its own inbox table (exactly-once effect)
```

Integration events are immutable records, versioned by name (`LoanDisbursedIntegrationEvent.V1` when breaking changes are needed), and contain everything subscribers need (no "go fetch it back" chatter).

### Integration event catalog

| Event | Publisher | Subscribers |
|---|---|---|
| `InstitutionCreated` | Organization | Ledger (seed CoA), Identity |
| `BusinessDateAdvanced` | Organization | Accounts, Loans, Cards, Ledger, Reporting |
| `CustomerCreated` | Customers | Reporting, Notifications |
| `CustomerKycApproved` / `CustomerKycRejected` | Customers | Accounts, Loans, Cards, Notifications |
| `CustomerRiskRatingChanged` | Customers | Accounts, Cards |
| `AccountOpened` | Accounts | Ledger, Notifications, Reporting |
| `AccountTransactionPosted` | Accounts | Ledger, Reporting, Notifications |
| `AccountClosed` | Accounts | Ledger, Reporting |
| `ApprovalRequestDecided` | Identity | Requesting module (Loans, Accounts, Cards, Ledger) |
| `LoanApproved` | Loans | Notifications, Microfinance |
| `LoanDisbursed` | Loans | Accounts (credit), Ledger, Microfinance, Notifications |
| `LoanRepaymentReceived` | Loans | Ledger, Microfinance, Reporting |
| `LoanBecameDelinquent` | Loans | Notifications, Microfinance, Reporting |
| `LoanWrittenOff` | Loans | Ledger, Reporting |
| `GroupLoanCycleRequested` | Microfinance | Loans |
| `CollectionSheetPosted` | Microfinance | Loans, Accounts |
| `CardIssued` | Cards | Notifications |
| `CardTransactionPosted` | Cards | Ledger, Reporting |
| `CardStatementGenerated` | Cards | Notifications |
| `CardPaymentReceived` | Cards | Ledger |
| `JournalEntryPosted` | Ledger | Reporting |
| `EndOfDayCompleted` | Organization | Reporting, Notifications |

## 11. Data Architecture

- **One PostgreSQL database, one schema per module** (`org`, `identity`, `customers`, `accounts`, `ledger`, `loans`, `mfi`, `cards`, `notifications`, `reporting`).
- **One `DbContext` per module**, each with its own migrations history table in its own schema:
  ```csharp
  options.UseNpgsql(cs, npg => npg.MigrationsHistoryTable("__ef_migrations", "loans"));
  ```
- **One DB role per module** (`fincore_loans` etc.) with rights only on its schema — accidental cross-schema queries fail at the database level.
- **No foreign keys across schemas.** Cross-module references are plain `uuid` columns (e.g., `loans.loan.customer_id`).
- Every module schema includes `outbox_messages` and `inbox_messages` tables.
- Ledger journal tables are **append-only** (no UPDATE/DELETE grants; corrections are reversing entries).
- Naming: snake_case via `EFCore.NamingConventions`.
- Migrations:
  ```bash
  dotnet ef migrations add Initial \
    --project src/Modules/Loans/FinCore.Modules.Loans.Infrastructure \
    --startup-project src/Bootstrapper/FinCore.Api \
    --context LoansDbContext --output-dir Persistence/Migrations
  ```
  Production migrations run as an idempotent SQL script (`dotnet ef migrations script --idempotent`) in the release pipeline, never at app startup.

---

# PART 3 — MODULES IN DETAIL

Each module section lists: purpose, domain model, business rules, state machines, tables, endpoints and events. All endpoints are prefixed with `/api/v1`.

## 12. Organization Module

**Purpose:** Defines *who* is operating the platform and *when* (business date). Supports multiple institutions of type `Bank` or `Mfi` in one deployment.

### Domain model

| Aggregate | Key fields |
|---|---|
| `Institution` | Id, Code, Name, Type (Bank/Mfi), BaseCurrency, LicenseNumber, Status, CurrentBusinessDate |
| `Branch` | Id, InstitutionId, Code, Name, Address, Status, ParentBranchId |
| `HolidayCalendar` | InstitutionId, Holidays[] (Date, Name), WeekendDays |
| `Currency` (reference) | IsoCode, Name, MinorUnits, IsActive |
| `ExchangeRate` | From, To, Rate, EffectiveDate (for display/multi-currency, after v1) |

### Business rules
- Institution type drives available modules: Microfinance features require `Type = Mfi` (or an explicit feature flag for banks doing group lending).
- Business date can only move forward, one working day at a time, and only by the EOD process.
- Repayment due dates falling on a holiday shift according to product policy (next working day / previous / none).

### Endpoints

| Method | Route | Permission |
|---|---|---|
| POST | `/institutions` | `org.manage` |
| GET | `/institutions?page=&pageSize=` | `org.read` |
| GET | `/institutions/{id}` | `org.read` |
| POST | `/institutions/{id}/branches` | `org.manage` |
| GET | `/institutions/{id}/branches` | `org.read` |
| PUT | `/institutions/{id}/holidays` | `org.manage` |
| GET | `/institutions/{id}/business-date` | authenticated |
| POST | `/institutions/{id}/eod` | `eod.run` |
| GET | `/institutions/{id}/eod/runs` | `eod.read` |

### Tables (`org` schema)
`institutions`, `branches`, `holidays`, `currencies`, `exchange_rates`, `eod_runs`, `eod_run_steps`, `outbox_messages`, `inbox_messages`

## 13. Identity & Approvals Module

**Purpose:** Authorization data (staff, roles, permissions, branch scope) and a generic **maker-checker** engine. Authentication itself is delegated to the OIDC provider (Keycloak locally, Cognito on AWS).

### Domain model

| Aggregate | Key fields |
|---|---|
| `StaffUser` | Id, ExternalSubjectId (IdP `sub`), InstitutionId, HomeBranchId, Name, Email, Status, Roles[], ApprovalLimits[] |
| `Role` | Id, Name, Permissions[] |
| `ApprovalLimit` | Type (LoanApproval, CardLimit, ManualJournal, Withdrawal), MaxAmount, Currency |
| `ApprovalRequest` | Id, Module, OperationType, EntityId, Amount, PayloadJson, MakerId, Status, CheckerId, Comment, DecidedAt |

### Business rules
- **Four-eyes:** maker can never approve their own request.
- Checker must hold the required permission **and** an approval limit ≥ request amount.
- High-value operations may require 2 checkers (configurable thresholds per operation type).
- Approval requests expire after a configurable number of business days.

### Standard roles

| Role | Typical permissions |
|---|---|
| `SystemAdmin` | org.manage, identity.manage, products.manage |
| `BranchOfficer` | customers.create, kyc.capture, accounts.open, accounts.transact |
| `LoanOfficer` | loans.apply, loans.assess, loans.repay |
| `FieldOfficer` | mfi.groups.manage, mfi.collections.record |
| `CreditApprover` | loans.approve, cards.limit.approve |
| `ComplianceOfficer` | kyc.approve, aml.review |
| `Accountant` | ledger.journal.create, ledger.read, eod.run |
| `Auditor` | *.read |

Permissions are emitted as claims after login (via a claims transformation that loads `StaffUser` roles) and checked with policies: `RequireAuthorization("loans.approve")`.

### Endpoints

| Method | Route | Permission |
|---|---|---|
| GET | `/me` | authenticated |
| POST | `/staff` | `identity.manage` |
| PUT | `/staff/{id}/roles` | `identity.manage` |
| PUT | `/staff/{id}/approval-limits` | `identity.manage` |
| GET | `/approvals?status=Pending` | any checker permission |
| POST | `/approvals/{id}/approve` | operation-specific |
| POST | `/approvals/{id}/reject` | operation-specific |

### Tables (`identity` schema)
`staff_users`, `roles`, `role_permissions`, `staff_roles`, `approval_limits`, `approval_requests`, `approval_decisions`, `outbox_messages`, `inbox_messages`

## 14. Customers & KYC Module

**Purpose:** Single customer view and Know-Your-Customer process. Every other module references customers by `CustomerId`.

### Domain model

| Aggregate | Key fields |
|---|---|
| `Customer` | Id, CustomerNumber, Type (Individual/Business), InstitutionId, BranchId, Status, RiskRating, KycStatus |
| `IndividualProfile` (VO) | FirstName, LastName, DateOfBirth, Gender, NationalId (encrypted), Nationality, Occupation, MonthlyIncome |
| `BusinessProfile` (VO) | LegalName, RegistrationNumber, TaxId, IncorporationDate, Industry |
| `Address` / `ContactInfo` | Type, lines, city, country, effective dates (history kept) |
| `KycCase` | Id, CustomerId, Level (Simplified/Standard/Enhanced), Status, Documents[], ScreeningResults[], ReviewerId |
| `KycDocument` | Type (NationalId, Passport, UtilityBill, PaySlip…), StorageKey, ExpiryDate, VerificationStatus |
| `ScreeningResult` | Provider, Type (Identity, Sanctions, PEP, AdverseMedia), Outcome, MatchScore, RawRef |

### Customer & KYC state machines

```
Customer:  Prospect ──► Active ──► Dormant ──► Active
               │           │
               └─► Rejected└─► Blacklisted / Closed

KycCase:   Draft ──► Submitted ──► Screening ──► PendingReview ──► Approved
                                       │               │
                                       └──► AutoApproved└──► Rejected / InfoRequested
```

### Business rules
- Customer number format: `{InstitutionCode}{Branch}{Sequence}{CheckDigit}` (mod-97 or Luhn check digit).
- Duplicate detection on NationalId + DateOfBirth (and fuzzy name match) before creation.
- **Risk rating** is computed from a scorecard: nationality/country risk, occupation, PEP flag, expected transaction volume, product mix. High risk ⇒ Enhanced Due Diligence and Compliance Officer approval.
- A customer cannot transact (accounts, loans, cards) until `KycStatus = Approved`.
- Expired ID documents flip KYC to `RenewalRequired` (daily job) and notify the customer.
- Screening providers sit behind `IIdentityVerificationProvider` and `ISanctionsScreeningProvider`, with **simulated** implementations (deterministic outcomes based on test names, e.g., a surname "Sanctioned" always hits).

### Endpoints

| Method | Route | Permission |
|---|---|---|
| POST | `/customers` | `customers.create` |
| GET | `/customers/{id}` | `customers.read` |
| GET | `/customers?search=&branchId=&page=` | `customers.read` |
| PUT | `/customers/{id}/contact` | `customers.update` |
| POST | `/customers/{id}/kyc-cases` | `kyc.capture` |
| POST | `/kyc-cases/{id}/documents` (multipart) | `kyc.capture` |
| POST | `/kyc-cases/{id}/submit` | `kyc.capture` |
| POST | `/kyc-cases/{id}/approve` | `kyc.approve` |
| POST | `/kyc-cases/{id}/reject` | `kyc.approve` |
| GET | `/customers/{id}/360` | `customers.read` (aggregates via other modules' Contracts) |

### Tables (`customers` schema)
`customers`, `individual_profiles`, `business_profiles`, `addresses`, `contacts`, `kyc_cases`, `kyc_documents`, `screening_results`, `risk_assessments`, `outbox_messages`, `inbox_messages`

## 15. Accounts Module

**Purpose:** Deposit products and customer accounts, including the **digital account-opening** journey. Holds the deposit sub-ledger.

### Domain model

| Aggregate | Key fields |
|---|---|
| `DepositProduct` | Id, InstitutionId, Code, Type (Savings/Current/FixedDeposit), Currency, InterestRate, InterestCalcBasis (Actual/365, 30/360), CapitalizationFrequency, MinOpeningBalance, MinBalance, OverdraftAllowed, DormancyDays, Fees[] |
| `AccountOpeningApplication` | Id, CustomerId (or prospect data), ProductId, Channel (Branch/Online), Status, InitialDeposit, Signatories[] |
| `Account` | Id, AccountNumber, CustomerId, ProductId, BranchId, Currency, Status, LedgerBalance, AvailableBalance, Holds[], AccruedInterest, OpenedOn, MaturityDate (FD) |
| `AccountTransaction` | Id, AccountId, Type, Amount, Direction (Dr/Cr), ValueDate, BookingDate, Reference, Narrative, BalanceAfter, CorrelationId |
| `Hold` | Id, Amount, Reason (CardAuth, Lien, Legal), ExpiresOn, Source |

### Account-opening workflow

```
Started ──► CustomerLinked ──► KycPending ──► KycApproved ──► PendingApproval* ──► Approved ──► AccountActive
   │                              │                                                              ▲
   └──────────► Abandoned         └──► Rejected                    initial deposit received ─────┘
* PendingApproval only for high-risk customers or restricted products (maker-checker)
```

1. Customer (or officer) starts an application and picks a product.
2. If the customer doesn't exist yet, the Customers module creates a `Prospect` customer and a KYC case.
3. Accounts subscribes to `CustomerKycApproved` and advances the application.
4. Risk/product rules decide whether a checker must approve.
5. Account is created in `PendingFunding` (if min opening balance > 0) or `Active`.
6. `AccountOpened` is published → Ledger, Notifications, Reporting.

### Account state machine

```
PendingFunding ──► Active ──► Dormant ──► Active (on customer-initiated txn)
                     │  ▲
                     ▼  │
                   Frozen (Debit/Credit/Total)
                     │
                     ▼
                   Closed (balance must be 0, no holds)
```

### Business rules
- Account number: `{BranchCode}{ProductCode}{Sequence}{CheckDigit}`, 13 digits, mod-97 check.
- `AvailableBalance = LedgerBalance − SumOfActiveHolds + OverdraftLimit`.
- Withdrawals/debits rejected when `AvailableBalance < amount` (unless overdraft).
- Every posting is an `AccountTransaction` with `BalanceAfter`; balances are updated in the same transaction with optimistic concurrency.
- Reversals create an opposite transaction linked by `ReversalOfId`; nothing is deleted.
- Withdrawals above the officer's limit go through maker-checker.
- **Interest:** daily accrual on end-of-day balance (`balance × rate / daysInYear`), capitalized monthly/quarterly per product.
- **Dormancy:** no customer-initiated transaction for `DormancyDays` → `Dormant`; debits blocked.
- **Fixed deposits:** no withdrawals before maturity unless "premature break" with penalty rate; on maturity, auto-renew or pay out to linked account.

### Endpoints

| Method | Route | Permission |
|---|---|---|
| POST | `/deposit-products` | `products.manage` |
| GET | `/deposit-products` | authenticated |
| POST | `/account-applications` | `accounts.open` or customer self |
| GET | `/account-applications/{id}` | `accounts.read` |
| POST | `/account-applications/{id}/submit` | `accounts.open` |
| GET | `/accounts/{id}` | `accounts.read` |
| GET | `/customers/{customerId}/accounts` | `accounts.read` |
| POST | `/accounts/{id}/deposits` | `accounts.transact` (Idempotency-Key required) |
| POST | `/accounts/{id}/withdrawals` | `accounts.transact` (Idempotency-Key required) |
| POST | `/transfers` | `accounts.transact` (Idempotency-Key required) |
| POST | `/accounts/{id}/holds` | `accounts.hold` |
| DELETE | `/accounts/{id}/holds/{holdId}` | `accounts.hold` |
| POST | `/accounts/{id}/freeze` / `/unfreeze` | `accounts.manage` |
| POST | `/accounts/{id}/close` | `accounts.manage` |
| GET | `/accounts/{id}/transactions?from=&to=` | `accounts.read` |
| GET | `/accounts/{id}/statement?from=&to=&format=pdf` | `accounts.read` |

### Tables (`accounts` schema)
`deposit_products`, `product_fees`, `account_applications`, `accounts`, `account_signatories`, `account_transactions`, `holds`, `interest_accruals`, `outbox_messages`, `inbox_messages`

## 16. General Ledger Module

**Purpose:** The accounting backbone. Every financial event in every module ends up as a balanced journal entry here.

### Domain model

| Aggregate | Key fields |
|---|---|
| `GlAccount` | Id, InstitutionId, Code (e.g. `1.2.01`), Name, Type (Asset/Liability/Equity/Income/Expense), ParentId, IsControlAccount, IsPostable, Currency |
| `JournalEntry` | Id, InstitutionId, EntryNumber, BusinessDate, Source (Module + EventId), Description, Status (Posted/Reversed), Lines[], ReversalOfId |
| `JournalLine` | GlAccountId, BranchId, Debit, Credit, Currency, SubLedgerRef (AccountId/LoanId/CardAccountId) |
| `PostingRule` | TransactionType (e.g. `LoanDisbursement`), ProductId?, Lines template (Dr account, Cr account, amount component) |
| `GlBalance` (projection) | GlAccountId, BranchId, BusinessDate, OpeningBalance, Debits, Credits, ClosingBalance |

### Core invariants
- `Σ Debits = Σ Credits` for every journal entry, per currency. Enforced in the aggregate **and** by a DB check/trigger.
- Posted entries are **immutable**; correction = reversing entry + new entry.
- Only `IsPostable` leaf accounts receive lines.
- Entries post to the current business date; back-dating only via an approved manual journal into an open period.
- Each source event posts **at most once** (unique index on `source_module, source_event_id`).

### Default chart of accounts (seeded per institution)

| Code | Name | Type |
|---|---|---|
| 1.1.01 | Cash in Vault | Asset |
| 1.1.02 | Nostro / Settlement Account | Asset |
| 1.2.01 | Loans Receivable — Principal (control) | Asset |
| 1.2.02 | Interest Receivable | Asset |
| 1.2.09 | Loan Loss Provision (contra) | Asset |
| 1.3.01 | Card Receivables (control) | Asset |
| 2.1.01 | Customer Savings Deposits (control) | Liability |
| 2.1.02 | Customer Current Accounts (control) | Liability |
| 2.1.03 | Fixed Deposits (control) | Liability |
| 2.1.04 | Interest Payable on Deposits | Liability |
| 2.2.01 | Card Settlement Payable | Liability |
| 3.1.01 | Share Capital | Equity |
| 3.2.01 | Retained Earnings | Equity |
| 4.1.01 | Interest Income — Loans | Income |
| 4.1.02 | Interest Income — Cards | Income |
| 4.2.01 | Fee Income | Income |
| 4.2.02 | Penalty Income | Income |
| 5.1.01 | Interest Expense — Deposits | Expense |
| 5.2.01 | Loan Loss Provision Expense | Expense |
| 5.2.02 | Bad Debt Write-off Expense | Expense |

### Sample posting rules

| Transaction | Debit | Credit |
|---|---|---|
| Cash deposit to savings | 1.1.01 Cash | 2.1.01 Savings Deposits |
| Loan disbursement to savings | 1.2.01 Loans Receivable | 2.1.01 Savings Deposits |
| Loan repayment from savings (principal) | 2.1.01 Savings Deposits | 1.2.01 Loans Receivable |
| Loan repayment (interest) | 2.1.01 Savings Deposits | 4.1.01 Interest Income |
| Loan fee charged | 2.1.01 Savings Deposits | 4.2.01 Fee Income |
| Deposit interest accrual | 5.1.01 Interest Expense | 2.1.04 Interest Payable |
| Deposit interest capitalization | 2.1.04 Interest Payable | 2.1.01 Savings Deposits |
| Card purchase cleared | 1.3.01 Card Receivables | 2.2.01 Card Settlement Payable |
| Card interest charged | 1.3.01 Card Receivables | 4.1.02 Interest Income — Cards |
| Card payment from account | 2.1.02 Current Accounts | 1.3.01 Card Receivables |
| Loan write-off | 1.2.09 Provision | 1.2.01 Loans Receivable |

### Endpoints

| Method | Route | Permission |
|---|---|---|
| GET/POST | `/gl-accounts` | `ledger.read` / `ledger.coa.manage` |
| GET/PUT | `/posting-rules` | `ledger.read` / `ledger.coa.manage` |
| POST | `/journal-entries` (manual, goes to maker-checker) | `ledger.journal.create` |
| GET | `/journal-entries?from=&to=&source=` | `ledger.read` |
| POST | `/journal-entries/{id}/reverse` | `ledger.journal.create` |
| GET | `/trial-balance?date=&branchId=` | `ledger.read` |
| GET | `/gl-accounts/{id}/statement?from=&to=` | `ledger.read` |
| GET | `/reconciliation?date=` | `ledger.read` |

### Tables (`ledger` schema)
`gl_accounts`, `posting_rules`, `posting_rule_lines`, `journal_entries`, `journal_lines`, `gl_daily_balances`, `accounting_periods`, `reconciliation_results`, `outbox_messages`, `inbox_messages`

## 17. Loans Module

**Purpose:** Full lending lifecycle for individual and business loans (and the individual member loans inside MFI group cycles).

### Domain model

| Aggregate | Key fields |
|---|---|
| `LoanProduct` | Id, InstitutionId, Code, Currency, MinAmount, MaxAmount, MinTerm, MaxTerm, InterestMethod (Flat/DecliningBalance), AnnualRate (min/default/max), RepaymentFrequency, GracePeriod (principal/interest), Fees[], PenaltyRate, PenaltyGraceDays, AllocationOrder, RequiresCollateral, RequiresGuarantor, IsGroupProduct |
| `LoanApplication` | Id, CustomerId, ProductId, RequestedAmount, RequestedTerm, Purpose, Collateral[], Guarantors[], Assessment, Status, OfficerId, GroupCycleId? |
| `CreditAssessment` (VO) | Score, Grade (A–E), DebtToIncome, ExistingExposure, Recommendation, Notes |
| `Loan` | Id, LoanNumber, ApplicationId, CustomerId, ProductId, Principal, AnnualRate, Term, Status, DisbursedOn, DisbursementAccountId, RepaymentAccountId, Schedule, Outstanding (Principal/Interest/Fees/Penalty), DaysPastDue, Bucket, GroupCycleId? |
| `Installment` | Number, DueDate, PrincipalDue, InterestDue, FeesDue, PenaltyDue, Paid amounts, Status (Pending/PartiallyPaid/Paid/Overdue) |
| `LoanTransaction` | Type (Disbursement, Repayment, FeeCharge, PenaltyCharge, Waiver, WriteOff, Reversal), Amount, Allocation breakdown, ValueDate |

### State machines

```
LoanApplication:
  Draft ──► Submitted ──► UnderAssessment ──► PendingApproval ──► Approved ──► (Loan created)
                                   │                 │
                                   └──► Rejected ◄───┘          Withdrawn (by customer)

Loan:
  Approved ──► Disbursed/Active ──► Closed (fully repaid)
                    │      ▲
                    ▼      │
                 Overdue ──┘
                    │
                    ├──► Restructured ──► Active
                    └──► WrittenOff ──► (Recovered)
```

### Business rules
- Applicant must have `KycStatus = Approved` and an active deposit account (for disbursement/repayment).
- Amount, term and rate must be within product limits (validator + aggregate).
- **Credit scoring** (simulated, explainable scorecard): income vs installment (DTI ≤ 40%), existing exposure from `ILoansModuleApi`, account history from `IAccountsModuleApi`, customer risk rating, collateral coverage. Output grade A–E with reasons.
- Approval routes through **maker-checker** with approval limits; amounts above a threshold require 2 checkers.
- Disbursement net of upfront fees (configurable: deduct or capitalize).
- **Schedule** generated at disbursement from the business date; due dates adjusted for holidays.
- **Repayment allocation order** (default): Penalty → Fees → Interest → Principal, oldest installment first.
- **Prepayment:** extra principal either reduces term or reduces installment (product setting); interest recalculated for declining balance.
- **Delinquency:** `DaysPastDue` from the oldest unpaid installment. Buckets: Current, 1–30, 31–60, 61–90, 91–180, 180+.
- **Penalty** accrues daily on overdue amounts after `PenaltyGraceDays`.
- **Provisioning** per bucket (e.g., 1%, 5%, 25%, 50%, 100%) computed at month-end (simplified; not full IFRS 9 ECL).
- **Write-off** after 180+ DPD with approval; recoveries post to income.
- **Auto-debit:** on due date, a job attempts to collect from the repayment account (respecting available balance; partial collection allowed).

### Endpoints

| Method | Route | Permission |
|---|---|---|
| POST/GET | `/loan-products` | `products.manage` / authenticated |
| POST | `/loan-products/{id}/schedule-preview` | authenticated |
| POST | `/loan-applications` | `loans.apply` |
| GET | `/loan-applications/{id}` | `loans.read` |
| POST | `/loan-applications/{id}/submit` | `loans.apply` |
| POST | `/loan-applications/{id}/assess` | `loans.assess` |
| POST | `/loan-applications/{id}/request-approval` | `loans.assess` |
| POST | `/loans/{id}/disburse` | `loans.disburse` (Idempotency-Key) |
| GET | `/loans/{id}` | `loans.read` |
| GET | `/loans/{id}/schedule` | `loans.read` |
| POST | `/loans/{id}/repayments` | `loans.repay` (Idempotency-Key) |
| POST | `/loans/{id}/prepayments` | `loans.repay` (Idempotency-Key) |
| POST | `/loans/{id}/waivers` | `loans.waive` (maker-checker) |
| POST | `/loans/{id}/restructure` | `loans.restructure` (maker-checker) |
| POST | `/loans/{id}/write-off` | `loans.writeoff` (maker-checker) |
| GET | `/customers/{customerId}/loans` | `loans.read` |
| GET | `/loans/portfolio?branchId=&bucket=` | `loans.read` |

### Tables (`loans` schema)
`loan_products`, `loan_product_fees`, `loan_applications`, `collaterals`, `guarantors`, `credit_assessments`, `loans`, `installments`, `loan_transactions`, `loan_transaction_allocations`, `provisions`, `outbox_messages`, `inbox_messages`

## 18. Microfinance Module

**Purpose:** MFI-specific operations around **group lending** with joint liability. It *orchestrates* lending but does **not** own loans — each member's loan is a normal `Loan` in the Loans module linked by `GroupCycleId`. This is the module that best demonstrates clean boundaries.

### Domain model

| Aggregate | Key fields |
|---|---|
| `Center` | Id, BranchId, Name, FieldOfficerId, MeetingDay, MeetingTime, Location, Status |
| `Group` | Id, CenterId, Name, Members[], Leader/Secretary/Treasurer roles, FormationDate, Status, CycleNumber |
| `Membership` | CustomerId, Role, JoinedOn, LeftOn, Status |
| `GroupLoanCycle` | Id, GroupId, CycleNumber, ProductId, MemberRequests[] (CustomerId, Amount), Status, LoanIds[] |
| `Meeting` | Id, CenterId, ScheduledDate, HeldOn, Attendance[], Status |
| `CollectionSheet` | Id, MeetingId, Lines[] (CustomerId, LoanId, InstallmentDue, SavingsDue, AmountCollected, SavingsCollected), Totals, Status (Draft/Submitted/Verified/Posted), FieldOfficerId |
| `CompulsorySavingsRule` | ProductId, PercentOfLoan or FixedPerMeeting |

### Business rules
- Group size 5–30 members (configurable); a customer can belong to only one active group per institution.
- All members must be KYC-approved and hold a savings account (compulsory savings).
- Group must complete pre-loan training / formation period (configurable N meetings) before first cycle.
- **Joint liability:** a new cycle can't start while any member of the group has an overdue loan; members' compulsory savings may be blocked (hold) as cash collateral.
- Cycle N+1 max loan amount = previous cycle amount × growth factor, only if repayment was 100% on time.
- **Collection sheet** generated per meeting from Loans/Accounts Contracts; field officer records collected amounts (offline-capable in UI); a supervisor verifies; posting emits `CollectionSheetPosted`, and Loans + Accounts each apply their part idempotently.
- **PAR (Portfolio at Risk):** `PAR30 = outstanding principal of loans with DPD > 30 / total outstanding principal`, reported per officer, center, branch.

### Group cycle flow

```
Microfinance                         Loans                                 Accounts
   │ CreateGroupLoanCycle              │                                       │
   │ (member requests)                 │                                       │
   │── GroupLoanCycleRequested ───────►│ creates one LoanApplication per member│
   │                                   │ (GroupCycleId set), approval as batch │
   │◄── LoanApproved (×N) ─────────────│                                       │
   │◄── LoanDisbursed (×N) ────────────│── LoanDisbursed ─────────────────────►│ credits each member's savings
   │ cycle → Active                    │                                       │
   │ weekly meetings + collection sheet│                                       │
   │── CollectionSheetPosted ─────────►│ apply repayments ─────────────────────► apply savings deposits
```

### Endpoints

| Method | Route | Permission |
|---|---|---|
| POST/GET | `/centers` | `mfi.groups.manage` / `mfi.read` |
| POST | `/centers/{id}/groups` | `mfi.groups.manage` |
| POST | `/groups/{id}/members` | `mfi.groups.manage` |
| DELETE | `/groups/{id}/members/{customerId}` | `mfi.groups.manage` |
| POST | `/groups/{id}/loan-cycles` | `mfi.groups.manage` |
| GET | `/groups/{id}/loan-cycles/{cycleId}` | `mfi.read` |
| GET | `/centers/{id}/meetings?from=&to=` | `mfi.read` |
| GET | `/meetings/{id}/collection-sheet` | `mfi.collections.record` |
| PUT | `/collection-sheets/{id}` | `mfi.collections.record` |
| POST | `/collection-sheets/{id}/submit` | `mfi.collections.record` |
| POST | `/collection-sheets/{id}/verify` | `mfi.collections.verify` |
| GET | `/mfi/par?branchId=&officerId=&date=` | `mfi.read` |

### Tables (`mfi` schema)
`centers`, `groups`, `memberships`, `group_loan_cycles`, `cycle_member_requests`, `meetings`, `meeting_attendance`, `collection_sheets`, `collection_sheet_lines`, `savings_rules`, `outbox_messages`, `inbox_messages`

## 19. Cards Module

**Purpose:** Credit card issuing and servicing. Payment networks and card processors are **simulated**.

### Domain model

| Aggregate | Key fields |
|---|---|
| `CardProduct` | Id, Code, Network (simulated: "FinPay"), Currency, AnnualFee, PurchaseAPR, CashAdvanceAPR, PenaltyAPR, GracePeriodDays, BillingCycleDay, MinPaymentPercent, MinPaymentFloor, LateFee, OverLimitFee, CashAdvanceFee, RewardsRate |
| `CardApplication` | Id, CustomerId, ProductId, RequestedLimit, DeclaredIncome, Assessment, ApprovedLimit, Status |
| `CardAccount` | Id, CustomerId, ProductId, CreditLimit, AvailableCredit, Balances (Purchase, CashAdvance, Interest, Fees), CurrentCycle, Status, DelinquencyCycles, RewardsPoints, AutoPayAccountId?, AutoPayMode (Min/Full) |
| `Card` | Id, CardAccountId, Token, MaskedPan (`**** **** **** 1234`), Last4, ExpiryMonth/Year, Type (Primary/Supplementary), Status (Inactive/Active/Blocked/Lost/Stolen/Expired), Controls (online, international, ATM, limits) |
| `Authorization` | Id, CardId, Amount, MerchantName, MCC, Status (Approved/Declined/Expired/Cleared), DeclineReason, HoldExpiresAt |
| `CardTransaction` | Id, CardAccountId, Type (Purchase, CashAdvance, Refund, Payment, Fee, Interest, Adjustment), Amount, PostingDate, TransactionDate, AuthorizationId?, StatementId? |
| `Statement` | Id, CardAccountId, CycleStart, CycleEnd, OpeningBalance, Purchases, Payments, Fees, Interest, ClosingBalance, MinimumDue, DueDate, Status |
| `Dispute` | Id, CardTransactionId, Reason, Status (Open/ProvisionalCredit/Won/Lost), Amount |

### Card lifecycle

```
CardApplication: Submitted ──► Assessed ──► PendingApproval ──► Approved ──► (CardAccount + Card issued)
                                                     └──► Declined
Card:  Inactive ──► Active ⇄ TemporarilyBlocked
                      │
                      ├──► Lost/Stolen ──► (replacement card issued)
                      └──► Expired / Closed
```

### Business rules
- **PCI-awareness:** never store a full PAN or CVV. Generate a test PAN with a valid Luhn digit, immediately tokenize it, keep only `Token`, `MaskedPan`, `Last4`. Real systems use a vault/HSM — document this boundary with `ICardVault`.
- **Credit limit** from income, scoring grade and existing exposure; changes go through maker-checker.
- **Authorization** (simulated endpoint `/card-network/authorize`): checks card status, controls, expiry, available credit (incl. over-limit tolerance), velocity rules; approved auth creates a hold reducing `AvailableCredit`; holds expire after N days if not cleared.
- **Clearing** (simulated batch file / endpoint) converts auths into posted `CardTransaction`s; amount can differ slightly from auth.
- **Billing cycle:** on cycle day, generate statement: `ClosingBalance`, `MinimumDue = max(MinPaymentFloor, ClosingBalance × MinPaymentPercent + interest + fees)` (capped at closing balance), `DueDate = CycleEnd + GracePeriodDays`.
- **Interest:** average daily balance method; no purchase interest if the previous statement was paid in full by due date (grace period); cash advances accrue from transaction date.
- **Payment allocation:** minimum portion → highest-APR balance first for the excess (configurable).
- **Late fee** if `MinimumDue` not paid by due date; 2+ consecutive misses → penalty APR; 3+ → card blocked; 180+ days → charge-off.
- **Rewards:** points = eligible purchases × RewardsRate; reversed on refunds.
- **AutoPay** debits the linked deposit account on due date via Accounts.

### Endpoints

| Method | Route | Permission |
|---|---|---|
| POST/GET | `/card-products` | `products.manage` / authenticated |
| POST | `/card-applications` | `cards.apply` |
| POST | `/card-applications/{id}/assess` | `cards.assess` |
| POST | `/card-applications/{id}/request-approval` | `cards.assess` |
| GET | `/card-accounts/{id}` | `cards.read` |
| POST | `/card-accounts/{id}/limit-change` | `cards.limit.request` (maker-checker) |
| POST | `/cards/{id}/activate` | `cards.manage` or customer |
| POST | `/cards/{id}/block` / `/unblock` | `cards.manage` or customer |
| POST | `/cards/{id}/report-lost` | `cards.manage` or customer |
| PUT | `/cards/{id}/controls` | `cards.manage` or customer |
| POST | `/card-network/authorize` | simulator client credential |
| POST | `/card-network/clearing` | simulator client credential |
| POST | `/card-accounts/{id}/payments` | `cards.payments` (Idempotency-Key) |
| GET | `/card-accounts/{id}/transactions` | `cards.read` |
| GET | `/card-accounts/{id}/statements` | `cards.read` |
| GET | `/statements/{id}?format=pdf` | `cards.read` |
| POST | `/card-transactions/{id}/disputes` | `cards.disputes` |

### Tables (`cards` schema)
`card_products`, `card_applications`, `card_accounts`, `cards`, `card_controls`, `authorizations`, `card_transactions`, `statements`, `statement_lines`, `interest_accruals`, `rewards_ledger`, `disputes`, `outbox_messages`, `inbox_messages`

## 20. Notifications Module

**Purpose:** Turns integration events into customer and staff messages. Pure subscriber — owns no business state except templates and delivery logs.

- `NotificationTemplate` (EventType, Channel [Email/SMS/Push/InApp], Language, Subject, Body with `{{placeholders}}`).
- `OutboundMessage` (Recipient, Channel, Status [Queued/Sent/Failed], Attempts, ProviderRef).
- Channels behind `IEmailSender` / `ISmsSender` / `IPushSender` (Expo Push API for the mobile app); dev uses MailPit/console sinks. Push payloads never contain sensitive data.
- Retries with exponential backoff (Polly via `Microsoft.Extensions.Http.Resilience`).
- Customer preferences (opt-out for marketing; transactional always sent).

Endpoints: `GET/PUT /notification-templates`, `GET /notifications?recipient=`. Tables (`notifications` schema): `templates`, `outbound_messages`, `preferences`, `push_receipts`, `inbox_messages`.

## 21. Reporting Module

**Purpose:** Read-optimized projections built from integration events, so heavy queries never hit operational tables.

| Report / Read model | Built from |
|---|---|
| Customer 360 summary | Customer, Account, Loan, Card events |
| Daily transaction volume per branch/channel | `AccountTransactionPosted`, `CardTransactionPosted` |
| Loan portfolio & aging buckets | `LoanDisbursed`, `LoanRepaymentReceived`, `LoanBecameDelinquent` |
| PAR by officer/center/branch | Loans + Microfinance events |
| Deposit book by product | Account events |
| Card utilization & revenue | Card events |
| Trial balance snapshot | `JournalEntryPosted`, `EndOfDayCompleted` |

Exports: CSV/XLSX (ClosedXML) and PDF (QuestPDF — check its community-license terms). Endpoints under `/reports/*`, permission `reports.read`. Projections are rebuildable by replaying stored events (`reporting.event_store`).

---

# PART 4 — CROSS-CUTTING CONCERNS

## 22. End-to-End Business Flows

### 22.1 Digital account opening (new customer, online)

```
Customer Portal        Accounts                Customers & KYC          Identity            Ledger / Notif.
     │ POST /account-applications │                     │                    │                     │
     │───────────────────────────►│ Started             │                    │                     │
     │                            │── create prospect ─►│ (via command API   │                    │
     │                            │                     │  in Contracts)     │                     │
     │ upload ID, selfie, POA ────────────────────────► │ KycCase Submitted  │                     │
     │                            │                     │ screening (sim.)   │                     │
     │                            │                     │ risk = Low → auto  │                     │
     │                            │◄─ CustomerKycApproved ─────────────────  │                     │
     │                            │ create Account (PendingFunding)          │                     │
     │ initial deposit ──────────►│ Active, AccountOpened ─────────────────────────────────────────►│ GL + welcome email
```
High-risk path: KYC goes to `PendingReview` → Compliance Officer approves → application → `PendingApproval` (maker-checker in Identity) → continues.

> Note: The one place a module *commands* another synchronously is "create prospect customer". Expose it as a narrow method on `ICustomersModuleApi` (`CreateProspectAsync`) — documented as an exception, and replaced by a saga step if extracted.

### 22.2 Loan disbursement

1. `POST /loans/{id}/disburse` with `Idempotency-Key`.
2. `Loan.Disburse()` → status `Active`, schedule generated, `LoanDisbursedDomainEvent` → outbox (same transaction).
3. **Accounts** handles `LoanDisbursedIntegrationEvent`: credits the customer account with type `LoanDisbursement`, reference = LoanId (inbox guarantees one credit).
4. **Ledger** handles it: posting rule `LoanDisbursement` → Dr 1.2.01 / Cr 2.1.01 (and fee lines).
5. **Notifications** sends disbursement SMS; **Microfinance** marks member loan disbursed if `GroupCycleId` present.

### 22.3 Card purchase → statement → payment

1. Simulator calls `/card-network/authorize` → hold, `AvailableCredit` reduced.
2. Clearing batch posts `CardTransaction` → `CardTransactionPosted` → Ledger Dr 1.3.01 / Cr 2.2.01.
3. Cycle-day job generates `Statement` → `CardStatementGenerated` → email.
4. Customer pays from deposit account: Accounts debits (event) → Cards applies payment → Ledger posts.

### 22.4 Failure handling (compensation)
If Accounts cannot credit a disbursement (e.g., account frozen), it publishes `LoanDisbursementCreditFailed`; Loans reverses the disbursement (`Loan.ReverseDisbursement()`), and Ledger posts the reversal. Every multi-module flow documents its compensation path in `docs/adr`.

## 23. Financial Calculations

All calculators are pure domain services with exhaustive unit tests (golden-file test cases).

### 23.1 Declining-balance (reducing) installment — EMI

```
r = AnnualRate / periodsPerYear          (12 monthly, 26 bi-weekly, 52 weekly)
EMI = P × r × (1 + r)^n / ((1 + r)^n − 1)

For each period k:
  interest_k  = outstanding_(k-1) × r
  principal_k = EMI − interest_k
  outstanding_k = outstanding_(k-1) − principal_k
Last installment absorbs rounding residue.
```
Example: P = 10,000, 12% p.a., 12 monthly → r = 0.01, EMI ≈ **888.49**, total interest ≈ 661.88.

> `decimal` has no `Pow` for fractional exponents; for integer `n`, compute `(1 + r)^n` by repeated multiplication in `decimal` to avoid `double` precision loss.

### 23.2 Flat interest

```
TotalInterest = P × AnnualRate × (TermInMonths / 12)
Installment   = (P + TotalInterest) / n
```
Example: same loan → interest 1,200, installment **933.33**. Common in MFIs; show the **effective annual rate** alongside for transparency.

### 23.3 Daily interest accrual (deposits, loans)

```
DailyInterest = Balance × AnnualRate / DaysInYear     (Actual/365, Actual/360, or 30/360 per product)
```
Accrue at full precision, post rounded at capitalization/repayment.

### 23.4 Penalty interest

```
DailyPenalty = OverdueAmount × PenaltyRate / DaysInYear     (only when DPD > PenaltyGraceDays)
```

### 23.5 Credit card interest — average daily balance

```
ADB = Σ(daily balance for each day in cycle) / daysInCycle
Interest = ADB × APR / 365 × daysInCycle
```
Computed per balance segment (purchase, cash advance) with its own APR; purchase segment is 0 if grace conditions met.

### 23.6 Minimum payment

```
MinimumDue = min( ClosingBalance,
                  max( MinPaymentFloor,
                       ClosingBalance × MinPaymentPercent + InterestCharged + FeesCharged ) )
```

### 23.7 Portfolio at Risk

```
PAR_x = Σ outstanding principal of loans with DPD > x / Σ outstanding principal of all active loans
```

### 23.8 Credit scorecard (simulated)

| Factor | Points |
|---|---|
| DTI ≤ 20% / 21–35% / 36–40% / > 40% | 30 / 20 / 10 / reject |
| Customer tenure ≥ 2y / 6m–2y / < 6m | 15 / 10 / 0 |
| Previous loans repaid on time | +20 |
| Current DPD > 0 on any facility | reject |
| Risk rating Low / Medium / High | 15 / 5 / 0 |
| Collateral coverage ≥ 100% | +20 |

Grade: A ≥ 80, B 65–79, C 50–64, D 35–49 (manual review), E < 35 (reject). Keep each factor's contribution in `CreditAssessment.Reasons` for explainability.

## 24. Security & Compliance

| Area | Approach |
|---|---|
| Authentication | OIDC (Keycloak locally, Cognito on AWS); React apps sign in through the BFF (Authorization Code + PKCE, tokens kept server-side, HttpOnly `__Host-` cookie, CSRF header); API validates JWT bearer tokens forwarded by the BFF |
| Authorization | Permission policies + branch scoping (officers see only their branch unless `scope.all-branches`) + resource ownership checks for customers |
| Maker-checker | Identity module approval engine for limit-sensitive operations |
| PII protection | National ID, DOB, phone encrypted at rest (EF value converter + ASP.NET Core Data Protection keys); masked in logs and responses (`***-**-1234`) |
| Mobile app | PKCE via system browser, refresh token in Keychain/Keystore, biometric app lock and step-up for sensitive actions, device registration & revocation, screenshot blocking, minimum app version — see §31.4–31.7 |
| Card data | Tokenization only; no PAN/CVV storage; `ICardVault` abstraction |
| Secrets | User Secrets (dev), environment variables / Key Vault (prod); never in appsettings |
| Transport | HTTPS everywhere, HSTS, secure cookies |
| API protection | Rate limiting (`Microsoft.AspNetCore.RateLimiting`) per client, request size limits, CORS allow-list |
| Audit | Every command logged in `audit_log` (who, what, when, before/after hash, correlation id); ledger is append-only |
| AML (simulated) | Rules on transactions: large cash (> threshold), structuring (many just-below-threshold deposits), rapid in/out — raises alerts for Compliance review |
| Data retention | Soft delete for customer-facing entities; financial records never deleted |
| Dependency scanning | `dotnet list package --vulnerable` in CI, Dependabot |

## 25. Reliability: Idempotency, Concurrency, Outbox

- **Idempotency keys:** all money-moving POSTs require an `Idempotency-Key` header. `IdempotencyFilter` stores key + request hash + response for 24h; a replay returns the stored response, a different body with the same key returns `422`.
- **Optimistic concurrency:** `xmin` concurrency token on aggregates; conflicts return `409` and the client retries.
- **Pessimistic locking** only for hot balance rows where needed (`SELECT … FOR UPDATE` via raw SQL in the repository) — documented per use.
- **Transactional outbox:** events written with state changes; publisher job marks `processed_on_utc`; failed messages retry with backoff and land in a dead-letter view after N attempts.
- **Inbox:** each handler records `(message_id, handler_name)`; duplicates are skipped → effectively-once processing.
- **Retries** for transient DB errors via `EnableRetryOnFailure` (with care around user-initiated transactions).

## 26. Background Jobs & End-of-Day Processing

Quartz.NET with persistent job store in `org` schema; jobs are registered by each module.

| Job | Module | Schedule |
|---|---|---|
| ProcessOutbox | every module | every 2 s |
| ExpireCardAuthorizations | Cards | hourly |
| KycDocumentExpiryCheck | Customers | daily |
| ExpireApprovalRequests | Identity | daily |
| **End of Day** | Organization (orchestrator) | manual or scheduled |

### EOD sequence (per institution)

```
1. Close business day for new postings (cut-off flag)
2. Accounts:  accrue deposit interest, capitalize if due, dormancy check, FD maturity
3. Loans:     auto-debit due installments, update DPD/buckets, accrue interest & penalties
4. Cards:     accrue interest, close billing cycles due today, late fees, autopay
5. Microfinance: refresh PAR snapshot, generate next meetings
6. Ledger:    post all pending event entries, compute daily GL balances
7. Ledger:    reconcile sub-ledgers vs control accounts (must be zero difference)
8. Month-end (if applicable): loan provisioning, close accounting period
9. Advance business date → publish BusinessDateAdvanced + EndOfDayCompleted
```

Each step is recorded in `eod_run_steps` with status and is **restartable**: a failed step can be retried without repeating completed ones (every step is idempotent for a given business date). EOD steps are invoked through module Contracts (`IEodParticipant` implemented by each module).

## 27. Observability

- **Logging:** Serilog structured logs, enriched with `CorrelationId`, `Module`, `UserId`, `InstitutionId`; PII destructuring policies mask sensitive fields.
- **Tracing:** OpenTelemetry for ASP.NET Core, HttpClient, EF Core (Npgsql), Quartz; custom `ActivitySource` per module; outbox messages carry `traceparent` so traces span async flows.
- **Metrics:** custom meters — `fincore.loans.disbursed.amount`, `fincore.accounts.transactions.count`, `fincore.outbox.pending`, `fincore.eod.duration`.
- **Health checks:** `/health/live`, `/health/ready` (Postgres, Redis, Keycloak, outbox backlog threshold).
- **Dashboards:** Aspire dashboard in dev; Grafana/Prometheus or Azure Monitor in hosted environments.

## 28. API Conventions

- Routes: `/api/v{version}/{resource}` with plural nouns; actions as sub-resources (`/loans/{id}/disburse`).
- Versioning via `Asp.Versioning.Http` (URL segment).
- JSON: camelCase, enums as strings, money as `{ "amount": "1250.00", "currency": "USD" }` (amount as string to avoid JS float issues), dates `yyyy-MM-dd` (`DateOnly`) for business dates, UTC ISO-8601 for timestamps.
- Errors: RFC 9457 `ProblemDetails` with `type`, `title`, `status`, `detail`, `errors` (validation), `code` (e.g. `Loans.InsufficientFunds`), `traceId`.
- Pagination: `?page=1&pageSize=25` (max 100), response `{ items, page, pageSize, totalCount }`.
- Filtering/sorting: explicit query parameters, no dynamic LINQ from user input.
- Headers: `Idempotency-Key` (money movement), `X-Correlation-Id` (optional, echoed back), `ETag`/`If-Match` for updates where useful.
- OpenAPI documents per module group, browsable via Scalar.

## 29. Testing Strategy

| Layer | Tool | What |
|---|---|---|
| Unit — Domain | xUnit v3, Shouldly | Aggregate invariants, state transitions, calculators (golden schedules) |
| Unit — Application | xUnit, NSubstitute | Handlers with mocked repositories/Contracts |
| Architecture | NetArchTest.Rules | Domain has no EF/ASP.NET deps; modules reference only other modules' `.Contracts`; handlers are `internal sealed` |
| Integration (per module) | `WebApplicationFactory`, Testcontainers (Postgres), Respawn | Endpoints → DB, migrations apply cleanly, outbox written |
| Event flow | In-memory bus + Testcontainers | Publish event → assert subscriber state (e.g., disbursement credits account and posts GL) |
| End-to-end | `FinCore.EndToEndTests` | Full journeys: onboard → open account → loan → repay → EOD → trial balance balances |
| Property-based (optional) | FsCheck | Σ debits = Σ credits for random posting sequences; schedule principal sums to P |
| Load (optional) | k6 / NBomber | Transfers and card authorizations throughput |

Rules: every bug fix adds a test; financial calculators target 100% branch coverage; CI fails under 80% line coverage on Domain projects.

### Example architecture test

```csharp
[Fact]
public void Loans_should_only_depend_on_other_modules_contracts()
{
    var otherModules = new[] { "Customers", "Accounts", "Cards", "Ledger", "Microfinance" }
        .SelectMany(m => new[] { $"FinCore.Modules.{m}.Domain", $"FinCore.Modules.{m}.Application",
                                 $"FinCore.Modules.{m}.Infrastructure" })
        .ToArray();

    var result = Types.InAssemblies(LoansAssemblies.All)
        .ShouldNot().HaveDependencyOnAny(otherModules)
        .GetResult();

    result.IsSuccessful.ShouldBeTrue();
}
```

## 30. Front End (React)

FinCore ships two React single-page applications in a **pnpm monorepo** under `frontend/`, both talking to the .NET API through a **Backend-for-Frontend (BFF)** written in ASP.NET Core.

| App | Users | Purpose |
|---|---|---|
| `apps/backoffice` | Staff (all internal personas) | Operations: customers, KYC review, accounts, loans, MFI groups, cards, ledger, approvals, admin, EOD |
| `apps/portal` | Customers (desktop web) | Self-service: online account opening, balances, transfers, loan applications, card controls, statements |
| `apps/mobile` | Customers (iOS/Android) | The primary customer channel, built with React Native + Expo — see §31 |

### 30.1 Front-end technology stack

| Concern | Choice | Notes |
|---|---|---|
| Framework | React 19 + TypeScript (strict) | Function components and hooks only |
| Build / dev server | Vite | Fast HMR; dev proxy to the BFF |
| Monorepo | pnpm workspaces + Turborepo | Shared packages, cached builds |
| Routing | TanStack Router | Type-safe routes, search-param validation, route-level data loading |
| Server state | TanStack Query v5 | Caching, retries, invalidation after mutations |
| Tables | TanStack Table | Server-side paging, sorting, filtering for large lists |
| Forms | React Hook Form + Zod | Zod schemas shared with generated API types |
| UI components | shadcn/ui (Radix primitives) + Tailwind CSS v4 | Accessible components you own in `packages/ui` |
| Charts | Recharts | Dashboards, PAR trends, portfolio mix |
| API client | Orval (generated from the API's OpenAPI documents) | Typed fetchers + TanStack Query hooks + Zod schemas per module |
| Money | `decimal.js` | No JavaScript `number` arithmetic on money |
| Dates | `date-fns` | Business dates handled as `yyyy-MM-dd` strings |
| i18n | `react-i18next` | English default; locale-aware money/date formatting via `Intl` |
| Offline (field officers) | `vite-plugin-pwa` + IndexedDB queue | Collection sheets usable without connectivity |
| Testing | Vitest, React Testing Library, MSW, Playwright | Unit, component, mocked-API and end-to-end |
| Quality | ESLint (flat config), Prettier, `tsc --noEmit`, Storybook (optional) | Enforced in CI |

### 30.2 Front-end folder structure

```
frontend/
├── package.json                    # root scripts: dev, build, lint, test, api:generate
├── pnpm-workspace.yaml             # apps/*, packages/*
├── turbo.json
├── tsconfig.base.json
├── eslint.config.js
├── .prettierrc
├── orval.config.ts                 # one output per API module
├── apps/
│   ├── backoffice/
│   │   ├── index.html
│   │   ├── vite.config.ts          # dev proxy: /api, /bff → BackOffice BFF
│   │   ├── playwright.config.ts
│   │   ├── e2e/                    # Playwright journeys
│   │   │   ├── onboarding.spec.ts
│   │   │   ├── loan-lifecycle.spec.ts
│   │   │   └── maker-checker.spec.ts
│   │   └── src/
│   │       ├── main.tsx
│   │       ├── app/
│   │       │   ├── providers.tsx    # QueryClient, Router, Theme, i18n, Toaster
│   │       │   ├── queryClient.ts
│   │       │   └── router.tsx
│   │       ├── routes/              # TanStack Router file-based routes
│   │       │   ├── __root.tsx
│   │       │   ├── index.tsx        # dashboard
│   │       │   ├── customers/
│   │       │   ├── accounts/
│   │       │   ├── loans/
│   │       │   ├── microfinance/
│   │       │   ├── cards/
│   │       │   ├── ledger/
│   │       │   ├── approvals/
│   │       │   ├── reports/
│   │       │   └── admin/
│   │       ├── features/            # one folder per backend module
│   │       │   ├── customers/
│   │       │   │   ├── components/  # CustomerSearch, Customer360Card, …
│   │       │   │   ├── hooks/       # wrappers over generated query hooks
│   │       │   │   ├── forms/       # CreateCustomerForm + zod schema
│   │       │   │   └── index.ts     # public surface of the feature
│   │       │   ├── kyc/
│   │       │   ├── accounts/
│   │       │   ├── loans/
│   │       │   ├── microfinance/
│   │       │   ├── cards/
│   │       │   ├── ledger/
│   │       │   ├── approvals/
│   │       │   ├── reports/
│   │       │   └── admin/
│   │       ├── layouts/             # AppShell, Sidebar (permission-aware), TopBar
│   │       ├── lib/                 # app-specific helpers
│   │       └── test/
│   │           ├── setup.ts
│   │           └── msw/handlers.ts
│   └── portal/
│       ├── index.html
│       ├── vite.config.ts           # dev proxy: /api, /bff → Portal BFF
│       ├── e2e/
│       └── src/
│           ├── main.tsx
│           ├── app/
│           ├── routes/
│           └── features/
│               ├── onboarding/      # account-opening wizard, document upload
│               ├── dashboard/
│               ├── accounts/
│               ├── transfers/
│               ├── loans/
│               ├── cards/
│               ├── statements/
│               └── profile/
└── packages/
    ├── api-client/                  # GENERATED — do not edit by hand
    │   └── src/
    │       ├── http.ts              # configurable fetch wrapper (cookie+CSRF or bearer), ProblemDetails parsing
    │       └── generated/
    │           ├── customers/
    │           ├── accounts/
    │           ├── loans/
    │           ├── microfinance/
    │           ├── cards/
    │           ├── ledger/
    │           ├── identity/
    │           └── organization/
    ├── ui/                          # shadcn/ui components + financial widgets
    │   └── src/
    │       ├── components/          # Button, Dialog, Form, Table, Tabs, …
    │       ├── data-table/          # DataTable with server paging
    │       ├── money/               # MoneyInput, MoneyDisplay, CurrencySelect
    │       ├── status/              # StatusBadge (loan, account, card, KYC states)
    │       ├── stepper/             # Wizard steps for onboarding & applications
    │       └── document-viewer/     # KYC document preview
    ├── auth/                        # BFF session hooks & guards
    │   └── src/
    │       ├── useSession.ts        # GET /bff/user
    │       ├── RequireAuth.tsx
    │       └── Can.tsx              # <Can permission="loans.approve">
    ├── permissions/                 # backend permission constants (shared with mobile)
    ├── i18n/                        # translation JSON + error-code messages (shared with mobile)
    ├── design-tokens/               # single token source for web & mobile (§32)
    ├── money/                       # decimal.js helpers: parse, add, format
    └── config/                      # shared tsconfig, eslint, tailwind presets
```

**Rule of thumb:** `features/*` mirror backend modules, and a feature imports another feature only through its `index.ts`, the same boundary idea as the backend `.Contracts` projects. An ESLint `no-restricted-imports` rule enforces it.

### 30.3 Authentication: the BFF pattern

Financial apps should not keep OAuth tokens in browser JavaScript. The SPA therefore never sees an access token:

```
 Browser (React SPA)                FinCore.Bff (ASP.NET Core)            Keycloak        FinCore.Api
   │ GET /bff/user ───────────────────►│ cookie session?                     │                 │
   │◄─ 401 ────────────────────────────│                                     │                 │
   │ window.location = /bff/login ────►│ OIDC Authorization Code + PKCE ────►│                 │
   │                                   │◄── tokens (kept server-side) ───────│                 │
   │◄─ Set-Cookie: __Host-fincore (HttpOnly, Secure, SameSite=Strict) ───────│                 │
   │ fetch /api/v1/loans (cookie + X-CSRF: 1) ►│ YARP proxy + attach Bearer token ────────────►│
   │◄──────────────────────────────────────────│◄──────────────────────────────────────────────│
```

- `src/Gateway/FinCore.Bff` is a small ASP.NET Core host using cookie auth + OpenID Connect + **YARP** reverse proxy, with tokens stored server-side and refreshed automatically. (Duende BFF is a ready-made alternative but is commercially licensed.)
- One BFF project with three modes: **BackOffice** (client `fincore-backoffice`, staff identity pool), **Portal** (client `fincore-portal`, customer pool) and **Mobile** (token passthrough, §31.4). Locally each mode runs as its own launch profile; on AWS a single gateway container serves all three, choosing the mode by host header (`Bff:Sites` config) to save resources.
- BFF endpoints: `GET /bff/login?returnUrl=`, `POST /bff/logout`, `GET /bff/user` (claims + permissions + institution/branch + business date).
- **CSRF:** every `/api` call must carry `X-CSRF: 1`; the BFF rejects requests without it. Combined with `SameSite=Strict`, this blocks cross-site requests.
- Locally (or on non-AWS hosts) the BFF can serve the built SPA (`wwwroot`); on AWS the SPAs are served from S3 + CloudFront, which also routes `/api/*` and `/bff/*` to the gateway so the cookie stays same-origin. Either way apply SPA fallback routing and strict security headers (CSP, `X-Content-Type-Options`, `Referrer-Policy`, `frame-ancestors 'none'`).
- **Permissions in the UI** (hiding buttons, menu items) are convenience only; the API always enforces authorization.

### 30.4 API integration conventions

- **Code generation:** the API publishes an OpenAPI document per module (`/openapi/loans.json`, …). `pnpm api:generate` runs Orval to regenerate `packages/api-client`. CI regenerates and fails if the output differs from what's committed, so front end and back end can't silently drift apart.
- **Money:** the API sends amounts as strings (`"1250.00"`). The UI parses them with `decimal.js`, does all arithmetic in `Decimal`, and formats with `Intl.NumberFormat` using the currency's minor units. `MoneyInput` emits strings, never floats.
- **Idempotency:** money-moving forms (deposit, withdrawal, transfer, disbursement, repayment, card payment) create an `Idempotency-Key` with `crypto.randomUUID()` when the form opens and reuse it on retries. Double clicks or network retries can't post twice.
- **Errors:** the `http.ts` wrapper parses RFC 9457 `ProblemDetails`. Validation `errors` map onto React Hook Form fields via `setError`; business errors (`code: "Loans.InsufficientFunds"`) map to translated messages; `409` conflicts prompt a reload of the latest version.
- **Cache invalidation:** after a mutation, invalidate the affected query keys (e.g., repayment → loan, schedule, account transactions). Since cross-module effects are asynchronous (outbox), screens that depend on another module's reaction show a "processing" state and refetch briefly, or poll until the expected status appears.
- **Business date:** displayed in the top bar from `/bff/user`; date pickers for value dates default to it, not to the browser's clock.
- **Paging:** server-side through `page`/`pageSize`; table state lives in URL search params (TanStack Router validates them with Zod), so filtered views are shareable links.

### 30.5 Back-office screens

| Area | Screens |
|---|---|
| Dashboard | Business date, pending approvals count, today's disbursements & collections, PAR trend, deposit growth |
| Customers | Search, create, Customer 360 (accounts, loans, cards, KYC, activity timeline) |
| KYC | Case queue, document viewer side by side with captured data, screening hits, approve/reject/request info |
| Accounts | Opening wizard, account detail, transactions with filters, deposit/withdraw/transfer dialogs, holds, freeze/close, statement PDF |
| Loans | Application wizard with live schedule preview, assessment with scorecard reasons, portfolio and aging buckets, repayment, prepayment, restructure, write-off requests |
| Microfinance | Centers & groups, member management, cycle creation, meeting calendar, **collection sheet** (mobile-first, offline-capable), PAR by officer |
| Cards | Applications, card account overview, card controls, authorizations & transactions, statements, disputes, **authorization simulator** (to demo the network flow) |
| Ledger | Chart of accounts tree, journal browser, manual journal entry (balanced-entry validation as you type), trial balance, reconciliation |
| Approvals | Checker inbox with full payload and before/after diff, approve/reject with comment |
| Admin | Institutions, branches, products (deposit, loan, card), users & roles, approval limits, holidays, EOD run monitor with step status |

### 30.6 Customer portal screens

| Area | Screens |
|---|---|
| Onboarding | Product picker → personal details → ID & proof-of-address upload (camera capture on mobile) → review → status tracker → initial funding |
| Dashboard | Balances across accounts, loans and cards; upcoming due dates |
| Accounts | Transactions, statements download, account details |
| Transfers | Own-account and internal transfers with confirmation step |
| Loans | Eligibility check, application with schedule preview, application status, repayment schedule, make a repayment |
| Cards | Activate, freeze/unfreeze, online/international toggles, spending limits, statements, pay balance (minimum / full / custom) |
| Profile | Contact details, KYC document renewal, notification preferences |

### 30.7 Offline collection sheets (MFI field officers)

Field officers often collect repayments where connectivity is poor:

1. Before leaving, the officer opens the day's meetings; the collection sheets are cached in IndexedDB.
2. Entries are recorded locally, each with a client-generated id and idempotency key.
3. A background sync pushes queued entries when back online (`PUT /collection-sheets/{id}`), then submits.
4. Conflicts (sheet already verified by a supervisor) are shown to the officer for resolution; nothing is silently overwritten.

### 30.8 Front-end testing

| Level | Tool | What |
|---|---|---|
| Unit | Vitest | Money helpers, schema validation, permission helpers |
| Component | Vitest + React Testing Library + MSW | Forms, tables and dialogs against mocked API responses (including ProblemDetails errors) |
| End-to-end | Playwright | Real journeys against the full stack (Aspire or docker compose): online account opening, loan lifecycle, maker-checker approval, card statement |
| Accessibility | `@axe-core/playwright` | No critical a11y violations on key pages |
| Visual (optional) | Storybook + Playwright screenshots | `packages/ui` components |

### 30.9 Running the front end

```bash
corepack enable              # provides pnpm
cd frontend
pnpm install
pnpm api:generate            # needs the API running (or uses committed specs)
pnpm dev                     # both apps; or: pnpm --filter backoffice dev
pnpm test                    # Vitest
pnpm e2e                     # Playwright (stack must be running)
pnpm build                   # production bundles → copied into BFF wwwroot
```

With Aspire, the AppHost starts the Vite dev servers too (via Aspire's JavaScript/Node.js hosting integration, e.g. `AddViteApp` or `AddNpmApp` depending on your Aspire version), so one `dotnet run` brings up everything.

## 31. Customer Mobile App (React Native + Expo)

The primary customer channel is a native **iOS and Android app** built with **React Native and Expo**. It lives in the same `frontend/` monorepo as `apps/mobile`, so it reuses the generated API client, money helpers, permission constants and translations from the web apps. The web Customer Portal (§30.6) stays available for desktop users and can be dropped if you only want mobile.

### 31.1 Mobile technology stack

| Concern | Choice | Notes |
|---|---|---|
| Framework | React Native + Expo (current SDK), TypeScript strict | New Architecture enabled (default in recent SDKs) |
| Navigation | Expo Router | File-based routes, typed routes, deep links (`fincore://`) |
| Server state | TanStack Query v5 | Same patterns as the web apps; persisted cache for read-only offline viewing |
| Forms | React Hook Form + Zod | Zod schemas shared with web |
| Styling | NativeWind (Tailwind for React Native) | Shares design tokens with the web Tailwind preset |
| API client | `packages/api-client` (Orval-generated) | Same generated code, configured for bearer tokens instead of cookies |
| Money | `packages/money` (`decimal.js`) | Identical rules to web |
| Auth | `expo-auth-session` (OIDC Authorization Code + PKCE against Keycloak) | Public client `fincore-mobile` |
| Secure storage | `expo-secure-store` | iOS Keychain / Android Keystore for refresh token |
| Biometrics | `expo-local-authentication` | App unlock and step-up confirmation for transfers |
| Camera & documents | `expo-camera`, `expo-image-picker`, `expo-document-picker`, `expo-image-manipulator` | KYC ID capture, selfie, proof of address; compress before upload |
| Push notifications | `expo-notifications` + Expo Push Service | Transaction alerts, due-date reminders, KYC status |
| Files | `expo-file-system`, `expo-sharing` | Download and share statement PDFs |
| Privacy | `expo-screen-capture` | Block screenshots on sensitive screens; blur app-switcher snapshot |
| Localization | `expo-localization` + `react-i18next` | Shared translation files with web |
| Builds & releases | EAS Build, EAS Submit, EAS Update | Store builds plus over-the-air JS updates per channel |
| Testing | Jest (`jest-expo`), React Native Testing Library, MSW, Maestro | Unit, component and on-device E2E flows |

> Check Expo's current SDK release notes when you start: module names are stable, but versions and defaults change with each SDK.

### 31.2 Mobile folder structure

```
frontend/apps/mobile/
├── app.config.ts                    # name, scheme "fincore", bundle ids, plugins, env per profile
├── eas.json                         # build profiles: development, preview, production
├── metro.config.js                  # monorepo-aware Metro config + NativeWind
├── babel.config.js
├── tailwind.config.js               # extends packages/config tailwind preset
├── global.css
├── app/                             # Expo Router routes
│   ├── _layout.tsx                  # providers: QueryClient, Auth, i18n, theme, push
│   ├── +not-found.tsx
│   ├── (auth)/
│   │   ├── welcome.tsx              # sign in / open an account
│   │   ├── sign-in.tsx
│   │   └── unlock.tsx               # biometric / PIN unlock on resume
│   ├── (onboarding)/                # online account opening
│   │   ├── _layout.tsx              # stepper header, progress saved server-side
│   │   ├── product.tsx
│   │   ├── personal-details.tsx
│   │   ├── id-document.tsx          # camera capture, front & back
│   │   ├── selfie.tsx
│   │   ├── proof-of-address.tsx
│   │   ├── review.tsx
│   │   └── status.tsx               # KYC / application status tracker
│   ├── (tabs)/
│   │   ├── _layout.tsx              # FloatingTabBar: Home | Accounts | (orb: Transfer) | Cards | More — §32.6
│   │   ├── index.tsx                # home: balances, upcoming dues, quick actions
│   │   ├── accounts/
│   │   │   ├── index.tsx
│   │   │   └── [accountId].tsx      # transactions (infinite scroll), details
│   │   ├── cards/
│   │   │   ├── index.tsx
│   │   │   └── [cardAccountId].tsx  # controls, freeze, statements, pay
│   │   ├── loans/
│   │   │   ├── index.tsx
│   │   │   ├── apply.tsx            # eligibility + schedule preview
│   │   │   └── [loanId].tsx         # schedule, repay
│   │   └── more/
│   │       ├── index.tsx
│   │       ├── profile.tsx
│   │       ├── security.tsx         # biometrics, devices, sign out everywhere
│   │       ├── notifications.tsx
│   │       └── documents.tsx        # KYC renewal
│   ├── transfer/
│   │   ├── new.tsx
│   │   ├── confirm.tsx              # biometric step-up above threshold
│   │   └── result.tsx
│   └── statements/[id].tsx          # view / share PDF
├── src/
│   ├── auth/
│   │   ├── AuthProvider.tsx         # session state machine
│   │   ├── authSession.ts           # expo-auth-session + Keycloak discovery
│   │   ├── tokenStorage.ts          # SecureStore (refresh) + memory (access)
│   │   ├── refresh.ts               # single-flight token refresh
│   │   └── appLock.ts               # biometric unlock, inactivity timeout
│   ├── api/
│   │   └── configureApi.ts          # base URL + bearer injection for packages/api-client
│   ├── features/                    # mirrors backend modules (customer-facing parts)
│   │   ├── onboarding/
│   │   ├── accounts/
│   │   ├── transfers/
│   │   ├── loans/
│   │   ├── cards/
│   │   ├── statements/
│   │   ├── profile/
│   │   └── notifications/
│   ├── components/                  # native UI kit
│   │   ├── Button.tsx, TextField.tsx, Sheet.tsx, ListItem.tsx
│   │   ├── MoneyText.tsx, MoneyInput.tsx
│   │   ├── StatusPill.tsx, Stepper.tsx
│   │   └── DocumentCapture.tsx      # camera overlay with ID frame guide
│   ├── security/
│   │   ├── screenPrivacy.ts         # expo-screen-capture helpers
│   │   └── deviceIntegrity.ts       # root/jailbreak signals (M9)
│   ├── push/
│   │   └── registerDevice.ts        # get Expo push token → POST /devices
│   └── test/
│       ├── setup.ts
│       └── msw/handlers.ts
├── e2e/                             # Maestro flows
│   ├── sign-in.yaml
│   ├── onboarding.yaml
│   ├── transfer.yaml
│   └── card-freeze.yaml
└── assets/                          # icons, splash, fonts
```

Monorepo notes: Expo supports pnpm workspaces and configures Metro for monorepos automatically in recent SDKs. If a native library has trouble with pnpm's symlinked layout, set `node-linker=hoisted` in `frontend/.npmrc`. Web-only packages (`packages/ui`, `packages/auth`) are **not** imported by mobile; ESLint blocks it.

### 31.3 Shared code across web and mobile

| Package | Web | Mobile | Notes |
|---|---|---|---|
| `packages/api-client` | ✓ | ✓ | `http.ts` is configurable: `configureHttp({ baseUrl, getAuthHeader, csrfHeader })` — web uses cookie + CSRF, mobile uses bearer token |
| `packages/money` | ✓ | ✓ | Pure TypeScript |
| `packages/config` | ✓ | ✓ | tsconfig, ESLint, Tailwind tokens |
| `packages/permissions` | ✓ | ✓ | Permission constants and helpers (moved out of `packages/auth`) |
| `packages/i18n` | ✓ | ✓ | Translation JSON + error-code messages |
| `packages/ui` | ✓ | ✗ | DOM/Radix based |
| `packages/auth` | ✓ | ✗ | BFF cookie session |

### 31.4 Mobile authentication & the Mobile Gateway

A cookie-based BFF doesn't suit native apps, so mobile uses OAuth directly with a hardened flow:

```
 Mobile app                     Keycloak                    FinCore.Bff (Mobile profile)        FinCore.Api
   │ Auth Code + PKCE (system browser) ─►│                              │                            │
   │◄── code ───────────────────────────│                              │                            │
   │── exchange code (+ verifier) ──────►│                              │                            │
   │◄── access (short, 5 min) + refresh (rotating) ──│                 │                            │
   │ refresh → SecureStore, access → memory only                       │                            │
   │ GET /api/v1/accounts  Authorization: Bearer … ────────────────────►│ validate JWT, rate-limit,  │
   │                                                                    │ device check, proxy ──────►│
```

- **OIDC client** `fincore-mobile` (Keycloak locally; a Cognito app client in the customer pool on AWS): public client, PKCE required, redirect URI `fincore://auth/callback`, refresh-token rotation on, short access-token lifetime.
- **Mobile Gateway:** the same `FinCore.Bff` project runs with a `Mobile` profile in token-passthrough mode (no cookies or CSRF). It validates the JWT, applies stricter rate limits, checks the device is registered and not revoked (`X-Device-Id`), and proxies to the API with YARP. This keeps the API itself free of channel-specific logic.
- **App lock:** after sign-in the customer enables biometrics. On cold start or after N minutes in the background, the app requires biometric/device-PIN unlock before reading the refresh token.
- **Step-up confirmation:** transfers and card payments above a configurable amount, adding a new beneficiary, and changing card controls require a fresh biometric check. The app sends an `X-Step-Up` proof (a short-lived token from a `/auth/step-up` endpoint that requires recent authentication); the API rejects sensitive operations without it.
- **Device management:** customers can see and revoke devices in *More → Security*; revocation kills that device's refresh tokens at the identity provider and its push registration.
- **Sign-out:** clears SecureStore, query cache, and push registration, and revokes the refresh token.

### 31.5 Backend additions for mobile

| Module | Addition |
|---|---|
| Identity | `CustomerDevice` aggregate (DeviceId, Platform, Model, AppVersion, PushToken, RegisteredAt, LastSeenAt, Status). Endpoints: `POST /devices`, `GET /devices`, `DELETE /devices/{id}`, `POST /auth/step-up` |
| Notifications | New **Push** channel: `IPushSender` with an Expo Push API implementation; templates gain a `Push` variant (title + body, no sensitive data); receipts job checks Expo push receipts and disables invalid tokens |
| Customers & KYC | `POST /kyc-cases/{id}/documents` accepts mobile captures (JPEG/HEIC, max size), stores capture metadata (device, timestamp); simulated liveness check on selfie behind `ILivenessCheckProvider` |
| All customer endpoints | Customer-scoped authorization: a customer token can only read/act on its own `CustomerId` (enforced by a resource-ownership policy) |
| Gateway | `Mobile` profile, `appsettings.Mobile.json`, minimum supported app version check (`426 Upgrade Required` with store link) |

### 31.6 Mobile screens

| Area | Screens |
|---|---|
| Welcome & sign-in | Welcome, sign in, biometric enrol, unlock |
| Onboarding | Product choice → personal details → ID capture (front/back with frame guide and glare check) → selfie → proof of address → review → status tracker with push updates → initial funding instructions |
| Home | Total balances, accounts carousel, upcoming loan/card dues, quick actions (transfer, pay card, repay loan) |
| Accounts | List, transaction history with infinite scroll and search, account details (copy/share account number), statements |
| Transfers | New transfer, beneficiary picker, confirmation with step-up, result with reference |
| Cards | Card visual (masked PAN), freeze/unfreeze, online/international/ATM toggles, spending limits, statements, pay (minimum / full / custom), report lost |
| Loans | Eligibility check, apply with schedule preview, application status, schedule, repay from account |
| More | Profile, security & devices, notification preferences, KYC document renewal, help, legal |

### 31.7 Mobile-specific rules

- **No money movement offline.** Cached balances may be shown offline with a clear "last updated" timestamp; all mutations require connectivity.
- **Idempotency keys** are generated when a payment screen opens and kept across retries and app restarts (persisted with the draft).
- **Sensitive screens** (card details, statements, account numbers) block screenshots and hide content in the app switcher.
- **No PII in logs, crash reports or push payloads.** Push text says "You received a payment" — details only inside the app.
- **Deep links** (`fincore://onboarding/status`, universal links for email links) always pass through the auth/unlock gate before showing data.
- **Accessibility:** Dynamic Type / font scaling, screen-reader labels on all money values (read as "one thousand two hundred fifty dollars"), minimum 44 pt touch targets.
- **Minimum supported version** enforced by the gateway; EAS Update ships JS fixes quickly, while native changes require a store release.

### 31.8 Mobile testing

| Level | Tool | What |
|---|---|---|
| Unit | Jest (`jest-expo`) | Auth state machine, refresh single-flight, money formatting, form schemas |
| Component | React Native Testing Library + MSW | Screens against mocked API, including ProblemDetails errors and 401 refresh |
| E2E | Maestro | Sign-in, onboarding with camera mocked by test images, transfer with step-up, card freeze — on Android emulator and iOS simulator |
| Manual / device | EAS preview builds via internal distribution | Real-device camera, biometrics and push |

### 31.9 Running the mobile app

```bash
cd frontend
pnpm install
pnpm --filter mobile start            # Expo dev server
# Press a (Android emulator) or i (iOS simulator, macOS only)

# Native modules like biometrics and secure storage are best tested in a development build:
pnpm --filter mobile exec eas build --profile development --platform android
```

For local development, the app points at the Mobile Gateway via `EXPO_PUBLIC_API_URL`. Android emulators reach the host machine at `10.0.2.2`; physical devices need your machine's LAN IP or a tunnel. Keycloak's redirect URI must include the Expo development scheme while developing.

### 31.10 Release pipeline (EAS)

| Profile / channel | Purpose |
|---|---|
| `development` | Dev client with debugging, points at local/dev gateway |
| `preview` | Internal distribution builds for testers, points at Staging |
| `production` | Store builds (App Store / Google Play) via EAS Submit, points at Production |

In GitHub Actions: PRs run lint → typecheck → Jest; merges to `main` publish an EAS Update to `preview`; a `mobile-vX.Y.Z` release (release-please, §33.7) runs EAS Build + Submit for `production` after approval. Store build numbers auto-increment in EAS, and `runtimeVersion` follows the app version so OTA updates never reach an incompatible binary. App signing credentials are managed by EAS, never committed.

## 32. Design System (Fintech UI)

FinCore uses one design language, **"Soft Ledger"**, across the Back Office, the web Customer Portal and the mobile app. It merges five reference projects, which were reviewed directly:

| Reference | What it actually looks like | What FinCore takes from it | Applied mostly to |
|---|---|---|---|
| **Lendora** (mortgage CRM, web + mobile) | Warm off-white canvas, soft yellow/peach/lavender gradient panels, very light large numerals with greyed decimals (`6.91`), pill tabs, numbered circular step buttons, thin orange trend line | Calm light dashboards, light-weight KPI numbers, warm gradient feature card, numbered pill steps, single warm highlight color in charts | Back Office, loan pipeline |
| **Payno** (banking + card app) | Light, airy, sky-blue backdrops; deep navy as the only strong color; navy active pill in nav; slate-blue grainy card; balance with eye toggle; circular quick actions (Send, Add Funds, Pay Bills, QR); transaction rows with avatars; striped limit progress bars; italic serif accents in headlines | Navy as a secondary brand color (card skin, charts), balance + quick actions + transaction list pattern, spending-limit bars, serif accent for marketing/onboarding headings | Mobile app, Customer Portal |
| **Orla** (AI assistant app) | Blush off-white background, pink→peach→yellow→lavender mesh-gradient orb, black floating pill tab bar with the orb in the center, chat bubbles, small pastel bar charts, soft white cards | The gradient "aura" for the assistant/insights, the floating dark pill tab bar with a center action, conversational insight bubbles | Mobile navigation, Insights |
| **ZIXO** (banking web + mobile) | Monochrome: black, white, warm greige and frosted glass; metallic silver card; black pill buttons; sidebar with a tile-grid of nav items (dark selected tile); fanned stack of cards; KPI row with sparklines and green/red deltas; thin black line charts; serif headline accent | Neutral greige surfaces, black/ink pill buttons, tile-grid module nav, fanned card stack, KPI-with-sparkline row, monochrome charts with one highlighted bar | Back Office layout, Cards screens |
| **Northpoint** (insurance app, web + mobile) | Scandinavian minimal: deep pine-teal (`#00262B`), light cyan (`#CDE3E6`) and light mint (`#ABFFAE`) on warm off-white; square corners and hairline rules; full-width rectangular buttons with a trailing arrow; light big amounts with a smaller currency suffix (`1,325 €`); coverage treemap in flat category colors; payment calendar strip of month tiles (overdue in red); "Optimization score" grade badge (B+) on a dark pine panel with a mint call to action; outlined choice tiles in onboarding; stacked budget bar with member initials | **Pine ink + mint** as the core color pair, structured hairline layouts for data, arrow row-buttons, treemap, payment calendar, health-score card, choice tiles, member-share bar (ideal for MFI groups) | Whole system: colors, Back Office data screens, mobile Home and onboarding |

**What they share (and FinCore keeps):** light, warm neutral canvases instead of stark white; one deep ink color for actions (FinCore uses Northpoint's pine); soft pastel gradients used sparingly as "moments"; pills and circles everywhere; generous rounded cards; big but *light-weight* numbers; two-tone headlines (bold phrase + light grey continuation); low-saturation charts with a single highlight.

> These boards are used for direction only. FinCore's tokens, layouts and components below are original, so nothing is copied from those projects.

### 32.1 Design principles

1. **Money first, but quiet.** Amounts are the largest elements on screen, set in a light weight with muted decimals, so they read as elegant rather than loud.
2. **Warm neutral canvas.** Backgrounds are cream/greige, surfaces are white or frosted; color is reserved for meaning.
3. **One ink, two highlights.** Deep pine ink drives actions and selection; mint marks "good / go" on dark surfaces (Northpoint); warm honey marks "the thing to look at" on light surfaces (current step, highlighted bar, key trend).
4. **Soft for customers, structured for data.** Customer-facing surfaces use soft rounded cards and pills (Payno, Orla, Lendora); dense data (tables, calendars, treemaps, statements) uses Northpoint's square corners and hairline rules so numbers line up and scan fast.
5. **Gradients are moments, not wallpaper.** The pastel "aura" appears on a few hero surfaces only: balance hero, insight/assistant, onboarding, empty states.
6. **Trust through clarity.** Every money movement has a review step (amount, from, to, fees, reference) before confirmation.
7. **State is always visible.** KYC, account, loan, card and approval states use the same pill colors and words everywhere.
8. **Light first, dark ready.** Light mode is the primary design (all five references are light-led); a graphite dark mode is provided for the mobile app and Back Office.

### 32.2 Color tokens

**Brand palette**

| Token | Hex | From | Use |
|---|---|---|---|
| `pine-950` | `#00262B` | Northpoint | **Primary ink**: primary buttons, active pills/tabs, floating tab bar, dark feature panels, headings |
| `pine-800` | `#0E3B42` | Northpoint | Hover/pressed on pine, secondary dark panels |
| `pine-600` | `#2E5E66` | — | Links, focus rings, chart series 2 |
| `cyan-100` | `#CDE3E6` | Northpoint | Selected rows, info tint, sky-like panels |
| `mint-300` | `#ABFFAE` | Northpoint | Call-to-action fill on dark panels, active tab indicator, "on track" highlights (always with pine text) |
| `mint-100` | `#E4FFE5` | — | Positive tint backgrounds ("Paid", "Active" pills) |
| `navy-800` | `#1F2A5C` | Payno | Secondary brand: "Navy Grain" card skin, chart series 3 |
| `canvas` | `#F8F6F2` | Lendora / ZIXO | App background (warm off-white) |
| `greige-100` | `#F1EEE9` | ZIXO | Raised background, sidebar, table header |
| `greige-200` | `#E6E2DC` | ZIXO | Borders, dividers |
| `stone-500` | `#8A8580` | Lendora | Muted text, decimals, secondary headline part |
| `stone-700` | `#4A4744` | — | Body text alternative on greige |
| `honey-400` | `#F4B740` | Lendora | Warm highlight: current step, highlighted chart bar, key trend line |
| `honey-100` | `#FDF1D6` | Lendora | Honey tint backgrounds (e.g., "Pending" pill) |
| `aura-peach` | `#FBD9A8` | Lendora / Orla | Gradient stop |
| `aura-blush` | `#F5C2D3` | Orla | Gradient stop |
| `aura-lilac` | `#D6CCF6` | Lendora / Orla | Gradient stop |
| `aura-sky` | `#CDE7F7` | Payno | Gradient stop |
| `cat-blue` | `#5E9FD3` | Northpoint | Category color 1 (treemaps, product mix) |
| `cat-sand` | `#C4B394` | Northpoint | Category color 2 |
| `cat-sun` | `#F7D468` | Northpoint | Category color 3 |
| `cat-leaf` | `#74C97B` | Northpoint | Category color 4 |
| `cat-lilac` | `#BDB4F2` | Orla | Category color 5 |
| `graphite-950` | `#0F0F12` | ZIXO | Dark-mode background |
| `graphite-900` | `#18181D` | ZIXO | Dark-mode surface |
| `graphite-800` | `#24242B` | ZIXO | Dark-mode raised surface / borders |

**Semantic tokens** (components only use these)

| Token | Light | Dark |
|---|---|---|
| `bg` | `canvas` | `graphite-950` |
| `surface` | `#FFFFFF` | `graphite-900` |
| `surface-sunken` | `greige-100` | `#131317` |
| `surface-glass` | `rgba(255,255,255,0.65)` + `backdrop-blur(16px)` | `rgba(36,36,43,0.6)` + blur |
| `border` | `greige-200` | `graphite-800` |
| `text` | `pine-950` | `#F4F3F0` |
| `text-muted` | `stone-500` | `#9C988F` |
| `primary` (fill) | `pine-950`, text white | `mint-300`, text `pine-950` |
| `primary-on-dark` | `mint-300`, text `pine-950` | `mint-300`, text `pine-950` |
| `panel-dark` | `pine-950` (feature panels such as the health score) | `pine-800` |
| `brand` | `pine-600` | `#7FB8BF` |
| `highlight` | `honey-400` | `honey-400` |
| `positive` | `#1F8F4E` (text) / `mint-100` (tint) | `mint-300` |
| `negative` | `#D64545` | `#F87171` |
| `warning` | `#C98A12` | `#FBBF24` |
| `info` | `#2F6FB5` (text) / `cyan-100` (tint) | `#7DB8F0` |

**Gradients**
- `aura`: mesh/radial blend of `aura-peach`, `aura-blush`, `aura-lilac`, `aura-sky` with a soft grain overlay (2–3% noise). Used for the balance hero, Insights/assistant, onboarding backgrounds and the Back Office "feature" card.
- `aura-warm` (Lendora-like): `#FFF6E3 → #FBD9A8 → #E8E4F8` for Back Office promo/insight panels.
- **Card skins** (virtual cards): *Pine* (`pine-950` with a mint stripe, Northpoint-style), *Navy Grain* (`navy-800` with grain and soft light sweep), *Silver* (metallic greige-to-white with fine line texture), *Aura* (pastel gradient). Each card product chooses one.

**Contrast rules:** all text meets WCAG AA (4.5:1 for body, 3:1 for large numbers). `honey-400` and `mint-300` are never used for text on light backgrounds; they fill shapes and lines only, with `pine-950` text on top. Muted decimals use `stone-500` at large sizes only (≥ 24 px). Positive/negative amounts always include `+`/`−` and, in tables, an arrow, never color alone.

### 32.3 Typography

| Role | Font | Weight | Size / line height (web) | Notes |
|---|---|---|---|---|
| Display numbers (balance, KPIs) | **Urbanist** | 300 (Light) | 48/56 web, 40/48 mobile | Lendora/ZIXO-style light numerals; decimals 60% size in `text-muted` |
| Display headline | Urbanist | 600 + 300 | 40/48 | Two-tone: bold first phrase in `text`, light continuation in `text-muted` |
| Editorial accent (optional) | **Instrument Serif** Italic | 400 | inherit | One or two words in onboarding/marketing headings only ("Manage your *cards*") |
| H1 | Urbanist | 600 | 28/36 | Page titles |
| H2 | Urbanist | 600 | 22/30 | Section titles |
| H3 | Inter | 600 | 17/24 | Card titles |
| Body | Inter | 400 | 15/22 | Default text |
| Body small | Inter | 400 | 13/18 | Table cells, meta |
| Label | Inter | 500 | 12/16 | Form labels, pills |
| Table numbers | Inter | 500, `tabular-nums` | 14/20 | Right-aligned |

**Money formatting:** `$56,751` + `.23` (smaller, muted) on hero numbers; full size in tables. The currency sign follows the locale; when it is a suffix (`1,325 €`, `189 €/mo`) it is set ~50% size like Northpoint, when it is a prefix it is smaller and raised (Lendora's `%` treatment). A "hide balances" eye toggle replaces digits with `••••`.

### 32.4 Shape, spacing, elevation, motion

| Token | Values |
|---|---|
| Spacing | 4-pt scale: 4, 8, 12, 16, 20, 24, 32, 40, 48, 64 |
| Radius | `xs` 4 (structured data: table cells, calendar tiles, treemap blocks, statement cards, row-buttons — Northpoint), `sm` 10 (inputs), `md` 14 (small cards, tiles), `lg` 20 (cards), `xl` 28 (hero cards, sheets, device-like panels), `pill` 999 (buttons, tabs, chips, tab bar) |
| Buttons | **Compact actions:** pill-shaped; primary = pine fill, secondary = white with `border`. **Row buttons** (forms, onboarding, panel CTAs): full-width, `xs` radius, label left + arrow `→` right (Northpoint) — pine fill on light, mint fill on dark panels. **Icon buttons:** 40 px circles |
| Elevation | `e1` 0 1 2 rgba(14,19,48,.04); `e2` 0 6 20 rgba(14,19,48,.06); `e3` 0 18 40 rgba(14,19,48,.10) — always soft and wide, never hard |
| Glass | `surface-glass` with 16 px blur over gradients/cards (ZIXO dashboard, Payno floating cards); fallback to solid `surface` where blur is unsupported or reduce-transparency is on |
| Texture | 2–3% grain on gradients and card skins |
| Motion | 160 ms (press/hover), 240 ms (sheets, tabs), 360 ms (hero, card fan); ease `cubic-bezier(.2,.8,.2,1)`; numbers count up on first load; respect reduce-motion |
| Icons | Lucide, 1.5 px stroke, 20/24 px, inside circles for quick actions |

### 32.5 Core components

| Component | Description | Inspired by | Web | Mobile |
|---|---|---|---|---|
| `BalanceHero` | Light large balance with muted decimals, eye toggle, available-limit line, `aura` background | Payno, Lendora | Portal | ✓ |
| `QuickActions` | Row of 48 px circular icon buttons with labels: Send, Add funds, Pay bills, Pay card, QR | Payno | Portal | ✓ |
| `CardStack` | Fanned stack of virtual cards (Navy Grain / Silver / Aura), tap to bring forward, frozen overlay | ZIXO, Payno | Portal, Back Office | ✓ (swipe) |
| `KpiTile` | Label, light big number, sparkline, delta chip (`▲ 5.2%` green / `▼` red) | ZIXO | Back Office | — |
| `TrendCard` | Light numbers row (e.g., rates, PAR30, NPL) with small arrows and one honey trend line | Lendora | Back Office | — |
| `TileNav` | Sidebar grid of square module tiles (icon + label), dark ink tile for the active one, list of shortcut actions below | ZIXO | Back Office | — |
| `PillTabs` | Segmented pill navigation with a filled active pill | Payno, Lendora | ✓ | ✓ |
| `StepDots` | Numbered circular steps (1 · 2 · 3), active step filled with honey | Lendora | Wizards | Onboarding |
| `TransactionItem` | Avatar or merchant logo, name, time, amount right-aligned (`+$80.89` in positive) | Payno | ✓ | ✓ |
| `LimitBar` | Spending/credit limit with striped fill, `used / total` and percent | Payno | Cards | Cards |
| `StatusPill` | Soft tinted pill: Active (mint tint), Pending (honey tint), Overdue (red tint), Draft (greige), Frozen (cyan tint) | Lendora | ✓ | ✓ |
| `FloatingTabBar` | Pine pill tab bar floating above content, mint dot under the active tab, center aura orb (Transfer / Assistant) | Orla + Northpoint | — | ✓ |
| `InsightBubble` | Chat-style bubble with small aura orb avatar and a short explainable insight + one action | Orla | Portal, Back Office | ✓ |
| `FeatureCard` | Warm gradient card with two-tone headline and a circular arrow button | Lendora | Back Office dashboard | Portal |
| `DataTable` | Square corners (`xs`), no zebra stripes, hairline dividers, sticky header on `greige-100`, right-aligned tabular amounts, row actions on hover, density toggle | Lendora, ZIXO, Northpoint | Back Office | — |
| `PipelineBoard` | Columns of application cards (Prospective → In processing → Expires soon → Approved) | Lendora | Back Office | — |
| `AmountKeypad` | Big centered amount, custom keypad, available balance under it | Payno | — | ✓ |
| `HealthScoreCard` | Dark pine panel: grade badge in a circle (A–E, e.g. `B+`), one-line "You're close to A− — 1 thing left", what's missing, mint row-button. Customer version shows a **financial health** score from their own behavior (on-time payments, savings habit, card utilization); staff version shows the loan scorecard grade from §23.8 with its reasons | Northpoint | Back Office (assessment), Portal | ✓ |
| `PaymentCalendar` | Horizontal strip of month/day tiles for upcoming installments and card due dates: paid = pine fill, upcoming = outline, overdue = red fill with alert icon, count of payments on the right | Northpoint | Portal, Back Office (loan detail) | ✓ |
| `MixTreemap` | Flat-color treemap of shares with icon + label + percent: customer "where my money goes" / debt mix; staff loan portfolio by product or sector | Northpoint | ✓ | ✓ |
| `AmountBreakdown` | Square-cornered card: greige header strip with reference (`Loan #48372`), main amount in light display type, hairline sub-rows (principal, interest, fees) | Northpoint | ✓ | ✓ |
| `ChoiceTiles` | 2×2 outlined square tiles with icon and label for onboarding questions (account purpose, income range, how confident are you with credit) | Northpoint | Portal onboarding | Onboarding |
| `MemberShareBar` | Stacked horizontal bar split by member with initials (K.C., T.C., …) and total underneath; used for MFI group loan allocation and repayment share | Northpoint | Back Office (Microfinance) | ✓ (group members) |
| `ReviewSheet` | Confirmation summary; dialog on web, bottom sheet on mobile | — | ✓ | ✓ |
| `EmptyState` / `Skeleton` | Small aura orb illustration + message + action; skeletons match final layout | Orla | ✓ | ✓ |

### 32.6 Layouts

**Back Office (ZIXO layout + Lendora calm)**
- Light `canvas` background; data areas (tables, schedules, statements, calendars) follow Northpoint's structured style with square corners and hairlines, while dashboard cards stay soft. Left sidebar on `greige-100` with the FinCore logo, search field, **TileNav** (active tile in pine) (Home, Customers, Accounts, Loans, Microfinance, Cards, Ledger, Approvals, Reports, Admin) and a short list of shortcut actions (Open account, New loan application, Freeze card, Run EOD).
- Top bar: greeting and business date ("Good morning, Amina · Business date 14 Oct 2026"), pill buttons for the two most common actions, approvals bell, branch switcher, avatar.
- **Dashboard:** KPI row of 4 `KpiTile`s (Deposits, Loan portfolio, PAR30, Disbursed today) → `TrendCard` + monochrome charts with one honey highlight → `FeatureCard` (insight) on the right → "Needs attention" list (approvals, KYC reviews, overdue loans).
- **Loan assessment:** `HealthScoreCard` (staff variant) beside the scorecard reasons; loan detail shows `AmountBreakdown` and a `PaymentCalendar` of installments.
- **Microfinance group page:** `MemberShareBar` for the current cycle, then members table and collection history.
- **Portfolio:** `MixTreemap` by product/sector next to aging buckets.
- **Detail pages:** header with name, `StatusPill`, primary pill actions; `PillTabs` (Overview, Transactions, Schedule, Documents, Activity); right rail of summary facts.
- **Applications:** list ⇄ `PipelineBoard` toggle.
- Grid: 12 columns, max content width 1440 px, 24 px gutters; cards on white with `e1`.

**Customer Portal (web)**
- Top pill navigation (Home, Accounts, Cards, Loans, Payments) with a filled pine active pill, `canvas` background.
- Home: `BalanceHero` (aura) + `QuickActions`, then `CardStack`, `LimitBar`s, `PaymentCalendar` of upcoming dues, `HealthScoreCard`, `InsightBubble`, recent transactions.

**Mobile app (Payno content + Orla navigation + ZIXO cards)**
- Light-first on `canvas`, graphite dark mode available.
- **Home:** avatar + greeting + bell → `BalanceHero` with eye toggle → `QuickActions` → `CardStack` → `PaymentCalendar` (next dues) → `HealthScoreCard` (pine panel, mint button) → `InsightBubble` → recent transactions.
- **Loans tab/detail:** `AmountBreakdown`, `PaymentCalendar`, `MixTreemap` of total debt by product.
- **Navigation:** `FloatingTabBar` (pine pill, mint active dot) with Home, Accounts, center aura orb (Transfer; long-press opens Insights), Cards, More. Loans are reached from Home and More.
- **Money flows:** full-screen `AmountKeypad` → recipient → `ReviewSheet` → biometric step-up → success screen with a soft aura pulse and the reference number.
- **Onboarding:** welcome screen on a pine panel ("A single place for your *money*" with the key word in mint) and full-width arrow row-buttons (Create account →, Sign in →); then one question per screen on `canvas` or `aura`, `ChoiceTiles` for multiple-choice questions, `StepDots` at the top.

### 32.7 Data visualization

- Mostly monochrome (ZIXO): series in `pine-950`, `pine-600`, `stone-500`, `greige-200`; **one** series or bar highlighted in `honey-400` (Lendora's orange line).
- Thin 1.5 px lines; area fills fade from 12% to 0%; hairline horizontal gridlines only; rounded bar tops; selected point shows a dark pill tooltip (`+8.2%`).
- **Treemaps** in the categorical palette (`cat-*`) for mix/composition (Northpoint); **calendar strips** for schedules.
- Half-donut gauges for spending breakdown; mini pastel bars (Orla) for small streak/goal widgets.
- Positive/negative paired with sign and arrow, not color alone.
- Web: Recharts. Mobile: `victory-native` (Skia) or `react-native-gifted-charts`.

### 32.8 Insights / assistant surface (optional, Orla-inspired)

An optional **FinCore Insights** experience uses the aura orb as its identity: `InsightBubble`s on Home and a full-screen conversational view from the tab-bar orb, and the `HealthScoreCard` as its "next best step" surface. It starts rule-based (spending trends, upcoming dues, "eligible for a loan up to X" from the real scorecard, savings streaks). An LLM-powered assistant can be added later behind an `IInsightsAssistant` interface with strict rules: read-only, customer-scoped data only, no investment advice, and every number traceable to an API value.

### 32.9 Implementation: one token source for web and mobile

```
frontend/packages/design-tokens/
├── src/
│   ├── colors.ts          # palette + semantic light/dark maps (§32.2)
│   ├── gradients.ts       # aura, aura-warm, card skins (CSS strings + RN gradient stops)
│   ├── typography.ts      # Urbanist / Inter / Instrument Serif scale (§32.3)
│   ├── spacing.ts / radius.ts / shadows.ts / motion.ts
│   └── index.ts
├── assets/
│   └── grain.png          # tiny tileable noise texture
├── build/
│   ├── tailwind-theme.css # generated Tailwind v4 @theme variables (web)
│   └── nativewind.js      # generated theme for NativeWind (mobile)
└── scripts/build-tokens.ts
```

- **Web:** Tailwind v4 `@theme` variables with `[data-theme="dark"]` overrides; shadcn/ui components restyled to pills, soft shadows and the tokens in `packages/ui`; fonts via `@fontsource/urbanist`, `@fontsource/inter`, `@fontsource/instrument-serif` (self-hosted, so the CSP stays strict).
- **Mobile:** NativeWind theme from the same tokens; fonts via `@expo-google-fonts/urbanist`, `@expo-google-fonts/inter`, `@expo-google-fonts/instrument-serif`; gradients with `expo-linear-gradient` (mesh look via layered radial gradients in `react-native-svg` or Skia); blur with `expo-blur`; motion with Reanimated; haptics (`expo-haptics`) on confirm/success.
- **Showcases:** Storybook (web) and a hidden "Design Gallery" screen (mobile dev builds) render every component in light and dark.
- **Figma (optional):** mirror the tokens as Figma variables so future designs and code stay in sync.

## 33. DevOps & AWS Deployment

### 33.1 General DevOps

- **Local:** .NET Aspire AppHost (preferred) or `deploy/docker-compose.yml`.
- **CI (GitHub Actions):** restore → build (`-warnaserror`) → unit tests → architecture tests → integration tests (Testcontainers on the runner) → coverage report → vulnerable-package check.
- **Front-end CI:** `pnpm install --frozen-lockfile` → lint → typecheck → Vitest → OpenAPI client drift check → build → Playwright against a compose stack.
- **Mobile CI/CD:** `ci.yml` runs Jest and typecheck on PRs; `cd-staging.yml` publishes EAS Updates to `preview` from `main`; a `mobile-vX.Y.Z` release runs EAS Build + Submit (§31.10, §33.8).
- **Environments:** Development (local), Staging (AWS), Production (AWS) with `appsettings.{Env}.json` + environment variables from SSM Parameter Store.
- **Branching & versions:** trunk-based, short-lived feature branches, Conventional Commits; merges to `main` deploy to Staging; release-please turns commits into versions and GitHub Releases that deploy to Production after approval (§33.7).

### 33.2 Recommended AWS architecture (lean)

The goal is the **smallest always-on footprint** that is still a proper, secure, container-based deployment you can push to from GitHub. The modular monolith helps a lot here: the whole backend is one container.

```
                         Route 53 (fincore.example.com)
                                    │
             ┌──────────────────────┴────────────────────────┐
             ▼                                               ▼
   CloudFront (admin.*, app.*)                      api.* (mobile)
   ├─ /*        → S3 (React builds, OAC, private)          │
   ├─ /bff/*    → ALB ──┐                                  │
   └─ /api/*    → ALB ──┤                                  │
                        ▼                                  ▼
            ┌──────────────── Application Load Balancer (HTTPS, ACM) ───────────────┐
            │  host/path rules                                                       │
            ▼                                                                        ▼
   ECS Fargate (ARM64 / Graviton)                                     ECS Fargate (ARM64)
   service: gateway (FinCore.Bff)  ───── internal (Service Connect) ─► service: api (FinCore.Api)
   BackOffice / Portal / Mobile modes                                 modules + Quartz jobs + outbox
   by host header                                                                    │
                                                                                     ▼
                      ┌──────────────────────────────┬──────────────────────┬────────────────┐
                      ▼                              ▼                      ▼                ▼
          RDS PostgreSQL (t4g, private)     S3: KYC documents &      Amazon Cognito     Amazon SES
          one DB, schema per module         statements (private,     (staff pool +      (email)
                                            SSE, presigned URLs)     customer pool)
   Observability: CloudWatch Logs (14–30 day retention), Container Insights (optional), alarms, AWS Budgets
   Secrets/config: SSM Parameter Store (SecureString); RDS-managed master secret
```

### 33.3 Service choices and why

| Need | AWS choice | Why this, for low resources | Instead of |
|---|---|---|---|
| Backend container | **ECS on Fargate, ARM64**, 1 task: 0.5 vCPU / 1 GB | No servers to patch; Graviton is ~20% cheaper; a modular monolith fits in one small task | EKS (control plane cost + complexity); Lambda (EOD, Quartz and outbox are long-running) |
| Gateway / BFF | **One** Fargate task (0.25 vCPU / 0.5 GB) running all three modes, selected by host header | One container instead of three | Separate BFF per app |
| Web apps | **S3 + CloudFront** (Origin Access Control) | Static hosting costs cents; CloudFront also routes `/api` and `/bff` to the ALB so cookies stay same-origin | Serving SPAs from the BFF container |
| Load balancer | **One ALB** shared by both services | Needed for HTTPS + health checks; one is enough | API Gateway + VPC Link |
| Database | **RDS for PostgreSQL**, `db.t4g.micro` (staging) / `db.t4g.small` (prod), gp3 20 GB, single-AZ to start | Cheapest managed Postgres; Multi-AZ can be switched on later | Aurora Serverless v2 (higher minimum cost) |
| Cache | **None at first**: `HybridCache` in-memory only; idempotency keys and Data Protection keys stored in Postgres | Saves a whole managed service | ElastiCache (add later if you scale to many tasks) |
| Identity | **Amazon Cognito** (staff user pool + customer user pool) | Managed, generous free tier, no container to run | Keycloak on Fargate (another always-on task) |
| Messaging | **None** (in-memory bus + outbox) until you extract services; then **SNS + SQS** | Zero cost now; SNS/SQS is pay-per-use later | Amazon MQ / self-hosted RabbitMQ |
| Files | **S3** (versioning, SSE, lifecycle to Glacier for old statements) | Cheap, durable | EFS |
| Email / SMS | **SES** for email; SMS via SNS (or stubbed) | Pay-per-use | Third-party SaaS |
| Logs & metrics | **CloudWatch Logs** (stdout JSON from Serilog), alarms, optional ADOT sidecar in prod for traces → X-Ray | No log server to run | Seq / Grafana stack on AWS |
| Secrets & config | **SSM Parameter Store** SecureString | Standard parameters are free | Secrets Manager for everything |
| Container registry | **ECR** with lifecycle policy (keep last 10 images) | Pay only for storage | Docker Hub |
| CI → AWS auth | **GitHub OIDC** → IAM role | No long-lived AWS keys in GitHub | Access keys in secrets |

**Networking choice for cost:** a NAT Gateway costs more per month than the whole compute layer here. FinCore therefore runs Fargate tasks in **public subnets with public IPs**, but their security groups accept traffic **only from the ALB** (and the gateway → API on the API port). RDS lives in **private subnets** with no internet route and accepts traffic only from the API's security group. If you later need private tasks, add NAT (or a NAT instance) and flip a Terraform variable — the design supports both.

### 33.4 Rough monthly cost (one environment, low traffic)

| Item | Approx. USD / month |
|---|---|
| Fargate ARM64: api (0.5 vCPU / 1 GB) | 14–16 |
| Fargate ARM64: gateway (0.25 vCPU / 0.5 GB) | 7–8 |
| Application Load Balancer | 18–22 |
| RDS PostgreSQL `db.t4g.micro` + 20 GB gp3 + backups | 14–18 |
| Public IPv4 addresses (ALB + tasks) | 10–15 |
| S3 + CloudFront + Route 53 + ECR | 2–5 |
| CloudWatch Logs & alarms | 3–8 |
| Cognito / SES / SSM | ~0 at low volume (within free tiers) |
| **Total** | **~ 70–95** |

These are estimates; prices vary by region and change over time, so check the **AWS Pricing Calculator** before committing. Ways to cut further:
- Run **Staging only during working hours** (scheduled scaling of ECS services to 0 and stopping RDS overnight via EventBridge Scheduler).
- Use **Fargate Spot** for Staging.
- Start with **Production only** and use local Aspire as your staging.
- Set an **AWS Budget** with email alerts at 50/80/100% (included in Terraform).

### 33.5 Runtime adjustments for AWS

| Area | Local | AWS |
|---|---|---|
| Identity provider | Keycloak (container) | Cognito. The code is IdP-agnostic: `Auth:Authority`, `Auth:ClientId`, `Auth:Audience` come from config, and roles/permissions are loaded from the Identity module's database (claims transformation), not from IdP groups. The IdP only proves *who* the user is (`sub`, email). |
| Cache | Redis | In-memory `HybridCache`; idempotency store in Postgres (`IIdempotencyStore` has Redis and Postgres implementations) |
| ASP.NET Data Protection keys | file system | Postgres (`PersistKeysToDbContext`), so cookies survive restarts and work across tasks |
| Document storage | local folder / MinIO | S3 via `IDocumentStorage` (presigned upload/download URLs, max size and content-type enforced) |
| Email | MailPit | SES via `IEmailSender` |
| Logs | Seq | stdout JSON → CloudWatch Logs |
| Background jobs | Quartz in API process | Same, with Quartz **clustered** Postgres job store so scaling to 2 tasks never runs EOD twice |
| Migrations | applied at startup (dev only) | EF Core **migration bundles** run as a one-off ECS task before each deployment |
| SPA hosting | Vite dev server | S3 + CloudFront; gateway no longer serves `wwwroot` on AWS |

Memory tuning for small tasks: keep the default .NET 10 GC settings (DATAS adapts the heap to the container) and set `DOTNET_GCHeapHardLimitPercent=75` as a safety margin; watch memory in CloudWatch for the first weeks and only then resize.

### 33.6 Terraform layout

```
infra/
├── bootstrap/                      # run ONCE manually with your admin credentials
│   ├── main.tf                     # S3 state bucket (versioned, encrypted)
│   ├── github-oidc.tf              # GitHub OIDC provider + deploy roles per environment
│   └── outputs.tf                  # role ARNs to put in GitHub environment variables
├── modules/
│   ├── network/                    # VPC, 2 AZs, public + private subnets, security groups
│   ├── ecr/                        # repos: fincore-api, fincore-gateway, fincore-migrator
│   ├── rds-postgres/               # instance, subnet group, parameter group, managed master secret
│   ├── ecs-cluster/                # cluster, Service Connect namespace, Container Insights flag
│   ├── ecs-service/                # task def, service, autoscaling, log group, IAM task/exec roles
│   ├── alb/                        # ALB, HTTPS listener, host/path rules, target groups
│   ├── static-site/                # S3 bucket + CloudFront + OAC + /api,/bff behaviors
│   ├── cognito/                    # staff & customer pools, app clients (backoffice, portal, mobile)
│   ├── storage/                    # documents bucket + versioned artifacts bucket (web builds for rollback)
│   ├── ses/                        # domain identity, DKIM
│   ├── dns-certs/                  # Route 53 records, ACM certs (CloudFront cert in us-east-1)
│   ├── params/                     # SSM parameters (non-secret config + SecureStrings)
│   ├── scheduler/                  # optional: scale staging to 0 out of hours, stop/start RDS
│   ├── observability/              # alarms (5xx, CPU/mem, RDS storage/CPU), SNS alert topic, AWS Budget
│   └── waf/                        # optional: AWS WAF managed rules + rate limiting (production)
├── envs/
│   ├── staging/
│   │   ├── backend.tf              # S3 backend, key = staging/terraform.tfstate
│   │   ├── main.tf                 # composes the modules
│   │   ├── variables.tf
│   │   └── staging.tfvars          # sizes, domain names, flags (spot, schedule, multi_az)
│   └── production/
│       ├── backend.tf
│       ├── main.tf
│       ├── variables.tf
│       └── production.tfvars
├── .tflint.hcl
└── README.md
```

**State backend** (Terraform 1.10+ supports S3-native locking, so no DynamoDB table is needed):

```hcl
terraform {
  required_version = ">= 1.10"
  backend "s3" {
    bucket       = "fincore-tfstate-<account-id>"
    key          = "staging/terraform.tfstate"
    region       = "eu-west-1"
    encrypt      = true
    use_lockfile = true
  }
  required_providers {
    aws = { source = "hashicorp/aws", version = "~> 6.0" }
  }
}
```

**GitHub OIDC trust** (in `bootstrap/github-oidc.tf`), so only your repo's chosen environment can assume the role:

```hcl
data "aws_iam_policy_document" "github_trust" {
  statement {
    actions = ["sts:AssumeRoleWithWebIdentity"]
    principals {
      type        = "Federated"
      identifiers = [aws_iam_openid_connect_provider.github.arn]
    }
    condition {
      test     = "StringEquals"
      variable = "token.actions.githubusercontent.com:aud"
      values   = ["sts.amazonaws.com"]
    }
    condition {
      test     = "StringLike"
      variable = "token.actions.githubusercontent.com:sub"
      values   = ["repo:<your-github-user>/FinCore:environment:staging"]
    }
  }
}
```

**Who owns the task definition:** Terraform creates the ECS service and an initial task definition, then ignores later revisions so the deployment pipeline can roll out new images without Terraform fighting it:

```hcl
resource "aws_ecs_service" "api" {
  # ...
  lifecycle { ignore_changes = [task_definition, desired_count] }
  deployment_circuit_breaker {
    enable   = true
    rollback = true
  }
}
```

Fargate ARM64 is set in the task definition:

```hcl
runtime_platform {
  cpu_architecture        = "ARM64"
  operating_system_family = "LINUX"
}
```

### 33.7 Versioning & releases

Every deployable part has a visible version, every release is a GitHub Release, and you can always see which version runs where.

#### 33.7.1 What gets a version

FinCore has **two release trains**, because the backend and web apps are deployed together while the mobile app goes through app stores at its own pace:

| Release train | Covers | Version source | Git tag | Example |
|---|---|---|---|---|
| **Platform** | .NET API (all modules), BFF/gateway, migrator, Back Office, Customer Portal, infrastructure | `version.txt` at repo root, mirrored into `Directory.Build.props` and the web `package.json` files | `platform-vX.Y.Z` | `platform-v1.4.0` |
| **Mobile** | Expo app (iOS + Android) | `frontend/apps/mobile/package.json` → `app.config.ts` | `mobile-vX.Y.Z` | `mobile-v1.2.0` |

Versions follow **Semantic Versioning** and are derived automatically from the Conventional Commits in §35.7:

| Commit | Bump | Example |
|---|---|---|
| `fix(...)` | Patch | 1.4.0 → 1.4.1 |
| `feat(...)` | Minor | 1.4.1 → 1.5.0 |
| `feat(...)!` or a `BREAKING CHANGE:` footer | Major | 1.5.0 → 2.0.0 |
| `docs`, `test`, `chore`, `ci`, `refactor`, `style` | No release on their own | — |

Before 1.0.0, breaking changes bump the minor version (`bump-minor-pre-major`), so the project can move fast during M0–M8 and reach **1.0.0 at M9**.

**Three different "versions" — don't mix them up:**

| Kind | Example | Changes when |
|---|---|---|
| Product version | `1.4.0` | Every release (from commits) |
| API contract version | `/api/v1` | Only on a breaking API change (then `/api/v2` runs alongside v1 for a deprecation period) |
| Database schema version | Latest EF migration per module, e.g. `loans: 20261102_AddRestructuring` | Every migration |

#### 33.7.2 Automated releases with release-please

[release-please](https://github.com/googleapis/release-please-action) (free GitHub Action from Google) runs on every push to `main`:

1. It reads the new Conventional Commits and keeps a **Release PR** open per train ("chore(release): platform 1.5.0"), containing the version bump and the generated `CHANGELOG.md` section.
2. The owner reviews and merges the Release PR when ready to release. **Nothing is released without that merge.**
3. On merge, release-please creates the Git tag and a **GitHub Release** with readable notes (Features, Bug Fixes, Infrastructure), and the same workflow run triggers the production deployment (§33.8).

Configuration files at the repo root:

```json
// release-please-config.json
{
  "bump-minor-pre-major": true,
  "include-component-in-tag": true,
  "changelog-sections": [
    { "type": "feat",  "section": "Features" },
    { "type": "fix",   "section": "Bug Fixes" },
    { "type": "perf",  "section": "Performance" },
    { "type": "infra", "section": "Infrastructure" },
    { "type": "refactor", "section": "Internal", "hidden": true }
  ],
  "packages": {
    ".": {
      "component": "platform",
      "release-type": "simple",
      "changelog-path": "CHANGELOG.md",
      "exclude-paths": ["frontend/apps/mobile"],
      "extra-files": [
        { "type": "xml",  "path": "Directory.Build.props", "xpath": "//Project/PropertyGroup/Version" },
        { "type": "json", "path": "frontend/apps/backoffice/package.json", "jsonpath": "$.version" },
        { "type": "json", "path": "frontend/apps/portal/package.json", "jsonpath": "$.version" }
      ]
    },
    "frontend/apps/mobile": {
      "component": "mobile",
      "release-type": "node",
      "changelog-path": "CHANGELOG.md"
    }
  }
}
```

```json
// .release-please-manifest.json  (current versions; updated by release-please)
{ ".": "0.1.0", "frontend/apps/mobile": "0.1.0" }
```

> The release commits and Release PRs are authored by the `github-actions` bot, which keeps release automation clearly separate from code changes. If you prefer them under your own name, give the release workflow a fine-grained personal access token (`RELEASE_TOKEN` secret, contents + pull-requests write) instead of `GITHUB_TOKEN`. Check the release-please docs for the exact option names of the version you pin.

#### 33.7.3 Where each version is visible

**Backend (.NET)**
- `Directory.Build.props` holds `<Version>`; CI sets `SourceRevisionId` to the Git SHA, so `AssemblyInformationalVersion` becomes `1.4.0+a1b2c3d`.
- `GET /api/version` (anonymous, cached) returns:
  ```json
  {
    "product": "1.4.0",
    "commit": "a1b2c3d",
    "builtAtUtc": "2026-11-02T09:14:00Z",
    "environment": "staging",
    "apiContract": "v1",
    "minimumMobileVersion": "1.2.0",
    "modules": [
      { "name": "Loans", "enabled": true, "schema": "20261102_AddRestructuring" },
      { "name": "Cards", "enabled": false, "schema": "20261020_Initial" }
    ]
  }
  ```
- Every response carries `X-FinCore-Version: 1.4.0`; the OpenAPI `info.version` is the product version; OpenTelemetry sets `service.version`, so logs and traces are filterable by version.
- Container images are tagged `1.4.0`, `1.4.0-a1b2c3d` and the full Git SHA in ECR.

**Web apps (Back Office, Portal)**
- Vite injects `__APP_VERSION__`, `__GIT_SHA__` and `__BUILD_TIME__` at build time.
- The footer shows `v1.4.0 · a1b2c3d · Staging`; every API call sends `X-Client-Version: backoffice/1.4.0`.
- Each build also publishes `/version.json`; the app polls it every few minutes and shows a "New version available — refresh" banner after a deployment.
- **Back Office → Admin → System versions** page lists, side by side: Back Office, Portal, API, gateway, each module's schema version and on/off state, minimum mobile version, and the business date. This is the one place to answer "what's running?".

**Mobile app (Expo)**
- `version` (store-facing, e.g. `1.2.0`) comes from `package.json`; build numbers (`ios.buildNumber`, `android.versionCode`) are auto-incremented by EAS (`"appVersionSource": "remote"`, `"autoIncrement": true` in `eas.json`).
- `runtimeVersion` uses the `appVersion` policy, so an over-the-air update only reaches installs with the same native version; native changes require a new store build.
- **More → About** shows `Version 1.2.0 (build 45) · Update 7f3c21 · Channel production`.
- Each request sends `X-App-Version: mobile/1.2.0 (45)`; the Mobile Gateway answers `426 Upgrade Required` below `minimumMobileVersion` (an SSM parameter), and the app shows an update screen linking to the store.

**GitHub**
- Tags and **GitHub Releases** for both trains, with generated notes.
- **Environments** (`staging`, `production`) show the deployment history: which commit and version went where, when, and who approved it.
- README badges show the latest platform and mobile release.
- Branch protection on `main`: PR required, CI green, linear history; only release-please creates `platform-v*` / `mobile-v*` tags.

#### 33.7.4 Module switches (enable modules per environment)

All backend modules ship inside the one API image, so "deploying a module" means **turning it on** in an environment. Each `IModule` reads `Modules:<Name>:Enabled` (SSM parameter `/fincore/<env>/Modules/<Name>/Enabled`):

- Disabled modules don't map their endpoints, don't run their jobs, and don't subscribe to events; their migrations still run so the schema is ready.
- Example: Cards enabled in Staging during M7 but disabled in Production until it passes acceptance.
- The web apps and mobile app read enabled modules from `/bff/user` (and `/api/version`) and hide menus and tabs for disabled modules.
- Toggling is a Terraform variable change (`enabled_modules = ["Organization","Identity","Customers","Ledger","Accounts","Loans"]`), applied through the pipeline, so every change is reviewed and recorded.

### 33.8 CI/CD pipeline (GitHub Actions → AWS)

One pipeline deploys **the full project** or **only the parts that changed**, to Staging automatically and to Production on release.

#### 33.8.1 Workflow files

```
.github/workflows/
├── ci.yml                    # every PR: build, test, lint, commit-message check (path-filtered)
├── release-please.yml        # push to main: maintain Release PRs; on release → call cd-production
├── cd-staging.yml            # push to main: detect changes → deploy changed components to Staging
├── cd-production.yml         # called on platform release (or manually): full deploy to Production
├── deploy-manual.yml         # workflow_dispatch: pick environment, components and version (redeploy/rollback)
├── mobile-release.yml        # called on mobile release: EAS Build + Submit (production)
├── _infra.yml                # reusable: terraform plan/apply for an environment
├── _build-backend.yml        # reusable: test + build api/gateway/migrator images (ARM64) → ECR
├── _deploy-backend.yml       # reusable: run migrator task → deploy api → deploy gateway
├── _build-web.yml            # reusable: build backoffice/portal for an environment → versioned artifact in S3
├── _deploy-web.yml           # reusable: sync artifact to the site bucket → CloudFront invalidation
├── _mobile-update.yml        # reusable: EAS Update to a channel
└── _smoke-test.yml           # reusable: /health/ready, /api/version matches, web pages load
```

Workflows starting with `_` are **reusable** (`on: workflow_call`), so staging, production and manual deploys run exactly the same steps.

#### 33.8.2 Components and when each deploys

| Component | Path filter | Built into | Deployed to |
|---|---|---|---|
| `infra` | `infra/**` | Terraform plan | AWS resources (§33.9) |
| `backend` | `src/**`, `Directory.*.props`, `global.json` | ECR images `fincore-api`, `fincore-gateway`, `fincore-migrator` | ECS services `api`, `gateway`; migrator one-off task |
| `web-backoffice` | `frontend/apps/backoffice/**`, `frontend/packages/**` | `backoffice-<version>-<sha>.zip` in the artifacts bucket | S3 site bucket + CloudFront (admin.*) |
| `web-portal` | `frontend/apps/portal/**`, `frontend/packages/**` | `portal-<version>-<sha>.zip` | S3 site bucket + CloudFront (app.*) |
| `mobile-ota` | `frontend/apps/mobile/**` (JS only), `frontend/packages/**` | EAS Update bundle | EAS channel `preview` (staging) / `production` |
| `mobile-store` | mobile release only | EAS Build | TestFlight / Google Play internal track via EAS Submit |

Change detection uses `dorny/paths-filter`. A change to `frontend/packages/api-client` triggers both web apps and mobile OTA; a change to `infra/` runs Terraform before any app deploy.

#### 33.8.3 Pipeline flows

**Pull request (`ci.yml`)**
```
detect changes ─┬─ backend:  restore → build -warnaserror → unit + architecture tests → integration tests (Testcontainers)
                ├─ web:      pnpm install → lint → typecheck → Vitest → api-client drift check → build
                ├─ mobile:   lint → typecheck → Jest → expo-doctor
                ├─ infra:    fmt → validate → tflint → plan (staging) posted as PR comment
                └─ always:   commit-message check (Conventional Commits, no AI attribution)
```

**Merge to `main` (`cd-staging.yml`, environment `staging`, automatic)**
```
detect changes
  └─► infra (if changed): terraform apply staging
        └─► backend (if changed): build images (version+sha) → push ECR
              └─► migrate: run fincore-migrator task, wait, fail on error
                    └─► deploy api → deploy gateway (circuit breaker rollback)
        └─► web (if changed): build per app with staging config → store artifact → sync → invalidate
        └─► mobile-ota (if changed): eas update --channel preview
  └─► smoke tests: /health/ready, /api/version.commit == this commit, Back Office + Portal load
  └─► summary: GitHub job summary lists what was deployed, with versions and links
```

**Release (`release-please.yml` → `cd-production.yml`, environment `production`, owner approval)**
```
owner merges Release PR
  └─► release-please creates platform-vX.Y.Z tag + GitHub Release
        └─► cd-production (same run, as a called workflow)
              └─► ⏸ approval required (GitHub environment reviewers)
                    └─► terraform apply production
                    └─► promote images: retag the staging-tested digest as X.Y.Z (no rebuild)
                    └─► migrate → deploy api → deploy gateway
                    └─► deploy web: same artifacts, built for production config
                    └─► mobile-ota: eas update --channel production (JS-only changes)
                    └─► smoke tests → post deployment summary on the GitHub Release
```

If the Release PR includes a `mobile` release, `mobile-release.yml` runs EAS Build + Submit for both stores (production profile), also behind the production approval.

**Manual (`deploy-manual.yml`)** — for redeploys, single-component deploys and rollbacks:

```yaml
on:
  workflow_dispatch:
    inputs:
      environment:
        type: choice
        options: [staging, production]
      components:
        description: "Comma-separated: all, infra, backend, web-backoffice, web-portal, mobile-ota"
        default: all
      version:
        description: "Release to deploy (e.g. 1.4.0). Empty = latest commit on main (staging only)"
        required: false
```

- `components: all` deploys the full project in the safe order: infra → migrate → api → gateway → web apps → mobile OTA → smoke tests.
- **Rollback:** run with an earlier `version`; the workflow redeploys that version's ECR images and stored web artifacts. Database migrations are forward-only, so every migration must be backward compatible with the previous release (expand → migrate data → contract over two releases).

#### 33.8.4 Example: `cd-staging.yml` (condensed)

```yaml
name: cd-staging
on:
  push:
    branches: [main]

concurrency:
  group: deploy-staging
  cancel-in-progress: false

permissions:
  id-token: write
  contents: read

jobs:
  changes:
    runs-on: ubuntu-latest
    outputs:
      infra: ${{ steps.f.outputs.infra }}
      backend: ${{ steps.f.outputs.backend }}
      backoffice: ${{ steps.f.outputs.backoffice }}
      portal: ${{ steps.f.outputs.portal }}
      mobile: ${{ steps.f.outputs.mobile }}
    steps:
      - uses: actions/checkout@v4
      - id: f
        uses: dorny/paths-filter@v3
        with:
          filters: |
            infra:      ['infra/**']
            backend:    ['src/**', 'Directory.*.props', 'global.json']
            backoffice: ['frontend/apps/backoffice/**', 'frontend/packages/**']
            portal:     ['frontend/apps/portal/**', 'frontend/packages/**']
            mobile:     ['frontend/apps/mobile/**', 'frontend/packages/**']

  infra:
    needs: changes
    if: needs.changes.outputs.infra == 'true'
    uses: ./.github/workflows/_infra.yml
    with: { environment: staging, apply: true }
    secrets: inherit

  backend-build:
    needs: [changes, infra]
    if: always() && needs.changes.outputs.backend == 'true' && needs.infra.result != 'failure'
    uses: ./.github/workflows/_build-backend.yml
    with: { environment: staging }
    secrets: inherit

  backend-deploy:
    needs: backend-build
    uses: ./.github/workflows/_deploy-backend.yml
    with:
      environment: staging
      image_tag: ${{ needs.backend-build.outputs.image_tag }}
    secrets: inherit

  web:
    needs: [changes, infra]
    if: always() && needs.infra.result != 'failure' && (needs.changes.outputs.backoffice == 'true' || needs.changes.outputs.portal == 'true')
    strategy:
      matrix:
        app: [backoffice, portal]
    uses: ./.github/workflows/_deploy-web.yml
    with:
      environment: staging
      app: ${{ matrix.app }}
      changed: ${{ needs.changes.outputs[matrix.app] }}
    secrets: inherit

  mobile-ota:
    needs: changes
    if: needs.changes.outputs.mobile == 'true'
    uses: ./.github/workflows/_mobile-update.yml
    with: { channel: preview }
    secrets: inherit

  smoke:
    needs: [backend-deploy, web]
    if: always() && !contains(needs.*.result, 'failure')
    uses: ./.github/workflows/_smoke-test.yml
    with: { environment: staging }
```

> Pin every third-party action (`dorny/paths-filter`, `aws-actions/*`, `googleapis/release-please-action`, `expo/expo-github-action`) to a full commit SHA once the pipeline works, and let Dependabot propose updates.

#### 33.8.5 GitHub setup checklist

| Item | Where | Value |
|---|---|---|
| Environments | Settings → Environments | `staging` (no approval), `production` (required reviewer: you; deployment branch/tag rule: `platform-v*`, `mobile-v*`) |
| Variables (per environment) | Environment → Variables | `AWS_REGION`, `AWS_ROLE_ARN`, `ECR_REGISTRY`, `ECS_CLUSTER`, `ARTIFACTS_BUCKET`, `WEB_BUCKET_BACKOFFICE`, `WEB_BUCKET_PORTAL`, `CLOUDFRONT_ID_BACKOFFICE`, `CLOUDFRONT_ID_PORTAL`, `API_BASE_URL`, `VITE_*` public settings |
| Secrets (repository) | Settings → Secrets | `EXPO_TOKEN`; optional `RELEASE_TOKEN` (§33.7.2) |
| Branch protection | Settings → Branches → `main` | PR required, required checks from `ci.yml`, linear history, no force push |
| Tag protection | Settings → Rules → Tags | Only Actions may create `platform-v*` / `mobile-v*` |
| Actions permissions | Settings → Actions | Allow GitHub Actions to create PRs (for release-please); workflow permissions read by default |
| Dependabot | `.github/dependabot.yml` | NuGet, npm (pnpm), GitHub Actions, Terraform — weekly |

### 33.9 AWS components to install

Everything below is created by Terraform from `infra/` through the pipeline; the "Manual" column lists the few one-time steps Terraform can't do for you.

| # | Component | Terraform module | Staging | Production | Manual one-time step |
|---|---|---|---|---|---|
| 1 | S3 state bucket + GitHub OIDC provider + deploy IAM roles | `bootstrap/` | ✓ | ✓ | Run once locally with an admin profile |
| 2 | Route 53 hosted zone + DNS records | `dns-certs` | ✓ | ✓ | Register/point the domain's name servers |
| 3 | ACM certificates (regional for ALB, `us-east-1` for CloudFront) | `dns-certs` | ✓ | ✓ | — (DNS-validated automatically) |
| 4 | VPC, public/private subnets (2 AZs), security groups | `network` | ✓ | ✓ | — |
| 5 | ECR repositories (`fincore-api`, `fincore-gateway`, `fincore-migrator`) + lifecycle | `ecr` | shared | shared | — |
| 6 | RDS PostgreSQL (t4g), subnet group, parameter group, managed master secret | `rds-postgres` | `db.t4g.micro` | `db.t4g.small`, deletion protection | — |
| 7 | ECS cluster + Service Connect namespace | `ecs-cluster` | ✓ | ✓ | — |
| 8 | ECS service `api` (Fargate ARM64) + task/exec roles + log group | `ecs-service` | 0.5 vCPU / 1 GB | 0.5–1 vCPU / 1–2 GB, autoscale 1–3 | — |
| 9 | ECS service `gateway` (BFF + Mobile Gateway) | `ecs-service` | 0.25 vCPU / 0.5 GB | 0.25–0.5 vCPU, autoscale 1–2 | — |
| 10 | ECS task definition `migrator` (one-off) | `ecs-service` | ✓ | ✓ | — |
| 11 | Application Load Balancer + listeners + target groups | `alb` | ✓ | ✓ | — |
| 12 | S3 + CloudFront for Back Office and Portal (OAC, security headers, `/api` and `/bff` behaviors) | `static-site` | ✓ | ✓ | — |
| 13 | S3 artifacts bucket (versioned web builds for rollback) | `storage` | shared | shared | — |
| 14 | S3 documents bucket (KYC, statements) | `storage` | ✓ | ✓ | — |
| 15 | Cognito user pools (staff, customers) + app clients (backoffice, portal, mobile) | `cognito` | ✓ | ✓ | Create the first admin user (or seed in Staging) |
| 16 | SES domain identity + DKIM | `ses` | ✓ | ✓ | Request SES production access |
| 17 | SSM parameters (config, module switches, minimum mobile version) | `params` | ✓ | ✓ | Enter real secret values once (SecureString) |
| 18 | CloudWatch alarms, SNS alert topic, AWS Budget | `observability` | ✓ | ✓ | Confirm the alert email subscription |
| 19 | EventBridge Scheduler (scale to zero / stop RDS out of hours) | `scheduler` | optional | — | — |
| 20 | AWS WAF on CloudFront/ALB | `waf` (optional) | — | recommended before real users | — |

**Outside AWS (required for the mobile app):** an Expo account (`EXPO_TOKEN`), an Apple Developer Program membership and a Google Play Console account, connected to EAS for signing and submission.

**Install order on day one:** 1 → 2–3 → 4–7 → 13–14 → 15–17 → 11–12 → 8–10 → 18 (the `envs/<env>/main.tf` dependencies enforce this; you just run the pipeline).

### 33.10 Migrator tool

`src/Tools/FinCore.Migrator` is a tiny console app that references every module's Infrastructure project and, for each module `DbContext`, applies pending migrations in a fixed order (Organization → Identity → Customers → Ledger → Accounts → Loans → Microfinance → Cards → Notifications → Reporting). It runs as a one-off ECS task using a dedicated DB role with DDL rights; the API's runtime role has DML rights only.

### 33.11 Security baseline on AWS

- HTTPS only (ACM certificates), TLS 1.2+, HSTS; CloudFront security headers policy.
- RDS: private subnets, encryption at rest, automated backups (7 days staging / 14+ days production), deletion protection in production.
- S3: Block Public Access on all buckets; CloudFront OAC for web buckets; documents bucket accessed only via presigned URLs with short expiry.
- IAM: separate task role per service with least privilege (API: S3 documents + SES + SSM read; gateway: SSM read only).
- GitHub deploy roles scoped per environment and limited to ECR push, ECS deploy, `iam:PassRole` for the task roles, S3 sync and CloudFront invalidation.
- AWS WAF on CloudFront/ALB is optional (adds cost); recommended before handling real users, with AWS managed rule groups and rate-based rules.
- Turn on CloudTrail (management events) and GuardDuty if budget allows.

### 33.12 First deployment checklist

1. Buy/choose a domain and create a Route 53 hosted zone.
2. `cd infra/bootstrap && terraform init && terraform apply` (with your admin AWS profile).
3. Add the GitHub environments and variables from the bootstrap outputs.
3a. Complete the GitHub setup checklist (§33.8.5) and add `release-please-config.json` + `.release-please-manifest.json` (§33.7.2).
4. Open a PR with `infra/envs/staging` → review the plan comment → merge → `cd-staging.yml` applies Staging.
5. Verify SES domain (DKIM records are created by Terraform; request SES production access to email non-verified addresses).
6. Create the first staff admin user in the Cognito staff pool (or let the seeder create one in Staging).
7. Merge to `main` → `cd-staging.yml` builds, migrates and deploys the changed components. Open `https://admin.<your-domain>` and check Admin → System versions.
8. When ready, merge the release-please Release PR → `platform-v0.1.0` is tagged and released → approve the Production deployment in GitHub.

## 34. Coding Conventions

- Feature folders inside Application (`Loans/DisburseLoan/…`), one command/query per folder.
- Handlers, validators and EF configurations are `internal sealed`; only Contracts are `public` across modules.
- Primary constructors for DI; `required` members for DTOs; records for commands, queries, events, DTOs.
- C# 14 features welcome where they improve clarity (e.g., `field` keyword in property setters for validation, extension members for module registration helpers).
- No `DateTime.Now` — use `TimeProvider` / `IBusinessDateProvider`. No `double`/`float` for money.
- Async all the way; every async method takes a `CancellationToken`.
- Domain never throws for business rule failures — return `Result`.
- Naming: `XxxCommand`, `XxxCommandHandler`, `XxxQuery`, `XxxDomainEvent`, `XxxIntegrationEvent`, `XxxErrors`.
- ADRs for every significant decision (`docs/adr/NNNN-title.md`).
- `.editorconfig` enforced; analyzers at `latest-recommended`, warnings as errors.

### 34.1 Code comments

Comments are part of the code review, not an afterthought. They are written for the next engineer who has to change
the code under pressure, and they read like a senior engineer explaining a decision to a colleague: short, specific,
plain English.

**What to comment**
- **Why, not what.** The code already says what it does. Comments explain the reason: the business rule, the
  constraint, the trade-off, or the failure the code protects against.
  ✗ `// loop over the messages` ✓ `// Oldest first, so subscribers see events in the order they happened.`
- **Business and money rules** at the point they are enforced, with the spec reference (`// Spec §17: penalty starts
  only after PenaltyGraceDays.`). Rounding, allocation order, interest formulas and date rules always get a comment.
- **Non-obvious technical decisions:** concurrency (`xmin`, locks, races), transactions and outbox ordering,
  idempotency, retries, security checks (CSRF, open-redirect guards, fail-closed authorization), and anything that
  would look like a bug or an optimisation opportunity to a newcomer ("This looks redundant, but …").
- **Deliberate deviations and temporary stubs**, linked to the ADR or milestone that resolves them.

**Where**
- **C#:** XML doc comments (`/// <summary>`) on every public type and on public members whose purpose is not obvious
  from the name — especially everything in `.Contracts`, `SharedKernel` and `BuildingBlocks`. Inside method bodies use
  `//` line comments sparingly, above the line they explain.
- **TypeScript:** TSDoc (`/** … */`) on exported functions, hooks, components and types in `packages/*`; `//` line
  comments inside implementations for the same "why" cases.
- **Config and infrastructure** (`*.json` where allowed, YAML, Terraform, Dockerfiles, MSBuild): a comment on every
  non-default or surprising setting.

**Style rules**
- One idea per comment; full sentences; no shouting, emojis or jokes.
- Reference the source of a rule: spec section (`spec §25`), ADR (`ADR-0003`), or milestone spec (`spec 001`).
- `TODO` comments name the milestone or work package that resolves them: `// TODO(M1): load permissions from Identity.`
  No anonymous TODOs and no `FIXME`/`HACK` left on `main`.
- No commented-out code, no change logs or author names in comments (git history has that), and no comments that
  restate a type signature.
- Keep comments true: a change that makes a comment wrong must update or delete it in the same commit.

## 35. Development Strategy & Build Plan

This section fixes **how** FinCore is built: the order of work, what must be finished before the next thing starts, and how AI coding assistants are used. The plan is **owner-led, not agent-driven**: the project owner assigns work packages in the order below, and an AI assistant only executes the package it was given, using the skills listed for that area.

### 35.1 Working rules for AI coding assistants

These rules are copied into the repo as `CLAUDE.md` (Claude Code) and `AGENTS.md` (other agents) in the first commit.

1. **This spec is the source of truth.** Architecture, folder structure, libraries, naming and design tokens come from this document. If a skill, a tutorial or the assistant's own preference disagrees with the spec, follow the spec and flag the conflict to the owner.
2. **One work package at a time.** Only work on the package the owner assigned (§35.8 template). Don't start the next package, refactor unrelated code, or "improve" other modules.
3. **No scope invention.** Don't add features, endpoints, screens, libraries or infrastructure that aren't in the package. Propose them in a note instead.
4. **No new dependencies without approval.** Any package not named in this spec needs owner approval and, if architectural, an ADR in `docs/adr/`.
5. **Ask when the spec is unclear.** Stop and ask rather than guess on business rules, money calculations, security or data model questions.
6. **Load the skills for the area first** (§35.2) before writing code in that area.
7. **Finish means Definition of Done** (§35.5), not "code compiles".
8. **Small pull requests.** One package = one branch = one PR, ideally under ~400 changed lines excluding generated code.
9. **Never touch secrets, production infrastructure or real credentials.** Use dev/test values only.
10. **Commit as the owner, in readable language.** Use the repo's configured Git identity, add no AI co-author or "generated with" lines, and write every commit message in the plain-English format of §35.7 so the owner can follow what changed and why.

### 35.2 Skills to use per area

The owner installs skills; the assistant loads the ones for the area it's working in. Skills give the assistant expert, up-to-date guidance; this spec still decides *what* to build.

> Skill names and install commands below were checked against the publishers' repositories in October 2026. Skills are renamed and added over time, so verify with the discovery step (§35.2.4) before each new track starts.

#### 35.2.1 React web (`apps/backoffice`, `apps/portal`, `packages/ui`, `packages/design-tokens`)

| Skill | Publisher | Use it for |
|---|---|---|
| `frontend-design` | Anthropic (`anthropics/skills`) | Distinctive, non-generic UI implementation; apply it *within* the Soft Ledger tokens in §32, not instead of them |
| `react-best-practices` | Vercel (`vercel-labs/agent-skills`) | Performance rules: avoiding waterfalls, bundle size, re-renders, data fetching |
| `composition-patterns` | Vercel | Component APIs for `packages/ui` (compound components, avoiding boolean-prop sprawl) |
| `web-design-guidelines` | Vercel | UI review: accessibility, focus states, forms, `tabular-nums`, `Intl` formatting, dark mode |
| `webapp-testing` | Anthropic | Playwright end-to-end tests for the web apps |

Install:
```bash
# Claude Code: Anthropic skills via the plugin marketplace (or copy into .claude/skills/)
# Any agent, via the skills CLI, from the repo root:
npx skills add anthropics/skills --skill frontend-design --skill webapp-testing
npx skills add vercel-labs/agent-skills --skill react-best-practices --skill composition-patterns --skill web-design-guidelines
```

Not used: Next.js- and Vercel-hosting-specific skills (`vercel-optimize`, `deploy-to-vercel`, `react-view-transitions` with Next.js), because FinCore uses Vite and AWS.

#### 35.2.2 Expo mobile (`apps/mobile`)

Official Expo skills (`expo/skills`). **Always load `expo-overview` first**, then the specific skill:

| Skill | Use it for | When |
|---|---|---|
| `expo-overview` | Entry point and shared setup rules | Every mobile session |
| `expo-project-structure` | `app/` routes-only + `src/` layout | M0 scaffold (adapt to §31.2) |
| `expo-router` | Routes, stacks, tabs, modals, sheets | All screens |
| `expo-design-system` | Token theme, component conventions, drift audits | `packages/design-tokens` → NativeWind, mobile UI kit |
| `expo-native-ui` | Native-feeling styling, controls, effects (blur, gradients) | All screens |
| `expo-animation` | Reanimated, gestures, haptics | Card stack, tab bar, success screens |
| `expo-data-fetching` | TanStack Query, caching, offline | All API work |
| `expo-dev-client` | Development builds (biometrics, secure store, camera) | M1 onward |
| `expo-upgrade` | SDK upgrades | Only when the owner schedules an upgrade |
| `eas-app-stores`, `eas-update`, `eas-workflows` | Store builds, OTA updates, EAS CI | M9 release (these use paid EAS services) |

Also from Vercel: `react-native-skills` (React Native performance, lists, images, state patterns).

Install:
```bash
# Claude Code (recommended — also wires up the Expo MCP server):
claude plugin install expo@claude-plugins-official
# Other agents:
npx skills@latest add expo/skills --skill '*'
npx skills add vercel-labs/agent-skills --skill react-native-skills
```

Spec precedence for mobile: if an Expo skill suggests a different library (e.g., native tabs or `@expo/ui` instead of the custom `FloatingTabBar`, or a different styling approach than NativeWind), keep the spec's choice and raise it with the owner.

#### 35.2.3 Backend (C# / .NET 10) and infrastructure

- **C# / .NET skills are selected and installed by the owner.** The assistant does not search for, install or switch .NET skills on its own; it uses whatever the owner has enabled in `.claude/skills/` (or the agent's equivalent) and this spec.
- **Terraform (optional, owner's choice):** HashiCorp publishes official skills (`claude plugin marketplace add hashicorp/agent-skills` then `claude plugin install terraform@hashicorp`, or `npx skills add hashicorp/agent-skills`). Use `terraform-style-guide` for `infra/`.

#### 35.2.4 Skill discovery step (start of each track)

Before M0 work starts on a track (web or mobile), and again at the start of each milestone, the assistant runs a **read-only discovery** and reports back; it installs nothing without approval:

1. List installed skills (`/plugin` and `.claude/skills/` in Claude Code, or the agent's equivalent).
2. Search for available skills for the track's keywords — web: `react`, `vite`, `tailwind`, `shadcn`, `tanstack`, `playwright`, `accessibility`; mobile: `expo`, `react native`, `eas`, `reanimated` — in the plugin marketplace and on skills.sh.
3. Report: name, publisher, what it covers, whether it conflicts with this spec. Prefer official publishers (Anthropic, Expo, Vercel, HashiCorp).
4. The owner approves; the assistant then installs and records the skill list in `docs/skills.md`.

### 35.3 Strategy overview

1. **Walking skeleton first.** Before any business feature, every layer exists and deploys: .NET host, one module, Postgres, BFF, both React apps, the Expo app, CI and the AWS Staging environment. Problems with plumbing surface in week 2, not month 6.
2. **Vertical slices by module.** Each milestone delivers one business capability end to end: domain → API → web screens → mobile screens (where customer-facing) → tests → deployed to Staging.
3. **Contract-first between backend and frontends.** The owner first publishes the module's endpoints and DTOs (Presentation project with stub handlers returning sample data). The OpenAPI document is then frozen for the milestone, `packages/api-client` is regenerated, and web/mobile work starts against MSW mocks **in parallel** with the real backend implementation.
4. **Design system before screens.** Tokens and core components (§32) are built in M1, so every later screen is assembled from finished parts.
5. **Money correctness before UI polish.** Ledger, accounts and loan calculations are complete and tested before their screens are polished.
6. **Deploy continuously.** Every merge to `main` deploys Staging from M0 onward; Production starts at M9.

**Four tracks**

| Track | Owner of execution | Scope |
|---|---|---|
| A — Backend | Owner (C#), optionally with assistant | Modules, building blocks, migrations, jobs |
| B — Web | Assistant under owner review | BFF config, `packages/*`, Back Office, Portal |
| C — Mobile | Assistant under owner review | `apps/mobile`, Mobile Gateway profile usage, push |
| D — Infra/DevOps | Owner, optionally with assistant | Terraform, GitHub Actions, AWS |

### 35.4 Milestones: start, deliver, finish

Indicative durations assume **one full-time developer using an AI assistant**; see §35.9 for other team shapes.

| # | Milestone | Can start when | Delivers | Finished when | Indicative |
|---|---|---|---|---|---|
| **M0** | Foundations & walking skeleton | Spec approved | Repo, `.slnx`, Directory props, SharedKernel + BuildingBlocks skeleton, `IModule` host, Organization module stub, outbox/inbox, architecture tests, CI; frontend monorepo, `packages/config`, `packages/api-client` generation, empty Back Office + Portal; Expo app shell (router, providers); BFF skeleton; Terraform bootstrap + Staging; `cd-staging.yml` + release-please + `/api/version` and version display in all three clients; `CLAUDE.md`/`AGENTS.md`; skill discovery done | CI green; a merge to `main` deploys Staging; `/health/ready` OK on AWS; Back Office and Portal load from CloudFront; Expo app runs in a dev build and calls `/health` through the Mobile Gateway | 2–3 weeks |
| **M1** | Platform core & design system | M0 finished | Organization (institutions, branches, business date, holidays); Identity & Approvals (staff, roles, permissions, maker-checker); login end-to-end for all three clients (Keycloak local, Cognito on AWS); `packages/design-tokens`; `packages/ui` core (buttons, inputs, pills, cards, DataTable, TileNav, PillTabs, StepDots, ReviewSheet); mobile UI kit core (FloatingTabBar, BalanceHero shell, QuickActions, TransactionItem); Back Office shell (sidebar, top bar with business date, approvals inbox); Storybook + mobile Design Gallery | Staff can sign in, set up a Bank and an MFI with branches and users; a maker-checker approval works end to end; every core component is in Storybook / Design Gallery in light and dark | 3–4 weeks |
| **M2** | Customers & KYC | M1 finished | Customer, KYC case, documents (S3), simulated screening, risk rating; Back Office customer search, create, Customer 360 shell, KYC review queue; Portal + mobile onboarding screens (ChoiceTiles, document capture, status tracker) up to "KYC submitted" | A customer reaches `KycApproved` via auto and manual review, from both Back Office and mobile onboarding, on Staging | 3 weeks |
| **M3** | General Ledger | M1 finished (can overlap M2 on the backend) | Chart of accounts seeding, posting rules, journal engine, manual journal with maker-checker, trial balance; Back Office ledger screens | Manual journals post and reverse; trial balance always balances (property test); ledger screens show it | 2–3 weeks |
| **M4** | Accounts, account opening & transfers | M2 **and** M3 finished | Deposit products, opening workflow, deposits/withdrawals/transfers with idempotency, holds, statements (PDF), GL postings, reconciliation check; Back Office account screens; Portal + mobile: complete onboarding to an active account, home (BalanceHero, accounts), transactions, transfer flow with ReviewSheet and biometric step-up | First full money movement on Staging: a new customer onboards on mobile, gets an account, receives a deposit, transfers money; sub-ledger equals GL | 4–5 weeks |
| **M5** | Loans & End of Day v1 | M4 finished | Loan products, calculators (golden tests), application → assessment (HealthScoreCard staff view) → approval → disbursement → repayment, delinquency, penalties; EOD orchestrator with accruals, DPD, reconciliation, business-date advance; Back Office loan screens + PipelineBoard; Portal + mobile loan apply, schedule (PaymentCalendar, AmountBreakdown), repay | Full loan lifecycle E2E passes; a 30-business-day EOD simulation runs clean with sub-ledger = GL every day | 5–6 weeks |
| **M6** | Microfinance | M5 finished | Centers, groups, cycles, meetings, collection sheets (offline-capable web view), PAR; MemberShareBar; mobile shows group membership and group loan for members | A 10-member group completes a cycle with weekly collections; PAR report correct | 3–4 weeks |
| **M7** | Cards | M4 finished (can run in parallel with M5/M6 if a second developer exists) | Card products, issuance (tokenized), authorization simulator, clearing, statements, interest, minimum payment, payments, disputes, autopay; Back Office card screens; Portal + mobile CardStack, controls, freeze, statements, pay card, LimitBar | Three billing cycles simulated with correct interest and minimum due; customer freezes a card on mobile and an authorization is declined | 5–6 weeks |
| **M8** | Notifications, Reporting & Insights | M5 finished (M6/M7 events added as they land) | Templates, email (SES), push (Expo), device registration; reporting projections, exports; dashboards with KpiTile/TrendCard/MixTreemap; customer HealthScoreCard and rule-based InsightBubbles | Customers receive push/email for key events; Back Office dashboard numbers match source data | 3 weeks |
| **M9** | Hardening & v1.0 release | M5–M8 finished | Load tests, AML rules, security review, accessibility audit (web + mobile), mobile device-integrity checks, WAF decision, Production environment, EAS production builds and store submission, docs polish | Production deployed by tag with approval; app approved in TestFlight / Play internal testing; all E2E suites green; DoD met for every milestone | 3–4 weeks |

**Dependency summary**

```
M0 ─► M1 ─┬─► M2 ─┐
          └─► M3 ─┴─► M4 ─┬─► M5 ─┬─► M6 ─┐
                          │       └─► M8 ─┼─► M9
                          └─► M7 ─────────┘
```

### 35.5 Order of work inside every milestone

The same sequence for every module, so nothing is started out of order:

1. **Kickoff (owner):** confirm scope from Part 3 of this spec, list work packages, run the skill discovery step (§35.2.4) for the web and mobile tracks.
2. **Contract (Track A):** endpoints, request/response DTOs, error codes and integration events defined; stub handlers return sample data; OpenAPI published. *Gate: owner freezes the contract for the milestone.*
3. **Parallel build:**
   - Track A: domain model + unit tests → handlers → persistence + migrations → integration tests → event handlers.
   - Track B: regenerate `packages/api-client` → MSW handlers from the contract → Back Office/Portal screens → component tests.
   - Track C: same client → mobile screens → component tests.
   - Track D: new infra if needed (e.g., S3 bucket in M2, SES in M8).
4. **Integrate:** frontends switch from MSW to the real API on Staging; fix contract mismatches (any contract change goes back through step 2).
5. **End-to-end:** Playwright (web) and Maestro (mobile) journeys for the milestone's exit criteria.
6. **Acceptance (owner):** demo on Staging against the "Finished when" column. Only then does the next dependent milestone start.

### 35.6 Definition of Done

A work package is done only when all of these hold:

- [ ] Matches this spec (or the spec was updated in the same PR with owner approval).
- [ ] Unit tests for domain logic and calculators; integration tests for endpoints/persistence; component tests for screens.
- [ ] Architecture tests and lint pass; no new analyzer warnings; TypeScript `strict` with no `any` escapes.
- [ ] Money: `decimal` / `decimal.js` only; idempotency keys on money-moving calls; ledger balances.
- [ ] Security: permissions enforced server-side; no PII in logs; no secrets in code.
- [ ] UI: built from `packages/ui` / mobile UI kit and tokens only (no ad-hoc colors or spacing); light + dark checked; accessibility checks pass (axe on web, screen-reader labels on mobile).
- [ ] OpenAPI and `packages/api-client` regenerated and committed if the contract changed.
- [ ] Docs: ADR for any architectural decision; `docs/skills.md` updated if skills changed.
- [ ] Comments follow §34.1: the *why* of business rules, money handling, concurrency and security is documented in the code; public/exported APIs have XML docs or TSDoc.
- [ ] Commits follow §35.7 (owner identity, no AI attribution, readable messages); PR uses the template.
- [ ] Merged to `main` and deployed to Staging automatically; smoke test green and `/api/version` shows the new commit.

### 35.7 Commits & pull requests

All commits are made **as the project owner**. The AI assistant writes the code and the commit messages, but commits use the owner's Git identity and carry **no AI attribution**.

#### 35.7.1 Identity and attribution rules

1. Commits use the identity already configured in the repo (`git config user.name` / `user.email`). The assistant must **never** change Git config, pass `--author`, or set `GIT_AUTHOR_*` / `GIT_COMMITTER_*` variables.
2. No `Co-Authored-By: Claude …` trailer, no "Generated with Claude Code" line, no AI mention in commit messages or PR descriptions.
3. If commit signing is enabled (GPG/SSH), commits are signed with the owner's key as normal; the assistant never bypasses signing (`--no-gpg-sign`) or hooks (`--no-verify`).

**Claude Code setting** (committed in `.claude/settings.json` so it applies to the whole repo):

```json
{
  "includeCoAuthoredBy": false,
  "attribution": {
    "commit": "",
    "pr": ""
  }
}
```

> `includeCoAuthoredBy` is the older setting and `attribution` the newer one; keeping both is harmless. Setting names can change between Claude Code versions, so confirm in the Claude Code settings docs and check the first commit (`git log -1 --format=%B`) to be sure no trailer appears. For other agents, set the equivalent option in their config.

**Safety net — a commit-msg hook** that rejects AI attribution even if a setting is missed:

```bash
# .githooks/commit-msg   (enable once per clone: git config core.hooksPath .githooks)
#!/usr/bin/env bash
if grep -qiE '^(co-authored-by:.*(claude|anthropic|copilot|cursor|codex)|.*generated with (claude|ai))' "$1"; then
  echo "Commit rejected: remove AI attribution lines from the commit message." >&2
  exit 1
fi
```

The same check runs in CI on every pull request (`ci.yml` job `commit-messages`), so an attributed commit can't reach `main`.

#### 35.7.2 Commit message format

Commits follow **Conventional Commits** with a plain-English body, so the history reads like a changelog the owner can follow without opening the code.

```
<type>(<scope>): <short summary in plain English, max ~72 chars>

What changed:
- <each change as a short, readable sentence>

Why:
<one or two sentences linking to the business reason>

How to check:
- <command, screen or test that shows it works>

Refs: WP-M4-07, spec §31.6
```

| Part | Rules |
|---|---|
| `type` | `feat` (new behavior), `fix` (bug fix), `test`, `refactor` (no behavior change), `docs`, `chore` (tooling, deps), `ci`, `infra` (Terraform/AWS), `style` (formatting only), `perf` |
| `scope` | The module or app: `loans`, `accounts`, `ledger`, `cards`, `mfi`, `customers`, `identity`, `org`, `bff`, `backoffice`, `portal`, `mobile`, `ui`, `tokens`, `api-client`, `infra`, `ci` |
| Summary | Imperative, describes the outcome a user or developer would notice ("add transfer review screen", not "update files") |
| What changed | Bullet per meaningful change, in plain language; name new screens, endpoints, tables or rules |
| Why | The reason in business terms (link to the spec rule or work package) |
| How to check | How the owner can see or verify it: a URL on Staging, a screen in the app, a test command |
| Refs | Work package ID and spec sections |

**Granularity:** one commit per logical step (e.g., domain + tests, then endpoint, then screen), never one giant "WIP" commit and never a commit mixing unrelated changes. Generated code (`packages/api-client`, EF migrations) goes in its own commit with type `chore` and a note of what generated it.

**Examples**

```
feat(mobile): add money transfer flow between own accounts

What changed:
- New screens: enter amount, review transfer, and result with reference number.
- Amount entry blocks values above the available balance and shows a message.
- A transfer key is created when the screen opens and reused on retry, so a
  network retry can't send the money twice.
- Transfers above 500 require Face ID / fingerprint before sending.

Why:
Customers need to move money between their own accounts from the app (spec §31.6).

How to check:
- Open the app on Staging, Home > Transfer, send 10.00 between two accounts.
- Run: pnpm --filter mobile test transfers

Refs: WP-M4-07, spec §31.6, §31.7, §32.5
```

```
fix(loans): stop charging penalty during the grace days

What changed:
- Penalty interest now starts only after the product's penalty grace days.
- Added tests for loans that are 1, 3 and 4 days late with a 3-day grace period.

Why:
Customers were charged penalty on day 1 overdue, which breaks the product rule in §17.

How to check:
- dotnet test --filter "FullyQualifiedName~PenaltyAccrual"

Refs: WP-M5-14, spec §17, §23.4
```

```
chore(api-client): regenerate client for the accounts transfer endpoint

What changed:
- Regenerated packages/api-client from the updated Accounts OpenAPI document
  (new POST /transfers request and response types).

Why:
The mobile and portal transfer screens need the new endpoint types.

How to check:
- pnpm api:generate produces no further changes.

Refs: WP-M4-02
```

#### 35.7.3 Pull requests

- One work package = one branch (`wp/M4-07-mobile-transfer`) = one PR into `main`.
- The PR description uses `.github/pull_request_template.md`:

```markdown
## Summary
<2–4 plain-English sentences: what this PR adds or fixes and why>

## Changes
- <readable list of changes, grouped by app/module>

## How to test
- <steps on Staging or locally>

## Screenshots
<before/after for any UI change, light and dark>

## Checklist
- [ ] Definition of Done (§35.6) met
- [ ] Spec updated if behavior differs from the spec
- [ ] No AI attribution in commits or this description

Refs: WP-…, spec §…
```

- Merge with **squash** for small packages (the squash message is edited into the format above) or **rebase** when the individual commits tell a useful story. The owner performs the merge.
- Releases are cut by merging the release-please **Release PR** (§33.7.2): it bumps versions, updates `CHANGELOG.md` from the commit messages (Features, Bug Fixes, Infrastructure), creates the `platform-vX.Y.Z` / `mobile-vX.Y.Z` tag and GitHub Release, and starts the production deployment.

### 35.8 Work package template

The owner hands each task to the assistant in this form (store them as GitHub issues):

```markdown
## WP-M4-07: Mobile transfer flow

**Track:** C — Mobile
**Spec references:** §31.6 (Transfers), §31.7 (rules), §32.5 (AmountKeypad, ReviewSheet), §15 (Accounts endpoints)
**Skills to load:** expo-overview, expo-router, expo-native-ui, expo-animation, expo-data-fetching, react-native-skills
**Depends on:** WP-M4-02 (transfer endpoint contract frozen), WP-M1-12 (ReviewSheet component)

**Goal:** A signed-in customer can send money between their own accounts.

**In scope:**
- Screens `app/transfer/new.tsx`, `confirm.tsx`, `result.tsx`
- Idempotency key created on screen open and persisted with the draft
- Biometric step-up above the configured threshold

**Out of scope:** beneficiaries for other customers, scheduled transfers.

**Files allowed to change:** `apps/mobile/app/transfer/**`, `apps/mobile/src/features/transfers/**`, tests.

**Acceptance criteria:**
1. Amount entry rejects more than the available balance with an inline message.
2. Review sheet shows from, to, amount, fee (0), reference.
3. Retrying after a network error does not create a second transfer.
4. Maestro flow `e2e/transfer.yaml` passes on Android emulator and iOS simulator.

**Definition of Done:** §35.6
```

**Suggested first work packages (M0, web and mobile tracks)**

| ID | Package | Skills |
|---|---|---|
| WP-M0-10 | pnpm + Turborepo monorepo, `packages/config`, ESLint/Prettier/TS base | react-best-practices |
| WP-M0-11 | `apps/backoffice` and `apps/portal` Vite scaffolds with TanStack Router + Query providers, empty home routes | react-best-practices |
| WP-M0-12 | `packages/api-client`: Orval config, `http.ts` (cookie+CSRF and bearer modes), generation script, CI drift check | react-best-practices |
| WP-M0-13 | Vite dev proxy to the BFF; `/bff/user` call and RequireAuth placeholder | — |
| WP-M0-14 | Playwright setup with one smoke test per app | webapp-testing |
| WP-M0-20 | `apps/mobile` Expo scaffold adapted to §31.2, Expo Router root layout, providers, `EXPO_PUBLIC_API_URL` | expo-overview, expo-project-structure, expo-router |
| WP-M0-21 | Development build profile (`eas.json` development) and local dev client | expo-dev-client |
| WP-M0-22 | Mobile health check screen calling the Mobile Gateway through `packages/api-client` | expo-data-fetching |
| WP-M0-23 | Jest (`jest-expo`) + React Native Testing Library + one Maestro smoke flow | react-native-skills |

### 35.9 Timeline estimate

The full v1 scope here is large: 10 backend modules with real accounting rules, two web apps, a mobile app with biometrics and push, and AWS infrastructure. Indicative totals:

| Team shape | Full v1 (M0–M9) | MVP (M0–M5: onboarding, accounts, ledger, loans, EOD) |
|---|---|---|
| 1 developer full-time with an AI assistant | **9–12 months** | 5–6 months |
| 1 developer part-time (~15 h/week) with an AI assistant | 2–3 years | 12–15 months |
| Small team (2 backend, 1 web, 1 mobile; part-time DevOps/QA) | 5–7 months | 3–4 months |

What moves the estimate most: the Ledger/Accounts/Loans correctness work (M3–M5) and Cards (M7) are where most of the time goes and where AI help saves the least, because the rules must be verified carefully. Screens and scaffolding go fastest with AI assistance. Cutting Cards or Microfinance from v1 saves roughly 1–1.5 months each.

## 36. Path to Microservices

The design keeps extraction cheap. To extract a module (e.g., Cards):

1. Swap `InMemoryEventBus` → a broker-backed bus (Amazon SNS + SQS on AWS, or RabbitMQ) behind the same `IEventBus` interface — integration events already serialize to JSON.
2. Create a new host `FinCore.Cards.Api` that registers only `CardsModule` + BuildingBlocks.
3. Point the Cards `DbContext` at its own database (schema already isolated; no cross-schema FKs to untangle).
4. Replace in-process implementations of `IAccountsModuleApi`/`ICustomersModuleApi` used by Cards with HTTP/gRPC clients (same interfaces, in a `Cards.Infrastructure.Clients` folder), with resilience policies.
5. Add an API gateway (YARP) in front of the monolith + extracted service.
6. Replace any synchronous cross-module command (e.g., `CreateProspectAsync`) with a saga / process manager.

Good first candidates: **Notifications** and **Reporting** (pure subscribers), then **Cards** (high-throughput authorization path).

## 37. Glossary

| Term | Meaning |
|---|---|
| **AML** | Anti-Money Laundering — monitoring transactions for suspicious activity |
| **APR** | Annual Percentage Rate on card balances |
| **Business date** | The institution's accounting date; advances only at EOD |
| **Control account** | GL account summarizing a sub-ledger (e.g., all savings balances) |
| **DPD** | Days Past Due on a loan's oldest unpaid installment |
| **DTI** | Debt-to-Income ratio |
| **EDD** | Enhanced Due Diligence for high-risk customers |
| **EMI** | Equated Monthly Installment (declining-balance payment) |
| **ALB** | Application Load Balancer (AWS) |
| **Release PR** | Pull request kept open by release-please with the next version bump and changelog; merging it publishes a release |
| **SemVer** | Semantic Versioning: MAJOR.MINOR.PATCH |
| **EAS** | Expo Application Services — cloud builds, store submission and over-the-air updates |
| **EOD** | End-of-Day batch processing |
| **Joint liability** | Group members are collectively responsible for each member's loan |
| **KYC** | Know Your Customer — identity verification and risk assessment |
| **Maker-checker** | Four-eyes control: one user initiates, another approves |
| **MFI** | Microfinance Institution |
| **Outbox / Inbox** | Patterns for reliable event publishing and idempotent consumption |
| **PAN** | Primary Account Number (card number) |
| **PAR** | Portfolio at Risk — share of loan portfolio overdue beyond X days |
| **OAC** | Origin Access Control: lets CloudFront read a private S3 bucket |
| **OIDC (GitHub)** | GitHub Actions proving its identity to AWS without stored keys |
| **PKCE** | Proof Key for Code Exchange — protects the OAuth code flow for public clients like mobile apps |
| **PEP** | Politically Exposed Person |
| **Sub-ledger** | Detailed customer-level ledger (accounts, loans, cards) |
| **Tokenization** | Replacing sensitive card data with a non-sensitive token |

---

*FinCore Project Specification — v1.0. Keep this document in `docs/` and update it alongside Architecture Decision Records as the design evolves.*
