# monitoring.tf
resource "azurerm_log_analytics_workspace" "logs" {
  name                = "${local.resource_prefix}-logs"
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
  sku                 = "PerGB2018"
  retention_in_days   = var.environment == "prod" ? 90 : 30
  daily_quota_gb      = var.environment == "prod" ? -1 : 1 # 1 GB/day cap for dev
  tags                = local.common_tags
}

resource "azurerm_application_insights" "appinsights" {
  name                = "${local.resource_prefix}-appinsights"
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
  application_type    = "web"
  workspace_id        = azurerm_log_analytics_workspace.logs.id
  daily_data_cap_in_gb = var.environment == "prod" ? 10 : 1
  tags                = local.common_tags
}