output "implemented" {
  description = "false while this module is a skeleton."
  value       = false
}

output "settings" {
  description = "Inputs received (placeholder output until the module is implemented)."
  value = {
    name_prefix = var.name_prefix
    domain_name = var.domain_name
    subdomains  = var.subdomains
    tags        = var.tags
  }
}
