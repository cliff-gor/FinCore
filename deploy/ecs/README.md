# ECS task-definition templates (placeholder)

This folder will hold the ECS task-definition templates used by the deployment pipeline
(master spec §33.6, §33.8, §33.9 rows 8–10):

- `api.json` — FinCore.Api service (Fargate ARM64, 0.5 vCPU / 1 GB in Staging)
- `gateway.json` — FinCore.Bff in BackOffice / Portal / Mobile modes (0.25 vCPU / 0.5 GB)
- `migrator.json` — one-off FinCore.Migrator task run before each deployment

Terraform (`infra/modules/ecs-service`) creates each service with an initial task definition and then
ignores later revisions; the CD workflows render these templates with the new image tag and register
new revisions.

**Not used in M0.** The templates, their placeholders and the render step are defined by the separate
AWS deployment spec (see `docs/specs/001-m0-foundations.md`, "Deferred Decisions").
