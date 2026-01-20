variable "name_prefix" {
  description = "Cluster name, e.g. fincore-staging."
  type        = string
}

variable "service_connect_namespace" {
  description = "Cloud Map HTTP namespace used by Service Connect (gateway → api)."
  type        = string
}

variable "container_insights" {
  description = "Enable CloudWatch Container Insights (adds cost; optional per §33.2)."
  type        = bool
  default     = false
}

variable "use_fargate_spot" {
  description = "Default capacity provider FARGATE_SPOT (Staging) instead of FARGATE."
  type        = bool
  default     = false
}

variable "tags" {
  description = "Tags added to every resource."
  type        = map(string)
  default     = {}
}
