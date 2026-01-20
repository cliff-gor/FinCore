output "vpc_id" {
  description = "VPC id."
  value       = module.network.vpc_id
}

output "ecs_cluster_name" {
  description = "ECS_CLUSTER GitHub environment variable."
  value       = module.ecs_cluster.cluster_name
}

output "ecr_repository_urls" {
  description = "ECR repositories (only in the environment that owns them)."
  value       = var.create_ecr_repositories ? module.ecr[0].repository_urls : {}
}

output "task_definition_families" {
  description = "Task definition families the pipeline registers new revisions in."
  value = {
    api      = module.api.task_definition_family
    gateway  = module.gateway.task_definition_family
    migrator = module.migrator.task_definition_family
  }
}
