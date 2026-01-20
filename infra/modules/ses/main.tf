# Module: ses — SKELETON (not implemented in M0)
#
# Purpose: Amazon SES: domain identity, DKIM records, MAIL FROM (§33.3, §33.9 row 16).
#
# TODO(aws-spec): implement per the separate AWS deployment spec (spec 001 defers full modules):
#   - Requesting SES production access is a manual one-time step.
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
