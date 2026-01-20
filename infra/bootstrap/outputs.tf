output "state_bucket_name" {
  description = "S3 bucket for envs/*/backend.tf (bucket = ...)."
  value       = aws_s3_bucket.tfstate.bucket
}

output "github_oidc_provider_arn" {
  description = "ARN of the GitHub Actions OIDC provider."
  value       = aws_iam_openid_connect_provider.github.arn
}

output "github_deploy_role_arns" {
  description = "Deploy role ARN per environment — set as AWS_ROLE_ARN in each GitHub environment (§33.8.5)."
  value       = { for env, role in aws_iam_role.github_deploy : env => role.arn }
}
