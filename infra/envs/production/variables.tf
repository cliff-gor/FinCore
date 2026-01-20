variable "environment" {
  description = "Environment name."
  type        = string
}

variable "dotnet_environment" {
  description = "ASPNETCORE_ENVIRONMENT / DOTNET_ENVIRONMENT value (Staging, Production)."
  type        = string
}

variable "aws_region" {
  description = "AWS region."
  type        = string
  default     = "eu-west-1"
}

variable "domain_name" {
  description = "Base domain (e.g. fincore.example.com); null until a domain exists."
  type        = string
  default     = null
}

variable "vpc_cidr" {
  description = "VPC CIDR block."
  type        = string
}

variable "enable_nat_gateway" {
  description = "NAT gateway for private subnets (cost; off by default)."
  type        = bool
  default     = false
}

variable "create_ecr_repositories" {
  description = "Create the shared ECR repositories in this environment (exactly one environment)."
  type        = bool
}

variable "create_artifacts_bucket" {
  description = "Create the shared web-artifacts bucket in this environment (exactly one environment)."
  type        = bool
}

variable "initial_image_tag" {
  description = "Image tag for the initial task definitions; later deployments are done by the pipeline."
  type        = string
  default     = "bootstrap"
}

variable "db_instance_class" {
  description = "RDS instance class."
  type        = string
}

variable "db_multi_az" {
  description = "RDS Multi-AZ."
  type        = bool
  default     = false
}

variable "db_deletion_protection" {
  description = "RDS deletion protection."
  type        = bool
}

variable "db_backup_retention_days" {
  description = "RDS automated backup retention (days)."
  type        = number
}

variable "container_insights" {
  description = "Enable Container Insights."
  type        = bool
  default     = false
}

variable "use_fargate_spot" {
  description = "Run services on Fargate Spot."
  type        = bool
  default     = false
}

variable "api_cpu" {
  description = "api task CPU units."
  type        = number
}

variable "api_memory" {
  description = "api task memory (MiB)."
  type        = number
}

variable "gateway_cpu" {
  description = "gateway task CPU units."
  type        = number
}

variable "gateway_memory" {
  description = "gateway task memory (MiB)."
  type        = number
}

variable "api_autoscaling" {
  description = "api autoscaling; null = fixed single task."
  type = object({
    min_capacity       = number
    max_capacity       = number
    cpu_target_percent = number
  })
  default = null
}

variable "gateway_autoscaling" {
  description = "gateway autoscaling; null = fixed single task."
  type = object({
    min_capacity       = number
    max_capacity       = number
    cpu_target_percent = number
  })
  default = null
}

variable "log_retention_days" {
  description = "CloudWatch Logs retention (days)."
  type        = number
}

variable "enabled_modules" {
  description = "Backend modules switched on in this environment (§33.7.4)."
  type        = list(string)
}

variable "minimum_mobile_version" {
  description = "Minimum supported mobile app version."
  type        = string
  default     = "0.1.0"
}

variable "alert_email" {
  description = "E-mail for alarms and budget alerts."
  type        = string
  default     = null
}

variable "monthly_budget_usd" {
  description = "AWS Budget (USD / month)."
  type        = number
}

variable "enable_out_of_hours_schedule" {
  description = "Scale to zero / stop RDS out of hours."
  type        = bool
  default     = false
}

variable "enable_waf" {
  description = "Create the AWS WAF web ACL."
  type        = bool
  default     = false
}
