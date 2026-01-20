# Staging sizing (master spec §33.4, §33.9). No secrets in tfvars.
environment        = "staging"
dotnet_environment = "Staging"
aws_region         = "eu-west-1"
domain_name        = null # e.g. "staging.fincore.example.com" once a domain exists

vpc_cidr           = "10.20.0.0/16"
enable_nat_gateway = false

# Shared resources live in staging; production reuses them.
create_ecr_repositories = true
create_artifacts_bucket = true

db_instance_class        = "db.t4g.micro"
db_multi_az              = false
db_deletion_protection   = false
db_backup_retention_days = 7

container_insights = false
use_fargate_spot   = true

api_cpu        = 512
api_memory     = 1024
gateway_cpu    = 256
gateway_memory = 512

log_retention_days = 14

enabled_modules        = ["Organization"]
minimum_mobile_version = "0.1.0"

monthly_budget_usd           = 100
enable_out_of_hours_schedule = true
enable_waf                   = false
