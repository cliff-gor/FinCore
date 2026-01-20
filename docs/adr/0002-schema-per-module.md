# ADR-0002: One PostgreSQL database, one schema per module

- **Status:** Accepted
- **Context:** Modules must own their data without the cost of one database server each.
- **Decision:** One PostgreSQL 17 database; each module owns a schema (`org`, `identity`, …) with its own DbContext
  and `__ef_migrations` history table in that schema. No foreign keys across schemas — cross-module references are
  plain `uuid` columns. `deploy/postgres/init-schemas.sql` prepares one role per module with rights only on its schema.
  Cross-cutting platform tables (idempotency records) live in a `platform` schema. Snake-case naming via
  `EFCore.NamingConventions`. Aggregates use PostgreSQL `xmin` for optimistic concurrency.
- **Consequences:** Extracting a module later means pointing its DbContext at a new database. In M0 the app connects
  as the database owner in Development; per-module roles are enforced when environments are provisioned.
  Production migrations run through `FinCore.Migrator`, never at API start-up.
