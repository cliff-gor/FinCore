variable "name_prefix" {
  description = "Prefix for resource names, e.g. fincore-staging."
  type        = string
}

variable "create_artifacts_bucket" {
  description = "Create the shared web-artifacts bucket in this environment."
  type        = bool
  default     = false
}

variable "document_retention_days" {
  description = "Days before old document versions expire."
  type        = number
  default     = 3650
}

variable "tags" {
  description = "Tags added to every resource."
  type        = map(string)
  default     = {}
}
