# ECS cluster + Service Connect namespace (master spec §33.6, §33.9 row 7).
# Minimal implementation so ecs-service has a cluster to target; capacity providers
# FARGATE and FARGATE_SPOT are both available (Spot for Staging, §33.4).
# TODO(aws-spec): finalise Container Insights level and execute-command logging.

terraform {
  required_version = ">= 1.10"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
  }
}

resource "aws_service_discovery_http_namespace" "this" {
  name        = var.service_connect_namespace
  description = "Service Connect namespace for ${var.name_prefix}"
  tags        = var.tags
}

resource "aws_ecs_cluster" "this" {
  name = var.name_prefix

  setting {
    name  = "containerInsights"
    value = var.container_insights ? "enabled" : "disabled"
  }

  service_connect_defaults {
    namespace = aws_service_discovery_http_namespace.this.arn
  }

  tags = var.tags
}

resource "aws_ecs_cluster_capacity_providers" "this" {
  cluster_name       = aws_ecs_cluster.this.name
  capacity_providers = ["FARGATE", "FARGATE_SPOT"]

  default_capacity_provider_strategy {
    capacity_provider = var.use_fargate_spot ? "FARGATE_SPOT" : "FARGATE"
    weight            = 1
  }
}
