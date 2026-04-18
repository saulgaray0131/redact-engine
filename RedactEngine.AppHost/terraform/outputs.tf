# terraform/outputs.tf

output "resource_group_name" {
  value = azurerm_resource_group.rg.name
}

# --- Container Registry ---
output "acr_name" {
  value = azurerm_container_registry.acr.name
}

output "acr_login_server" {
  value = azurerm_container_registry.acr.login_server
}

# --- Database ---
output "postgres_fqdn" {
  value = azurerm_postgresql_flexible_server.postgres.fqdn
}

output "postgres_connection_string" {
  value     = "Server=${azurerm_postgresql_flexible_server.postgres.fqdn};Database=core;User Id=adminuser;Password=${var.postgres_admin_password};SSL Mode=Require;"
  sensitive = true
}

# --- Messaging ---
output "servicebus_namespace" {
  value = azurerm_servicebus_namespace.sb.name
}

output "dapr_pubsub_component_name" {
  value = azurerm_container_app_environment_dapr_component.pubsub.name
}

# --- Compute ---
output "api_fqdn" {
  value = azurerm_container_app.api.ingress[0].fqdn
}

output "worker_name" {
  value = azurerm_container_app.worker.name
}

output "migration_job_name" {
  value = azurerm_container_app_job.db_migrator.name
}

# --- Identity ---
output "app_identity_client_id" {
  value = azurerm_user_assigned_identity.app_identity.client_id
}

# --- Key Vault ---
output "key_vault_name" {
  value = azurerm_key_vault.kv.name
}

output "key_vault_uri" {
  value = azurerm_key_vault.kv.vault_uri
}

# --- Storage ---
output "storage_account_name" {
  value = azurerm_storage_account.storage.name
}

output "storage_blob_endpoint" {
  value = azurerm_storage_account.storage.primary_blob_endpoint
}

# --- Azure OpenAI ---
output "azure_openai_endpoint" {
  value = azurerm_cognitive_account.openai.endpoint
}

output "azure_openai_deployment" {
  value = azurerm_cognitive_deployment.gpt_4o_mini.name
}

