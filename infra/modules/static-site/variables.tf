variable "name_prefix" {
  description = "Prefix for resource names, e.g. fincore-staging."
  type        = string
}

variable "site_name" {
  description = "Site name: backoffice or portal."
  type        = string
}

variable "domain_name" {
  description = "Public host name, e.g. admin.example.com."
  type        = string
  default     = null
}

variable "alb_dns_name" {
  description = "ALB DNS name for the /api and /bff origins."
  type        = string
  default     = null
}

variable "certificate_arn" {
  description = "ACM certificate ARN in us-east-1 (CloudFront)."
  type        = string
  default     = null
}

variable "tags" {
  description = "Tags added to every resource."
  type        = map(string)
  default     = {}
}
