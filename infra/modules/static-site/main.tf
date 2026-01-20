# Module: static-site — SKELETON (not implemented in M0)
#
# Purpose: S3 + CloudFront for one SPA (Back Office or Portal) with Origin Access Control, security headers policy and /api/*, /bff/* behaviors to the ALB (§33.2, §33.3, §33.9 row 12).
#
# TODO(aws-spec): implement per the separate AWS deployment spec (spec 001 defers full modules):
#   - Private S3 bucket (Block Public Access), CloudFront OAC, SPA fallback to index.html.
#   - Behaviors /api/* and /bff/* → ALB origin so cookies stay same-origin.
#   - Outputs bucket name + distribution id (WEB_BUCKET_*, CLOUDFRONT_ID_* GitHub variables).
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
