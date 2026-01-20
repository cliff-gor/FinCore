output "implemented" {
  description = "false while this module is a skeleton."
  value       = false
}

output "settings" {
  description = "Inputs received (placeholder output until the module is implemented)."
  value = {
    name_prefix           = var.name_prefix
    instance_class        = var.instance_class
    allocated_storage_gb  = var.allocated_storage_gb
    multi_az              = var.multi_az
    deletion_protection   = var.deletion_protection
    backup_retention_days = var.backup_retention_days
    subnet_ids            = var.subnet_ids
    security_group_ids    = var.security_group_ids
    tags                  = var.tags
  }
}
