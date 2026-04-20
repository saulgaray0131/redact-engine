variable "environment" {
  type        = string
  description = "The environment name (dev, prod)"
}

variable "location" {
  type    = string
  default = "eastus"
}

variable "project_name" {
  type    = string
  default = "RedactEngine"
}

variable "subscription_id" {
  type        = string
  description = "Azure Subscription ID"
}

# --- Database Variables ---
variable "postgres_sku" {
  type        = string
  description = "SKU for PostgreSQL Flexible Server (e.g., B_Standard_B1ms, GP_Standard_D2s_v3)"
}

variable "postgres_admin_password" {
  type        = string
  sensitive   = true
  description = "Administrator password for PostgreSQL server"
}

variable "postgres_storage_mb" {
  type        = number
  description = "Storage in MB for PostgreSQL Flexible Server"
  default     = 32768
}

# --- Messaging Variables ---
variable "sb_sku" {
  type        = string
  description = "Service Bus SKU (Basic, Standard, Premium)"
}

# --- Compute Variables (Container Apps) ---
variable "api_config" {
  type = object({
    min_replicas = number
    max_replicas = number
    cpu          = number
    memory       = string
  })
  description = "Scaling and sizing config for API Service"
}

variable "worker_config" {
  type = object({
    min_replicas = number
    max_replicas = number
    cpu          = number
    memory       = string
  })
  description = "Scaling and sizing config for Worker Service"
}

variable "inference_config" {
  type = object({
    min_replicas = number
    max_replicas = number
    cpu          = number
    memory       = string
  })
  description = "Scaling and sizing config for Inference Service"
}

variable "inference_location" {
  type        = string
  description = "Region for the inference service's Container App Environment. Must support Consumption-GPU workload profiles (e.g. eastus, westus3, swedencentral). Distinct from var.location so the GPU env can live apart from the rest of the stack."
  default     = "eastus"
}

variable "inference_workload_profile_name" {
  type        = string
  description = "Logical name assigned to the GPU workload profile on the inference env. Referenced from azurerm_container_app.inference.workload_profile_name."
  default     = "gpu-t4"
}

variable "inference_service_key" {
  type        = string
  sensitive   = true
  description = "Shared secret required on X-Inference-Key header for worker -> inference calls. The inference app runs on a public FQDN once moved to its own env, so this gates access."
  default     = ""
}

variable "inference_warm_schedule" {
  type = object({
    timezone = string
    start    = string
    end      = string
  })
  description = "KEDA cron scale rule for the inference service. Between `start` and `end` (in `timezone`), the service holds 1 replica; outside, it scales to 0. Cron is standard 5-field (minute hour dom month dow)."
  default = {
    timezone = "America/Chicago"
    start    = "0 8 * * 1-5"
    end      = "0 18 * * 1-5"
  }
}

# --- Security Variables ---
variable "admin_object_id" {
  type        = string
  description = "Azure AD Object ID for Key Vault admin access"
  default     = ""
}

# --- Cognitive Services / Azure OpenAI Variables ---
variable "cognitive_location" {
  type        = string
  description = "Region for Azure OpenAI (must support the chosen model). Defaults to var.location."
  default     = ""
}

variable "cognitive_sku" {
  type        = string
  description = "SKU for the Azure OpenAI cognitive account"
  default     = "S0"
}

variable "cognitive_deployment_capacity" {
  type        = number
  description = "TPM capacity (in thousands) for the gpt-4o-mini deployment"
  default     = 20
}