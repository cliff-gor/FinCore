output "implemented" {
  description = "false while this module is a skeleton."
  value       = false
}

output "settings" {
  description = "Inputs received (placeholder output until the module is implemented)."
  value = {
    name_prefix        = var.name_prefix
    alert_email        = var.alert_email
    monthly_budget_usd = var.monthly_budget_usd
    tags               = var.tags
  }
}
