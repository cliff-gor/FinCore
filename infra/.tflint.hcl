# TFLint configuration for infra/ (run with: tflint --init && tflint --recursive --config "$(pwd)/.tflint.hcl")

config {
  # Inspect calls to the local modules with their real arguments; there are no registry modules.
  call_module_type = "local"
}

plugin "terraform" {
  enabled = true
  preset  = "recommended"
}

plugin "aws" {
  enabled = true
  version = "0.49.0"
  source  = "github.com/terraform-linters/tflint-ruleset-aws"
}

rule "terraform_naming_convention" {
  enabled = true
}

# Every variable and output needs a description; they are the modules' only documentation.
rule "terraform_documented_variables" {
  enabled = true
}

rule "terraform_documented_outputs" {
  enabled = true
}
