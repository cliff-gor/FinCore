output "implemented" {
  description = "false while this module is a skeleton."
  value       = false
}

output "settings" {
  description = "Inputs received (placeholder output until the module is implemented)."
  value = {
    name_prefix             = var.name_prefix
    create_artifacts_bucket = var.create_artifacts_bucket
    document_retention_days = var.document_retention_days
    tags                    = var.tags
  }
}
