# FinCore bootstrap — run ONCE manually with admin credentials (master spec §33.6, §33.12 step 2).
#
#   cd infra/bootstrap && terraform init && terraform apply
#
# Creates the remote-state bucket used by envs/* and the GitHub OIDC deploy roles
# (github-oidc.tf). Its own state stays local (keep terraform.tfstate safe, or migrate it
# into the new bucket afterwards). NOTHING here has been applied in M0.

terraform {
  required_version = ">= 1.10"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
  }
}

provider "aws" {
  region = var.aws_region

  default_tags {
    tags = {
      Project   = "FinCore"
      ManagedBy = "terraform"
      Stack     = "bootstrap"
    }
  }
}

data "aws_caller_identity" "current" {}

locals {
  state_bucket_name = coalesce(var.state_bucket_name, "fincore-tfstate-${data.aws_caller_identity.current.account_id}")
}

resource "aws_s3_bucket" "tfstate" {
  bucket = local.state_bucket_name

  # Losing this bucket loses the state of every environment.
  lifecycle {
    prevent_destroy = true
  }
}

resource "aws_s3_bucket_ownership_controls" "tfstate" {
  bucket = aws_s3_bucket.tfstate.id

  rule {
    # Disables ACLs; access is controlled by IAM and the bucket policy only.
    object_ownership = "BucketOwnerEnforced"
  }
}

resource "aws_s3_bucket_versioning" "tfstate" {
  bucket = aws_s3_bucket.tfstate.id

  # Lets a corrupted or wrongly applied state be rolled back to an earlier version.
  versioning_configuration {
    status = "Enabled"
  }
}

resource "aws_s3_bucket_server_side_encryption_configuration" "tfstate" {
  bucket = aws_s3_bucket.tfstate.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
    bucket_key_enabled = true
  }
}

resource "aws_s3_bucket_public_access_block" "tfstate" {
  bucket = aws_s3_bucket.tfstate.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_lifecycle_configuration" "tfstate" {
  bucket = aws_s3_bucket.tfstate.id

  rule {
    id     = "expire-old-state-versions"
    status = "Enabled"

    # Empty filter = the whole bucket.
    filter {}

    noncurrent_version_expiration {
      noncurrent_days = var.state_noncurrent_version_days
    }
  }
}

data "aws_iam_policy_document" "tfstate_tls_only" {
  statement {
    sid     = "DenyInsecureTransport"
    effect  = "Deny"
    actions = ["s3:*"]
    resources = [
      aws_s3_bucket.tfstate.arn,
      "${aws_s3_bucket.tfstate.arn}/*",
    ]

    principals {
      type        = "*"
      identifiers = ["*"]
    }

    condition {
      test     = "Bool"
      variable = "aws:SecureTransport"
      values   = ["false"]
    }
  }
}

resource "aws_s3_bucket_policy" "tfstate" {
  bucket = aws_s3_bucket.tfstate.id
  policy = data.aws_iam_policy_document.tfstate_tls_only.json

  # S3 can reject a bucket policy written while the public-access block is still being applied.
  depends_on = [aws_s3_bucket_public_access_block.tfstate]
}
