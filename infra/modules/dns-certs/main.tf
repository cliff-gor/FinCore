# Module: dns-certs — SKELETON (not implemented in M0)
#
# Purpose: Route 53 records and ACM certificates: regional (ALB) and us-east-1 (CloudFront), DNS-validated (§33.9 rows 2–3).
#
# TODO(aws-spec): implement per the separate AWS deployment spec (spec 001 defers full modules):
#   - Needs an aws.us_east_1 provider alias passed in via configuration_aliases.
#   - Hosted zone itself is created/pointed manually once (§33.12 step 1).
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
