variable "name_prefix" {
  description = "Prefix for resource names, e.g. fincore-staging."
  type        = string
}

variable "environment" {
  description = "Environment name (staging, production)."
  type        = string
}

variable "enabled_modules" {
  description = "Backend modules switched on (§33.7.4)."
  type        = list(string)
  default     = ["Organization"]
}

variable "minimum_mobile_version" {
  description = "Minimum supported mobile app version (426 below it)."
  type        = string
  default     = "0.1.0"
}

variable "tags" {
  description = "Tags added to every resource."
  type        = map(string)
  default     = {}
}
