output "implemented" {
  description = "false while this module is a skeleton."
  value       = false
}

output "settings" {
  description = "Inputs received (placeholder output until the module is implemented)."
  value = {
    name_prefix            = var.name_prefix
    environment            = var.environment
    enabled_modules        = var.enabled_modules
    minimum_mobile_version = var.minimum_mobile_version
    tags                   = var.tags
  }
}
