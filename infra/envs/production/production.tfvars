# Production sizing (master spec §33.4, §33.9). No secrets in tfvars.
environment        = "production"
dotnet_environment = "Production"
aws_region         = "eu-west-1"
domain_name        = null # e.g. "fincore.example.com" once a domain exists

vpc_cidr           = "10.30.0.0/16"
enable_nat_gateway = false

# ECR repositories and the artifacts bucket are owned by staging (shared).
create_ecr_repositories = false
create_artifacts_bucket = false

db_instance_class        = "db.t4g.small"
db_multi_az              = false
db_deletion_protection   = true
db_backup_retention_days = 14

container_insights = false
use_fargate_spot   = false

api_cpu        = 512
api_memory     = 1024
gateway_cpu    = 256
gateway_memory = 512

api_autoscaling = {
  min_capacity       = 1
  max_capacity       = 3
  cpu_target_percent = 70
}

gateway_autoscaling = {
  min_capacity       = 1
  max_capacity       = 2
  cpu_target_percent = 70
}

log_retention_days = 30

enabled_modules        = ["Organization"]
minimum_mobile_version = "0.1.0"

monthly_budget_usd           = 150
enable_out_of_hours_schedule = false
enable_waf                   = false # recommended before real users (§33.11)
