# FinCore — environment composition. NOTHING has been applied (spec 001: AWS is out of scope for M0).
# Install order (§33.9) follows from the module dependencies below.
# TODO(aws-spec): wire alb target groups, static sites, cognito, params into the services once those
# modules are implemented.

provider "aws" {
  region = var.aws_region

  default_tags {
    tags = local.tags
  }
}

locals {
  name_prefix = "fincore-${var.environment}"

  tags = {
    Project     = "FinCore"
    Environment = var.environment
    ManagedBy   = "terraform"
  }

  ecr_registry = "${data.aws_caller_identity.current.account_id}.dkr.ecr.${var.aws_region}.amazonaws.com"
}

data "aws_caller_identity" "current" {}

# 4. Network -------------------------------------------------------------------
module "network" {
  source = "../../modules/network"

  name_prefix        = local.name_prefix
  vpc_cidr           = var.vpc_cidr
  enable_nat_gateway = var.enable_nat_gateway
  tags               = local.tags
}

# 5. ECR (shared between environments; created in one environment only) -------
module "ecr" {
  source = "../../modules/ecr"
  count  = var.create_ecr_repositories ? 1 : 0

  tags = local.tags
}

# 2–3. DNS + certificates --------------------------------------------------------
module "dns_certs" {
  source = "../../modules/dns-certs"

  name_prefix = local.name_prefix
  domain_name = var.domain_name
  tags        = local.tags
}

# 6. Database --------------------------------------------------------------------
module "rds" {
  source = "../../modules/rds-postgres"

  name_prefix           = local.name_prefix
  instance_class        = var.db_instance_class
  multi_az              = var.db_multi_az
  deletion_protection   = var.db_deletion_protection
  backup_retention_days = var.db_backup_retention_days
  subnet_ids            = module.network.private_subnet_ids
  security_group_ids    = [module.network.rds_security_group_id]
  tags                  = local.tags
}

# 7. ECS cluster -----------------------------------------------------------------
module "ecs_cluster" {
  source = "../../modules/ecs-cluster"

  name_prefix               = local.name_prefix
  service_connect_namespace = "${local.name_prefix}.local"
  container_insights        = var.container_insights
  use_fargate_spot          = var.use_fargate_spot
  tags                      = local.tags
}

# 13–14. Storage -----------------------------------------------------------------
module "storage" {
  source = "../../modules/storage"

  name_prefix             = local.name_prefix
  create_artifacts_bucket = var.create_artifacts_bucket
  tags                    = local.tags
}

# 15–17. Identity, email, parameters --------------------------------------------
module "cognito" {
  source = "../../modules/cognito"

  name_prefix              = local.name_prefix
  backoffice_callback_urls = var.domain_name == null ? [] : ["https://admin.${var.domain_name}/bff/signin-oidc"]
  portal_callback_urls     = var.domain_name == null ? [] : ["https://app.${var.domain_name}/bff/signin-oidc"]
  tags                     = local.tags
}

module "ses" {
  source = "../../modules/ses"

  name_prefix = local.name_prefix
  domain_name = var.domain_name
  tags        = local.tags
}

module "params" {
  source = "../../modules/params"

  name_prefix            = local.name_prefix
  environment            = var.environment
  enabled_modules        = var.enabled_modules
  minimum_mobile_version = var.minimum_mobile_version
  tags                   = local.tags
}

# 11–12. Load balancer and web apps ----------------------------------------------
module "alb" {
  source = "../../modules/alb"

  name_prefix        = local.name_prefix
  vpc_id             = module.network.vpc_id
  subnet_ids         = module.network.public_subnet_ids
  security_group_ids = [module.network.alb_security_group_id]
  tags               = local.tags
}

module "site_backoffice" {
  source = "../../modules/static-site"

  name_prefix = local.name_prefix
  site_name   = "backoffice"
  domain_name = var.domain_name == null ? null : "admin.${var.domain_name}"
  tags        = local.tags
}

module "site_portal" {
  source = "../../modules/static-site"

  name_prefix = local.name_prefix
  site_name   = "portal"
  domain_name = var.domain_name == null ? null : "app.${var.domain_name}"
  tags        = local.tags
}

# 8–10. Services ---------------------------------------------------------------------
# Tasks run in public subnets with public IPs instead of behind a NAT gateway, which would cost more than
# the whole compute layer; their security groups still only admit the ALB and the gateway (spec §33.3).
# `initial_image_tag` is used for the first deploy only; CD registers new task definitions afterwards.
module "api" {
  source = "../../modules/ecs-service"

  name_prefix                   = local.name_prefix
  name                          = "api"
  cluster_arn                   = module.ecs_cluster.cluster_arn
  container_image               = "${local.ecr_registry}/fincore-api:${var.initial_image_tag}"
  cpu                           = var.api_cpu
  memory                        = var.api_memory
  subnet_ids                    = module.network.public_subnet_ids
  security_group_ids            = [module.network.api_security_group_id]
  use_fargate_spot              = var.use_fargate_spot
  service_connect_namespace_arn = module.ecs_cluster.service_connect_namespace_arn
  autoscaling                   = var.api_autoscaling
  log_retention_days            = var.log_retention_days
  environment_variables = {
    ASPNETCORE_ENVIRONMENT = var.dotnet_environment
  }
  tags = local.tags
}

# The gateway is a Service Connect client only: it calls the API, nothing calls it internally.
module "gateway" {
  source = "../../modules/ecs-service"

  name_prefix                   = local.name_prefix
  name                          = "gateway"
  cluster_arn                   = module.ecs_cluster.cluster_arn
  container_image               = "${local.ecr_registry}/fincore-gateway:${var.initial_image_tag}"
  cpu                           = var.gateway_cpu
  memory                        = var.gateway_memory
  subnet_ids                    = module.network.public_subnet_ids
  security_group_ids            = [module.network.gateway_security_group_id]
  use_fargate_spot              = var.use_fargate_spot
  service_connect_namespace_arn = module.ecs_cluster.service_connect_namespace_arn
  service_connect_publish       = false
  autoscaling                   = var.gateway_autoscaling
  log_retention_days            = var.log_retention_days
  environment_variables = {
    ASPNETCORE_ENVIRONMENT = var.dotnet_environment
  }
  tags = local.tags
}

# Task definition only, run as a one-off task by the deploy workflow before the services roll (spec §33.10).
module "migrator" {
  source = "../../modules/ecs-service"

  name_prefix        = local.name_prefix
  name               = "migrator"
  create_service     = false
  cluster_arn        = module.ecs_cluster.cluster_arn
  container_image    = "${local.ecr_registry}/fincore-migrator:${var.initial_image_tag}"
  container_port     = null
  cpu                = 256
  memory             = 512
  log_retention_days = var.log_retention_days
  environment_variables = {
    DOTNET_ENVIRONMENT = var.dotnet_environment
  }
  tags = local.tags
}

# 18–20. Operations ---------------------------------------------------------------
module "observability" {
  source = "../../modules/observability"

  name_prefix        = local.name_prefix
  alert_email        = var.alert_email
  monthly_budget_usd = var.monthly_budget_usd
  tags               = local.tags
}

module "scheduler" {
  source = "../../modules/scheduler"

  name_prefix = local.name_prefix
  enabled     = var.enable_out_of_hours_schedule
  tags        = local.tags
}

module "waf" {
  source = "../../modules/waf"

  name_prefix = local.name_prefix
  enabled     = var.enable_waf
  tags        = local.tags
}
