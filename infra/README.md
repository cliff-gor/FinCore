# FinCore infrastructure (Terraform)

AWS infrastructure as code for FinCore (master spec §33). **Nothing in this folder has been applied.**
In Milestone M0 it is a validated skeleton only (spec `docs/specs/001-m0-foundations.md`, AC-28);
the real AWS rollout is defined by a separate AWS deployment spec.

## Layout

| Path | Purpose |
|---|---|
| `bootstrap/` | Run **once** manually with admin credentials: S3 state bucket (versioned, encrypted, public access blocked, TLS-only) and the GitHub OIDC provider + one deploy role per GitHub environment. Local state. |
| `modules/network` | VPC over 2 AZs, public + private subnets, optional NAT, security groups (ALB → gateway/api → RDS). **Implemented.** |
| `modules/ecr` | `fincore-api`, `fincore-gateway`, `fincore-migrator` repositories, immutable tags, scan on push, keep last 10. **Implemented.** |
| `modules/ecs-cluster` | Cluster, Service Connect namespace, Fargate/Fargate Spot capacity providers. **Minimal implementation.** |
| `modules/ecs-service` | Fargate **ARM64** task definition, service with circuit-breaker rollback, `ignore_changes = [task_definition, desired_count]`, log group, exec/task roles, optional autoscaling and Service Connect. Also used task-only for the migrator. **Implemented.** |
| `modules/rds-postgres`, `alb`, `static-site`, `cognito`, `storage`, `ses`, `dns-certs`, `params`, `scheduler`, `observability`, `waf` | **Skeletons**: documented inputs and TODOs, no resources yet. |
| `envs/staging`, `envs/production` | Compose the modules. `backend.tf` uses the S3 backend with S3-native locking (`use_lockfile = true`, Terraform ≥ 1.10, no DynamoDB). Sizes and flags in `<env>.tfvars`. |
| `.tflint.hcl` | TFLint with the Terraform recommended preset and the AWS ruleset. |

Versions: Terraform `>= 1.10`, AWS provider `~> 6.0`.

## Validate locally (no AWS credentials needed)

```bash
cd infra
terraform fmt -check -recursive
for d in bootstrap envs/staging envs/production; do
  (cd "$d" && terraform init -backend=false && terraform validate)
done
tflint --init && tflint --recursive --config "$(pwd)/.tflint.hcl"
```

CI runs the same steps (`.github/workflows/ci.yml`, job `infra`).

## First deployment (future — AWS spec)

1. `cd infra/bootstrap && cp terraform.tfvars.example terraform.tfvars` (set `github_owner`), then
   `terraform init && terraform apply` with an admin profile.
2. Put `state_bucket_name` into `envs/*/backend.tf` (or `-backend-config="bucket=..."`), and the
   `github_deploy_role_arns` into the GitHub environments as `AWS_ROLE_ARN` (§33.8.5).
3. Grant the deploy roles their least-privilege permissions (TODO in `bootstrap/github-oidc.tf`, §33.11).
4. Implement the skeleton modules, then let `cd-staging.yml` run `terraform apply` for staging.

Shared resources (ECR repositories, web-artifacts bucket) are created by **staging**
(`create_ecr_repositories = true`, `create_artifacts_bucket = true`) and reused by production.
