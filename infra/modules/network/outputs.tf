output "vpc_id" {
  description = "VPC id."
  value       = aws_vpc.this.id
}

output "public_subnet_ids" {
  description = "Public subnet ids (ALB, Fargate tasks)."
  value       = aws_subnet.public[*].id
}

output "private_subnet_ids" {
  description = "Private subnet ids (RDS)."
  value       = aws_subnet.private[*].id
}

output "alb_security_group_id" {
  description = "Security group of the ALB."
  value       = aws_security_group.alb.id
}

output "gateway_security_group_id" {
  description = "Security group of the gateway service."
  value       = aws_security_group.gateway.id
}

output "api_security_group_id" {
  description = "Security group of the api service and migrator task."
  value       = aws_security_group.api.id
}

output "rds_security_group_id" {
  description = "Security group of the RDS instance."
  value       = aws_security_group.rds.id
}
