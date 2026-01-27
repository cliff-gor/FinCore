# FinCore — Unified Financial Services Platform

A modular-monolith financial platform built with **.NET 10** and **C# 14**: Bank/MFI organization management,
customers & KYC, deposit accounts, loans, microfinance groups, credit cards and a shared double-entry general ledger —
with a React Back Office, a React Customer Portal and an Expo mobile app.

> **Disclaimer:** FinCore is a learning and portfolio project. It simulates real financial processes but is not
> certified for production use, regulatory compliance (KYC/AML, PCI DSS, Basel, IFRS 9), or handling real customer money.

- **Master specification:** [`docs/FinCore-Project-Spec.md`](docs/FinCore-Project-Spec.md)
- **Current milestone spec:** [`docs/specs/001-m0-foundations.md`](docs/specs/001-m0-foundations.md) (M0 — walking skeleton)
- **Architecture decisions:** [`docs/adr/`](docs/adr/)

## Status: M0 — foundations & walking skeleton
| Area | State |
|---|---|
| Backend | SharedKernel, BuildingBlocks (dispatcher, outbox/inbox, in-process bus, idempotency), API host, Organization module (institutions, currencies, business date), Migrator |
| Gateway | `FinCore.Bff` in BackOffice / Portal / Mobile modes (CSRF check, min-app-version check, login stubs) |
| Frontend | pnpm/Turborepo monorepo, Back Office + Portal shells, Expo app shell, shared packages |
| DevOps | docker-compose, Aspire AppHost, CI + release-please, Terraform skeleton (nothing deployed) |

## Getting started
Prerequisites: .NET 10 SDK, Node.js 22/24 with Corepack (`corepack enable`), Docker.
The checkout path must not contain `#` (e.g. `C#-Projects`): the Vite dev server and Vitest break on it.
Dev sign-in users and passwords are in [`docs/dev-accounts.md`](docs/dev-accounts.md); sign in through the BFF URLs
(https://localhost:7200 Back Office, https://localhost:7201 Portal), which serve the web apps in Development.

```bash
git config core.hooksPath .githooks          # once per clone: blocks AI attribution in commit messages
dotnet tool restore
(cd frontend && pnpm install)
dotnet run --project src/Aspire/FinCore.AppHost   # Postgres, Keycloak, Seq, API, gateways, web apps
```

Without Aspire:
```bash
docker compose -f deploy/docker-compose.yml up -d
dotnet run --project src/Bootstrapper/FinCore.Api
dotnet run --project src/Gateway/FinCore.Bff --launch-profile BackOffice
cd frontend && pnpm dev
```

| Service | URL |
|---|---|
| API / Scalar | https://localhost:7100 · https://localhost:7100/scalar |
| Back Office / Portal (Vite) | http://localhost:5173 · http://localhost:5174 |
| BackOffice BFF / Portal BFF / Mobile Gateway | https://localhost:7200 · 7201 · 7202 |
| Keycloak / Seq | http://localhost:8080 · http://localhost:5341 |

In Development the API migrates every module on start-up and seeds **FinCore Bank (FCB)** and
**FinCore Microfinance (FCMFI)** in KES.

## Tests
```bash
dotnet test --solution FinCore.slnx                                        # everything (integration tests need Docker)
dotnet test --project tests/FinCore.ArchitectureTests                       # boundary rules only
dotnet test --solution FinCore.slnx --filter-trait "Category=Integration"  # Testcontainers-based tests
cd frontend && pnpm test && pnpm e2e
```
Set `FINCORE_TEST_POSTGRES_IMAGE` to use a different Postgres image for Testcontainers.
