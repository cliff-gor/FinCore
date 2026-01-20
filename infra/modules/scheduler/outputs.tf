output "implemented" {
  description = "false while this module is a skeleton."
  value       = false
}

output "settings" {
  description = "Inputs received (placeholder output until the module is implemented)."
  value = {
    name_prefix = var.name_prefix
    enabled     = var.enabled
    timezone    = var.timezone
    start_cron  = var.start_cron
    stop_cron   = var.stop_cron
    tags        = var.tags
  }
}
