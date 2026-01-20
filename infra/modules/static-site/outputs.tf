output "implemented" {
  description = "false while this module is a skeleton."
  value       = false
}

output "settings" {
  description = "Inputs received (placeholder output until the module is implemented)."
  value = {
    name_prefix     = var.name_prefix
    site_name       = var.site_name
    domain_name     = var.domain_name
    alb_dns_name    = var.alb_dns_name
    certificate_arn = var.certificate_arn
    tags            = var.tags
  }
}
