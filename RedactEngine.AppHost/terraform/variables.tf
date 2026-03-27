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

# --- Security Variables ---
variable "admin_object_id" {
  type        = string
  description = "Azure AD Object ID for Key Vault admin access"
  default     = ""
}