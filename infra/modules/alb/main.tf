# Module: alb — SKELETON (not implemented in M0)
#
# Purpose: Application Load Balancer: HTTPS listener (TLS 1.2+), HTTP→HTTPS redirect, host/path rules, target groups for api and gateway (§33.2, §33.9 row 11).
#
# TODO(aws-spec): implement per the separate AWS deployment spec (spec 001 defers full modules):
#   - Target groups (ip target type) with /health/ready checks; outputs target_group_arns for ecs-service.
#   - Host rules: api.* → gateway (Mobile mode); /api/*, /bff/* from CloudFront → gateway.
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
