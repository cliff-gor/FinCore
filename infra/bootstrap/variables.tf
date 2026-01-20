variable "aws_region" {
  description = "AWS region for the state bucket and IAM resources."
  type        = string
  default     = "eu-west-1"
}

variable "state_bucket_name" {
  description = "Name of the Terraform state bucket. Null = fincore-tfstate-<account-id>."
  type        = string
  default     = null
}

variable "state_noncurrent_version_days" {
  description = "Days to keep non-current versions of state objects."
  type        = number
  default     = 90
}

variable "github_owner" {
  description = "GitHub user or organization that owns the FinCore repository."
  type        = string
}

variable "github_repository" {
  description = "GitHub repository name."
  type        = string
  default     = "FinCore"
}

variable "environments" {
  description = "GitHub environments that get their own deploy role."
  type        = list(string)
  default     = ["staging", "production"]

  validation {
    condition     = length(var.environments) > 0
    error_message = "At least one environment is required."
  }
}
