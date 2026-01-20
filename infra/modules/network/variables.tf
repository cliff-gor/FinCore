variable "name_prefix" {
  description = "Prefix for resource names, e.g. fincore-staging."
  type        = string
}

variable "vpc_cidr" {
  description = "VPC CIDR block (/16 recommended; subnets are /24)."
  type        = string
  default     = "10.20.0.0/16"
}

variable "az_count" {
  description = "Number of availability zones (2 per §33.9)."
  type        = number
  default     = 2

  validation {
    condition     = var.az_count >= 2 && var.az_count <= 3
    error_message = "az_count must be 2 or 3 (ALB and RDS subnet groups need at least 2)."
  }
}

variable "enable_nat_gateway" {
  description = "Create a NAT gateway for private subnets (off by default for cost, §33.3)."
  type        = bool
  default     = false
}

variable "app_port" {
  description = "Container port of the api and gateway (ASP.NET Core default 8080)."
  type        = number
  default     = 8080
}

variable "tags" {
  description = "Tags added to every resource."
  type        = map(string)
  default     = {}
}
