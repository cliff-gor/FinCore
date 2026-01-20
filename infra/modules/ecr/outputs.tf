output "repository_urls" {
  description = "Repository URL by name."
  value       = { for name, repo in aws_ecr_repository.this : name => repo.repository_url }
}

output "repository_arns" {
  description = "Repository ARN by name."
  value       = { for name, repo in aws_ecr_repository.this : name => repo.arn }
}

output "registry_id" {
  description = "Registry (account) id — the ECR_REGISTRY GitHub variable is <id>.dkr.ecr.<region>.amazonaws.com."
  value       = one(distinct([for repo in aws_ecr_repository.this : repo.registry_id]))
}
