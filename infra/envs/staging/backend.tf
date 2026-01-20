# Remote state (master spec §33.6). Terraform 1.10+ S3-native locking: no DynamoDB table.
#
# `bucket` is the bootstrap output `state_bucket_name` (fincore-tfstate-<account-id>).
# Replace the placeholder, or pass it at init time:
#   terraform init -backend-config="bucket=fincore-tfstate-123456789012"
# CI validates with `terraform init -backend=false`, so this file is never contacted in M0.

terraform {
  required_version = ">= 1.10"

  backend "s3" {
    bucket       = "fincore-tfstate-ACCOUNT_ID"
    key          = "staging/terraform.tfstate"
    region       = "eu-west-1"
    encrypt      = true
    use_lockfile = true
  }

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
  }
}
