# Module: observability — SKELETON (not implemented in M0)
#
# Purpose: CloudWatch alarms (5xx, CPU/memory, RDS storage/CPU), SNS alert topic, AWS Budget at 50/80/100 % (§33.2, §33.4, §33.9 row 18).
#
# TODO(aws-spec): implement per the separate AWS deployment spec (spec 001 defers full modules):
#   - Email subscription must be confirmed manually.
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
