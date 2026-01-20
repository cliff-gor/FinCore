variable "name_prefix" {
  description = "Prefix for resource names, e.g. fincore-staging."
  type        = string
}

variable "backoffice_callback_urls" {
  description = "Back Office BFF callback URLs."
  type        = list(string)
  default     = []
}

variable "portal_callback_urls" {
  description = "Portal BFF callback URLs."
  type        = list(string)
  default     = []
}

variable "mobile_callback_urls" {
  description = "Mobile app redirect URIs."
  type        = list(string)
  default     = ["fincore://auth/callback"]
}

variable "tags" {
  description = "Tags added to every resource."
  type        = map(string)
  default     = {}
}
