variable "name_prefix" {
  description = "Prefix for resource names, e.g. fincore-staging."
  type        = string
}

variable "enabled" {
  description = "Create the schedules."
  type        = bool
  default     = false
}

variable "timezone" {
  description = "Schedule time zone."
  type        = string
  default     = "Africa/Nairobi"
}

variable "start_cron" {
  description = "Start (scale up) schedule."
  type        = string
  default     = "cron(0 7 ? * MON-FRI *)"
}

variable "stop_cron" {
  description = "Stop (scale to zero) schedule."
  type        = string
  default     = "cron(0 20 ? * MON-FRI *)"
}

variable "tags" {
  description = "Tags added to every resource."
  type        = map(string)
  default     = {}
}
