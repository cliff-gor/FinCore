# FinCore — rules for AI coding assistants

FinCore is a modular-monolith financial platform (.NET 10 / C# 14, React 19, Expo). The source of truth is
[`docs/FinCore-Project-Spec.md`](docs/FinCore-Project-Spec.md) plus the approved milestone specs in
[`docs/specs/`](docs/specs/). These rules come from master spec §35.1.

## Working rules
1. **The spec is the source of truth.** Architecture, folder structure, libraries, naming and design tokens come from
   the spec. If a skill, tutorial or your own preference disagrees, follow the spec and flag the conflict to the owner.
2. **One work package at a time.** Only work on the assigned package (§35.8). Don't start the next one, refactor
   unrelated code, or "improve" other modules.
3. **No scope invention.** No features, endpoints, screens, libraries or infrastructure outside the package — propose them in a note.
4. **No new dependencies without approval.** Packages not named in the spec need owner approval (and an ADR if architectural).
5. **Ask when the spec is unclear** — especially business rules, money calculations, security and data model.
6. **Load the skills for the area first** (see below and `docs/skills.md`).
7. **Done means Definition of Done** (§35.6), not "it compiles".
8. **Small pull requests:** one package = one branch = one PR, ideally < 400 changed lines excluding generated code.
9. **Never touch secrets, production infrastructure or real credentials.** Dev/test values only.
10. **Commit as the owner, in readable language.** Use the repo's git identity; no AI co-author or "generated with"
    lines (enforced by `.claude/settings.json` and `.githooks/commit-msg`); Conventional Commits with the plain-English
    body of §35.7.2 (What changed / Why / How to check / Refs).

## Non-negotiables in code
- Money is `decimal` (C#) / `decimal.js` (TS) — never `double`/`float`/JS `number`. Banker's rounding by default (ADR-0004).
- Financial logic uses `IBusinessDateProvider`, never `DateTime.Now`; audit timestamps use `TimeProvider`.
- Modules talk only via `.Contracts` (read-only module APIs + integration events through the outbox). No cross-schema FKs.
- Business failures return `Result`; endpoints map them to RFC 9457 ProblemDetails with a stable `code`.
- Handlers, validators and EF configurations are `internal sealed`. Architecture tests enforce boundaries.
- Money-moving endpoints require `Idempotency-Key` (`RequireIdempotencyKey()`).

## Skills per area (§35.2)
- **Backend (C#/.NET):** use the skills the owner enabled; do not install or switch .NET skills yourself.
- **Web (`frontend/apps/backoffice|portal`, `packages/ui|design-tokens`):** `frontend-design`, `react-best-practices`,
  `composition-patterns`, `web-design-guidelines`, `webapp-testing`.
- **Mobile (`frontend/apps/mobile`):** always `expo-overview` first, then `expo-router`, `expo-native-ui`,
  `expo-data-fetching`, `expo-animation`, `expo-dev-client` as relevant; plus `react-native-skills`.
- **Infra:** optional `terraform-style-guide`.

## Common commands
```bash
dotnet build FinCore.slnx                      # warnings are errors
dotnet test --solution FinCore.slnx            # unit + architecture + integration (Docker needed; MTP runner)
dotnet run --project src/Aspire/FinCore.AppHost
ASPNETCORE_ENVIRONMENT=Development dotnet ef migrations add <Name> \
  --project src/Modules/<M>/FinCore.Modules.<M>.Infrastructure \
  --startup-project src/Bootstrapper/FinCore.Api --context <M>DbContext --output-dir Persistence/Migrations
cd frontend && pnpm install && pnpm dev
```
