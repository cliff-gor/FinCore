variable "name_prefix" {
  description = "Prefix for resource names, e.g. fincore-staging."
  type        = string
}

variable "alert_email" {
  description = "E-mail address for alarms and budget alerts."
  type        = string
  default     = null
}

variable "monthly_budget_usd" {
  description = "Monthly AWS Budget limit in USD."
  type        = number
  default     = 100
}

variable "tags" {
  description = "Tags added to every resource."
  type        = map(string)
  default     = {}
}
