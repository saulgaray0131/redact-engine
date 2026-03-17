locals {
  # Force lowercase for Azure resource naming compliance
  project_name_safe = lower(var.project_name)

  # Resource prefix: e.g., redactengine-dev, redactengine-prod
  resource_prefix = "${local.project_name_safe}-${var.environment}"

  common_tags = {
    environment = var.environment
    project     = var.project_name
    managed_by  = "terraform"
  }
}

resource "azurerm_resource_group" "rg" {
  name     = "${local.resource_prefix}-rg"
  location = var.location
  tags     = local.common_tags
}

resource "azurerm_container_registry" "acr" {
  # ACR names must be alphanumeric only (no hyphens)
  name                = "acr${local.project_name_safe}${var.environment}"
  resource_group_name = azurerm_resource_group.rg.name
  location            = azurerm_resource_group.rg.location
  sku                 = var.environment == "prod" ? "Standard" : "Basic"
  admin_enabled       = false
  tags                = local.common_tags
}