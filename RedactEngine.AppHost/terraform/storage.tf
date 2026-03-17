# storage.tf - Azure Storage Account (Blob Storage for workflow artifacts)

resource "azurerm_storage_account" "storage" {
  # Storage account names must be 3-24 chars, lowercase alphanumeric only
  name                     = "st${replace(local.project_name_safe, "-", "")}${var.environment}"
  resource_group_name      = azurerm_resource_group.rg.name
  location                 = azurerm_resource_group.rg.location
  account_tier             = "Standard"
  account_replication_type = var.environment == "prod" ? "GRS" : "LRS"
  min_tls_version          = "TLS1_2"

  blob_properties {
    delete_retention_policy {
      days = var.environment == "prod" ? 30 : 7
    }
  }

  tags = local.common_tags
}

# Grant the app identity access to blob storage
resource "azurerm_role_assignment" "storage_blob_contributor" {
  scope                = azurerm_storage_account.storage.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_user_assigned_identity.app_identity.principal_id
}

# Container for workflow artifacts (e.g., OpenAPI specs, logs)
resource "azurerm_storage_container" "artifacts" {
  name                  = "artifacts"
  storage_account_id    = azurerm_storage_account.storage.id
  container_access_type = "private"
}
