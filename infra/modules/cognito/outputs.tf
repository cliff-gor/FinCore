output "implemented" {
  description = "false while this module is a skeleton."
  value       = false
}

output "settings" {
  description = "Inputs received (placeholder output until the module is implemented)."
  value = {
    name_prefix              = var.name_prefix
    backoffice_callback_urls = var.backoffice_callback_urls
    portal_callback_urls     = var.portal_callback_urls
    mobile_callback_urls     = var.mobile_callback_urls
    tags                     = var.tags
  }
}
