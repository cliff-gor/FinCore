variable "name_prefix" {
  description = "Prefix for resource names, e.g. fincore-staging."
  type        = string
}

variable "vpc_id" {
  description = "VPC id."
  type        = string
}

variable "subnet_ids" {
  description = "Public subnets for the ALB."
  type        = list(string)
  default     = []
}

variable "security_group_ids" {
  description = "Security groups (network.alb_security_group_id)."
  type        = list(string)
  default     = []
}

variable "certificate_arn" {
  description = "Regional ACM certificate ARN (dns-certs)."
  type        = string
  default     = null
}

variable "tags" {
  description = "Tags added to every resource."
  type        = map(string)
  default     = {}
}
