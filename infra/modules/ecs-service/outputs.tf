output "service_name" {
  description = "ECS service name (null for task-only components)."
  value       = var.create_service ? aws_ecs_service.this[0].name : null
}

output "task_definition_family" {
  description = "Task definition family — the pipeline registers new revisions in it."
  value       = aws_ecs_task_definition.this.family
}

output "task_definition_arn" {
  description = "ARN of the initial task definition revision."
  value       = aws_ecs_task_definition.this.arn
}

output "task_role_arn" {
  description = "Task role ARN (the deploy role needs iam:PassRole on it)."
  value       = aws_iam_role.task.arn
}

output "execution_role_arn" {
  description = "Execution role ARN (the deploy role needs iam:PassRole on it)."
  value       = aws_iam_role.execution.arn
}

output "log_group_name" {
  description = "CloudWatch log group."
  value       = aws_cloudwatch_log_group.this.name
}
