variable "name_prefix" {
  description = "Prefix for resource names, e.g. fincore-staging."
  type        = string
}

variable "domain_name" {
  description = "Apex domain, e.g. fincore.example.com."
  type        = string
  default     = null
}

variable "subdomains" {
  description = "Host names that need records and certificates."
  type        = list(string)
  default     = ["admin", "app", "api"]
}

variable "tags" {
  description = "Tags added to every resource."
  type        = map(string)
  default     = {}
}
