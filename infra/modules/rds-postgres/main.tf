# Module: rds-postgres — SKELETON (not implemented in M0)
#
# Purpose: RDS for PostgreSQL: instance, subnet group, parameter group, RDS-managed master secret (§33.3, §33.9 row 6, §33.11).
#
# TODO(aws-spec): implement per the separate AWS deployment spec (spec 001 defers full modules):
#   - db.t4g.micro (staging) / db.t4g.small (production), gp3 20 GB, single-AZ to start.
#   - Encryption at rest, automated backups (7 days staging / 14+ production), deletion protection in production.
#   - manage_master_user_password = true; separate DDL (migrator) and DML (api) roles per §33.10 / deploy/postgres/init-schemas.sql.
#
# Until then this module creates no resources; its outputs echo the inputs so the
# environment compositions in envs/* can already be wired and validated.

terraform {
  required_version = ">= 1.10"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
  }
}
