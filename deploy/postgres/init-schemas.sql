-- =============================================================================
-- FinCore — module schemas and per-module roles (master spec §11)
--
-- * One schema per module, owned by the role that runs this script
--   (`fincore` in local Development; the migrator role on AWS).
-- * One role per module (`fincore_<schema>`) with rights ONLY on its own schema,
--   so an accidental cross-schema query fails at the database level.
-- * Idempotent: safe to run any number of times.
--
-- Local use: docker-compose mounts this file into /docker-entrypoint-initdb.d/,
-- so Postgres runs it once when the data volume is first created. Re-run by hand:
--   psql -h localhost -U fincore -d fincore -f deploy/postgres/init-schemas.sql
--
-- M0 note: in Development the API connects as `fincore` (the schema owner) and
-- applies migrations at startup. The per-module roles are prepared for later
-- milestones. They are created NOLOGIN and WITHOUT passwords — no credentials
-- live in this repository. To use one, a login role is granted membership, e.g.:
--   CREATE ROLE fincore_loans_app LOGIN PASSWORD '<from SSM>' IN ROLE fincore_loans;
--
-- Ledger journal tables are append-only (§11): the ledger migrations REVOKE
-- UPDATE, DELETE on those tables from fincore_ledger after creating them.
-- =============================================================================

-- Tighten the public schema (PostgreSQL 15+ already removes CREATE from PUBLIC).
REVOKE CREATE ON SCHEMA public FROM PUBLIC;

DO
$$
DECLARE
    module_schema text;
    module_role   text;
BEGIN
    FOREACH module_schema IN ARRAY ARRAY[
        'org', 'identity', 'customers', 'accounts', 'ledger',
        'loans', 'mfi', 'cards', 'notifications', 'reporting'
    ]
    LOOP
        module_role := 'fincore_' || module_schema;

        -- Role (no login, no password; see header)
        IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = module_role) THEN
            EXECUTE format('CREATE ROLE %I NOLOGIN', module_role);
        END IF;

        -- Schema owned by the current (migrator/owner) role
        EXECUTE format('CREATE SCHEMA IF NOT EXISTS %I', module_schema);

        -- Rights on its own schema only
        EXECUTE format('REVOKE ALL ON SCHEMA %I FROM PUBLIC', module_schema);
        EXECUTE format('GRANT USAGE ON SCHEMA %I TO %I', module_schema, module_role);

        -- Existing objects (no-op on a fresh database)
        EXECUTE format(
            'GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA %I TO %I',
            module_schema, module_role);
        EXECUTE format(
            'GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA %I TO %I',
            module_schema, module_role);

        -- Objects created later by the owner (EF Core migrations)
        EXECUTE format(
            'ALTER DEFAULT PRIVILEGES IN SCHEMA %I GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO %I',
            module_schema, module_role);
        EXECUTE format(
            'ALTER DEFAULT PRIVILEGES IN SCHEMA %I GRANT USAGE, SELECT ON SEQUENCES TO %I',
            module_schema, module_role);
    END LOOP;
END
$$;
