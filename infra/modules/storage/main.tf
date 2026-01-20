# Module: storage — SKELETON (not implemented in M0)
#
# Purpose: S3 buckets: documents (KYC, statements; private, SSE, versioned, presigned URLs only) and the shared, versioned artifacts bucket for web builds used for rollback (§33.3, §33.9 rows 13–14).
#
# TODO(aws-spec): implement per the separate AWS deployment spec (spec 001 defers full modules):
#   - Block Public Access on every bucket; lifecycle to Glacier for old statements.
#   - Artifacts bucket shared between environments (create_artifacts_bucket in one env only).
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
