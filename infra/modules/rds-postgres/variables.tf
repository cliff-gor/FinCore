variable "name_prefix" {
  description = "Prefix for resource names, e.g. fincore-staging."
  type        = string
}

variable "instance_class" {
  description = "RDS instance class."
  type        = string
  default     = "db.t4g.micro"
}

variable "allocated_storage_gb" {
  description = "Allocated gp3 storage in GB."
  type        = number
  default     = 20
}

variable "multi_az" {
  description = "Multi-AZ deployment."
  type        = bool
  default     = false
}

variable "deletion_protection" {
  description = "Deletion protection (required in production)."
  type        = bool
  default     = true
}

variable "backup_retention_days" {
  description = "Automated backup retention in days."
  type        = number
  default     = 7
}

variable "subnet_ids" {
  description = "Private subnets for the DB subnet group."
  type        = list(string)
  default     = []
}

variable "security_group_ids" {
  description = "Security groups (network.rds_security_group_id)."
  type        = list(string)
  default     = []
}

variable "tags" {
  description = "Tags added to every resource."
  type        = map(string)
  default     = {}
}
