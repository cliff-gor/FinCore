# ADR-0001: Modular monolith

- **Status:** Accepted
- **Context:** FinCore covers ten business capabilities but is built by one developer with an AI assistant and
  must run cheaply (spec §33). Microservices would add network, deployment and consistency costs from day one.
- **Decision:** One deployable host (`FinCore.Api`) composed of modules. Each module has five projects
  (Domain, Application, Infrastructure, Presentation, Contracts), its own schema and DbContext, and talks to other
  modules only through its `.Contracts` (read-only APIs and integration events). Boundaries are enforced by project
  references and NetArchTest rules (`tests/FinCore.ArchitectureTests`). Modules can be switched off per environment
  (`Modules:<Name>:Enabled`).
- **Consequences:** Simple operations and local development; boundaries stay extractable (spec §36). Discipline is
  required — the architecture tests are the safety net. Each module's `IModule` maps its own endpoints, so a module's
  Infrastructure project references its Presentation project (the host only registers modules).
