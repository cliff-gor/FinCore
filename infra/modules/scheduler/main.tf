# Module: scheduler — SKELETON (not implemented in M0)
#
# Purpose: EventBridge Scheduler: scale Staging ECS services to 0 and stop RDS out of hours (§33.4, §33.9 row 19). Optional.
#
# TODO(aws-spec): implement per the separate AWS deployment spec (spec 001 defers full modules):
#   - Schedules in the owner's time zone; RDS stop/start + ECS UpdateService desiredCount.
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
