output "implemented" {
  description = "false while this module is a skeleton."
  value       = false
}

output "settings" {
  description = "Inputs received (placeholder output until the module is implemented)."
  value = {
    name_prefix              = var.name_prefix
    enabled                  = var.enabled
    rate_limit_per_5_minutes = var.rate_limit_per_5_minutes
    tags                     = var.tags
  }
}
