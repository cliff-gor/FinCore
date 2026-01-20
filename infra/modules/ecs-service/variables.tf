variable "name_prefix" {
  description = "Prefix for resource names, e.g. fincore-staging."
  type        = string
}

variable "name" {
  description = "Component name: api, gateway or migrator. Also the container and Service Connect name."
  type        = string
}

variable "cluster_arn" {
  description = "ECS cluster ARN."
  type        = string
}

variable "container_image" {
  description = "Initial image (repo:tag). Later images are deployed by the pipeline, not Terraform."
  type        = string
}

variable "command" {
  description = "Optional container command override."
  type        = list(string)
  default     = null
}

variable "container_port" {
  description = "Container port (null for tasks without a listener, e.g. the migrator)."
  type        = number
  default     = 8080
}

variable "cpu" {
  description = "Task CPU units (256 = 0.25 vCPU, 512 = 0.5 vCPU)."
  type        = number
  default     = 512
}

variable "memory" {
  description = "Task memory in MiB."
  type        = number
  default     = 1024
}

variable "environment_variables" {
  description = "Plain (non-secret) environment variables."
  type        = map(string)
  default     = {}
}

variable "secrets" {
  description = "Environment variable name => SSM parameter / Secrets Manager ARN, injected at start."
  type        = map(string)
  default     = {}
}

variable "readonly_root_filesystem" {
  description = "Run the container with a read-only root filesystem."
  type        = bool
  default     = true
}

variable "log_retention_days" {
  description = "CloudWatch Logs retention (14–30 days, §33.2)."
  type        = number
  default     = 14
}

variable "task_role_policy_json" {
  description = "Inline IAM policy (JSON) for the task role; null = no app permissions."
  type        = string
  default     = null
}

variable "create_service" {
  description = "Create an ECS service. false = task definition only (one-off tasks such as the migrator)."
  type        = bool
  default     = true
}

variable "desired_count" {
  description = "Initial desired count (afterwards owned by autoscaling / the pipeline / the scheduler)."
  type        = number
  default     = 1
}

variable "subnet_ids" {
  description = "Subnets for the service tasks."
  type        = list(string)
  default     = []
}

variable "security_group_ids" {
  description = "Security groups for the service tasks."
  type        = list(string)
  default     = []
}

variable "assign_public_ip" {
  description = "Give tasks a public IP (public subnets without NAT, §33.3)."
  type        = bool
  default     = true
}

variable "use_fargate_spot" {
  description = "Run on FARGATE_SPOT (Staging cost saving, §33.4)."
  type        = bool
  default     = false
}

variable "enable_execute_command" {
  description = "Allow ECS Exec into running tasks."
  type        = bool
  default     = false
}

variable "target_group_arn" {
  description = "ALB target group to register with; null = not behind the ALB."
  type        = string
  default     = null
}

variable "health_check_grace_period_seconds" {
  description = "Grace period before ALB health checks count (only with a target group)."
  type        = number
  default     = 60
}

variable "service_connect_namespace_arn" {
  description = "Service Connect namespace ARN; null disables Service Connect."
  type        = string
  default     = null
}

variable "service_connect_publish" {
  description = "Publish this service in Service Connect (api: true; gateway: client only)."
  type        = bool
  default     = true
}

variable "autoscaling" {
  description = "Target-tracking autoscaling on CPU; null disables it."
  type = object({
    min_capacity       = number
    max_capacity       = number
    cpu_target_percent = number
  })
  default = null
}

variable "tags" {
  description = "Tags added to every resource."
  type        = map(string)
  default     = {}
}
