# ECS service on Fargate ARM64 (master spec §33.3, §33.5, §33.6, §33.9 rows 8–10).
#
# One module instance per component: `api`, `gateway`, and `migrator` (create_service = false:
# task definition only, run as a one-off task by the pipeline before each deployment).
#
# Ownership of the task definition: Terraform creates the service with an INITIAL task
# definition and then ignores later revisions (and desired_count), so the deployment
# pipeline can roll out new images without Terraform fighting it.

terraform {
  required_version = ">= 1.10"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
  }
}

data "aws_region" "current" {}

locals {
  full_name = "${var.name_prefix}-${var.name}"

  container_environment = [
    for k, v in merge(
      {
        DOTNET_GCHeapHardLimitPercent = "0x4B" # 75 % (hex), safety margin for small tasks (§33.5)
        ASPNETCORE_HTTP_PORTS         = tostring(var.container_port)
      },
      var.environment_variables
    ) : { name = k, value = v }
  ]

  container_secrets = [for k, arn in var.secrets : { name = k, valueFrom = arn }]

  port_mappings = var.container_port == null ? [] : [
    {
      name          = var.name
      containerPort = var.container_port
      protocol      = "tcp"
      appProtocol   = "http"
    }
  ]
}

# ---------------------------------------------------------------------------
# Logs
# ---------------------------------------------------------------------------

resource "aws_cloudwatch_log_group" "this" {
  name              = "/ecs/${local.full_name}"
  retention_in_days = var.log_retention_days
  tags              = var.tags
}

# ---------------------------------------------------------------------------
# IAM: execution role (pull image, write logs, read secrets) and task role (app permissions)
# ---------------------------------------------------------------------------

