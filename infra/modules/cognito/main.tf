# Module: cognito — SKELETON (not implemented in M0)
#
# Purpose: Amazon Cognito: staff user pool + customer user pool, app clients backoffice, portal (confidential) and mobile (public, PKCE) (§33.3, §33.5, §33.9 row 15).
#
# TODO(aws-spec): implement per the separate AWS deployment spec (spec 001 defers full modules):
#   - Mirror the local Keycloak realm (deploy/keycloak/realm-export.json): same client roles and audience.
#   - Mobile: refresh-token rotation, short access-token lifetime (§31.4).
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
