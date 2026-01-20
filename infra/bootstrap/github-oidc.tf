# GitHub Actions → AWS without long-lived keys (master spec §33.3, §33.6, §33.11).
# One deploy role per GitHub environment; only workflows running in that environment of
# this repository can assume it.

resource "aws_iam_openid_connect_provider" "github" {
  url            = "https://token.actions.githubusercontent.com"
  client_id_list = ["sts.amazonaws.com"]
}

data "aws_iam_policy_document" "github_trust" {
  for_each = toset(var.environments)

  statement {
    actions = ["sts:AssumeRoleWithWebIdentity"]

    principals {
      type        = "Federated"
      identifiers = [aws_iam_openid_connect_provider.github.arn]
    }

    condition {
      test     = "StringEquals"
      variable = "token.actions.githubusercontent.com:aud"
      values   = ["sts.amazonaws.com"]
    }

    condition {
      test     = "StringLike"
      variable = "token.actions.githubusercontent.com:sub"
      values   = ["repo:${var.github_owner}/${var.github_repository}:environment:${each.key}"]
    }
  }
}

resource "aws_iam_role" "github_deploy" {
  for_each = toset(var.environments)

  name                 = "fincore-github-deploy-${each.key}"
  description          = "Assumed by GitHub Actions (environment ${each.key}) to deploy FinCore"
  assume_role_policy   = data.aws_iam_policy_document.github_trust[each.key].json
  max_session_duration = 3600
}

# Remote state access for this environment's key only (S3-native locking uses a .tflock object).
data "aws_iam_policy_document" "state_access" {
  for_each = toset(var.environments)

  statement {
    sid       = "ListStateBucket"
    actions   = ["s3:ListBucket"]
    resources = [aws_s3_bucket.tfstate.arn]

    condition {
      test     = "StringLike"
      variable = "s3:prefix"
      values   = ["${each.key}/*"]
    }
  }

  statement {
    sid     = "ReadWriteEnvironmentState"
    actions = ["s3:GetObject", "s3:PutObject", "s3:DeleteObject"]
    resources = [
      "${aws_s3_bucket.tfstate.arn}/${each.key}/terraform.tfstate",
      "${aws_s3_bucket.tfstate.arn}/${each.key}/terraform.tfstate.tflock",
    ]
  }
}

resource "aws_iam_role_policy" "state_access" {
  for_each = toset(var.environments)

  name   = "terraform-state"
  role   = aws_iam_role.github_deploy[each.key].id
  policy = data.aws_iam_policy_document.state_access[each.key].json
}

# TODO(aws-spec): attach the least-privilege deploy permissions from §33.11 — ECR push,
# ECS deploy/RunTask, iam:PassRole for the task roles only, S3 sync to the site/artifacts
# buckets, CloudFront invalidation, SSM read — plus the permissions `terraform apply` needs
# for envs/<env>. Defined by the separate AWS deployment spec; deliberately not granted here.