data "aws_iam_policy_document" "ecs_tasks_assume" {
  statement {
    actions = ["sts:AssumeRole"]

    principals {
      type        = "Service"
      identifiers = ["ecs-tasks.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "execution" {
  name               = "${local.full_name}-exec"
  assume_role_policy = data.aws_iam_policy_document.ecs_tasks_assume.json
  tags               = var.tags
}

resource "aws_iam_role_policy_attachment" "execution_managed" {
  role       = aws_iam_role.execution.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy"
}

data "aws_iam_policy_document" "execution_secrets" {
  count = length(var.secrets) > 0 ? 1 : 0

  statement {
    sid       = "ReadInjectedSecrets"
    actions   = ["ssm:GetParameters", "secretsmanager:GetSecretValue"]
    resources = distinct(values(var.secrets))
  }
}

resource "aws_iam_role_policy" "execution_secrets" {
  count = length(var.secrets) > 0 ? 1 : 0

  name   = "read-secrets"
  role   = aws_iam_role.execution.id
  policy = data.aws_iam_policy_document.execution_secrets[0].json
}

resource "aws_iam_role" "task" {
  name               = "${local.full_name}-task"
  assume_role_policy = data.aws_iam_policy_document.ecs_tasks_assume.json
  tags               = var.tags
}

# Least-privilege app permissions are passed in per service (§33.11):
# api: S3 documents + SES + SSM read; gateway: SSM read only.
resource "aws_iam_role_policy" "task" {
  count = var.task_role_policy_json == null ? 0 : 1

  name   = "app"
  role   = aws_iam_role.task.id
  policy = var.task_role_policy_json
}

# ---------------------------------------------------------------------------
# Task definition (initial revision; later revisions are registered by the pipeline)
# ---------------------------------------------------------------------------

resource "aws_ecs_task_definition" "this" {
  family                   = local.full_name
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = var.cpu
  memory                   = var.memory
  execution_role_arn       = aws_iam_role.execution.arn
  task_role_arn            = aws_iam_role.task.arn

  runtime_platform {
    cpu_architecture        = "ARM64"
    operating_system_family = "LINUX"
  }

  container_definitions = jsonencode([
    {
      name                   = var.name
      image                  = var.container_image
      essential              = true
      command                = var.command
      portMappings           = local.port_mappings
      environment            = local.container_environment
      secrets                = local.container_secrets
      readonlyRootFilesystem = var.readonly_root_filesystem
      logConfiguration = {
        logDriver = "awslogs"
        options = {
          awslogs-group         = aws_cloudwatch_log_group.this.name
          awslogs-region        = data.aws_region.current.region
          awslogs-stream-prefix = var.name
        }
      }
    }
  ])

  tags = var.tags
}

# ---------------------------------------------------------------------------
# Service (skipped for one-off tasks such as the migrator)
# ---------------------------------------------------------------------------

resource "aws_ecs_service" "this" {
  count = var.create_service ? 1 : 0

  name                              = var.name
  cluster                           = var.cluster_arn
  task_definition                   = aws_ecs_task_definition.this.arn
  desired_count                     = var.desired_count
  enable_execute_command            = var.enable_execute_command
  health_check_grace_period_seconds = var.target_group_arn == null ? null : var.health_check_grace_period_seconds
  propagate_tags                    = "SERVICE"

  capacity_provider_strategy {
    capacity_provider = var.use_fargate_spot ? "FARGATE_SPOT" : "FARGATE"
    weight            = 1
  }

  network_configuration {
    subnets          = var.subnet_ids
    security_groups  = var.security_group_ids
    assign_public_ip = var.assign_public_ip
  }

  deployment_minimum_healthy_percent = 100
  deployment_maximum_percent         = 200

  deployment_circuit_breaker {
    enable   = true
    rollback = true
  }

  dynamic "load_balancer" {
    for_each = var.target_group_arn == null ? [] : [var.target_group_arn]

    content {
      target_group_arn = load_balancer.value
      container_name   = var.name
      container_port   = var.container_port
    }
  }

  dynamic "service_connect_configuration" {
    for_each = var.service_connect_namespace_arn == null ? [] : [var.service_connect_namespace_arn]

    content {
      enabled   = true
      namespace = service_connect_configuration.value

      # Publish this service (e.g. http://api:8080) when it has a port; clients-only otherwise.
      dynamic "service" {
        for_each = var.container_port == null || !var.service_connect_publish ? [] : [var.name]

        content {
          port_name      = service.value
          discovery_name = service.value

          client_alias {
            port     = var.container_port
            dns_name = service.value
          }
        }
      }
    }
  }

  lifecycle {
    ignore_changes = [task_definition, desired_count]
  }

  tags = var.tags
}

# ---------------------------------------------------------------------------
# Autoscaling (production: api 1–3, gateway 1–2; §33.9)
# ---------------------------------------------------------------------------

resource "aws_appautoscaling_target" "this" {
  count = var.create_service && var.autoscaling != null ? 1 : 0

  service_namespace  = "ecs"
  scalable_dimension = "ecs:service:DesiredCount"
  resource_id        = "service/${split("/", var.cluster_arn)[1]}/${aws_ecs_service.this[0].name}"
  min_capacity       = var.autoscaling.min_capacity
  max_capacity       = var.autoscaling.max_capacity
}

resource "aws_appautoscaling_policy" "cpu" {
  count = var.create_service && var.autoscaling != null ? 1 : 0

  name               = "${local.full_name}-cpu"
  policy_type        = "TargetTrackingScaling"
  service_namespace  = aws_appautoscaling_target.this[0].service_namespace
  scalable_dimension = aws_appautoscaling_target.this[0].scalable_dimension
  resource_id        = aws_appautoscaling_target.this[0].resource_id

  target_tracking_scaling_policy_configuration {
    target_value       = var.autoscaling.cpu_target_percent
    scale_in_cooldown  = 300
    scale_out_cooldown = 60

    predefined_metric_specification {
      predefined_metric_type = "ECSServiceAverageCPUUtilization"
    }
  }
}
