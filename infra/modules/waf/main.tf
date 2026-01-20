# Module: waf — SKELETON (not implemented in M0)
#
# Purpose: AWS WAF web ACL with AWS managed rule groups and rate-based rules on CloudFront/ALB (§33.9 row 20, §33.11). Optional; recommended in production before real users.
#
# TODO(aws-spec): implement per the separate AWS deployment spec (spec 001 defers full modules):
#   - CloudFront-scoped ACLs must be created in us-east-1.
#
# Until then this module creates no resources; its outputs echo the inputs so the
# environment compositions in envs/* can already be wired and validated.

terraform {
  required_version = ">= 1.10"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
  }
}
