# security.tf
data "azurerm_client_config" "current" {}

resource "azurerm_key_vault" "kv" {
  name                        = "${local.resource_prefix}-kv"
  location                    = azurerm_resource_group.rg.location
  resource_group_name         = azurerm_resource_group.rg.name
  enabled_for_disk_encryption = true
  tenant_id                   = data.azurerm_client_config.current.tenant_id
  soft_delete_retention_days  = 7
  purge_protection_enabled    = var.environment == "prod" ? true : false
  sku_name                    = "standard"

  rbac_authorization_enabled = true

  tags = local.common_tags
}

# 1. Grant the APP IDENTITY read access (Secrets User)
resource "azurerm_role_assignment" "kv_access_app" {
  scope                = azurerm_key_vault.kv.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_user_assigned_identity.app_identity.principal_id
}

# 2. Grant admin full access (only when admin_object_id is provided)
resource "azurerm_role_assignment" "kv_access_admin" {
  count                = var.admin_object_id != "" ? 1 : 0
  scope                = azurerm_key_vault.kv.id
  role_definition_name = "Key Vault Administrator"
  principal_id         = var.admin_object_id
}

# 3. Grant the Terraform Runner (Current User/SP) full access
resource "azurerm_role_assignment" "kv_access_terraform" {
  scope                = azurerm_key_vault.kv.id
  role_definition_name = "Key Vault Administrator"
  principal_id         = data.azurerm_client_config.current.object_id
}

resource "azurerm_user_assigned_identity" "app_identity" {
  location            = azurerm_resource_group.rg.location
  name                = "${local.resource_prefix}-id"
  resource_group_name = azurerm_resource_group.rg.name
  tags                = local.common_tags
}