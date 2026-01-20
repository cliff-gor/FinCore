variable "name_prefix" {
  description = "Prefix for resource names, e.g. fincore-staging."
  type        = string
}

variable "enabled" {
  description = "Create the web ACL."
  type        = bool
  default     = false
}

variable "rate_limit_per_5_minutes" {
  description = "Requests per IP per 5 minutes before blocking."
  type        = number
  default     = 2000
}

variable "tags" {
  description = "Tags added to every resource."
  type        = map(string)
  default     = {}
}
