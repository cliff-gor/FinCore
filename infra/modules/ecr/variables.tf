variable "repository_names" {
  description = "ECR repositories to create."
  type        = list(string)
  default     = ["fincore-api", "fincore-gateway", "fincore-migrator"]
}

variable "keep_last_images" {
  description = "Number of images to keep per repository (§33.3: last 10)."
  type        = number
  default     = 10
}

variable "tags" {
  description = "Tags added to every resource."
  type        = map(string)
  default     = {}
}
