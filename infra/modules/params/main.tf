# Module: params — SKELETON (not implemented in M0)
#
# Purpose: SSM Parameter Store: non-secret config, module switches /fincore/<env>/Modules/<Name>/Enabled, minimum mobile version, SecureString placeholders (§33.7.4, §33.9 row 17).
#
# TODO(aws-spec): implement per the separate AWS deployment spec (spec 001 defers full modules):
#   - SecureString values are entered once manually; Terraform ignores their value changes.
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
