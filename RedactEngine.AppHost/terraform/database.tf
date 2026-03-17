# database.tf - PostgreSQL primary data store

# --- PostgreSQL Flexible Server ---
resource "azurerm_postgresql_flexible_server" "postgres" {
  name                   = "${local.resource_prefix}-postgres"
  resource_group_name    = azurerm_resource_group.rg.name
  location               = azurerm_resource_group.rg.location
  version                = "16"
  administrator_login    = "adminuser"
  administrator_password = var.postgres_admin_password

  sku_name   = var.postgres_sku
  storage_mb = var.postgres_storage_mb

  backup_retention_days        = var.environment == "prod" ? 14 : 7
  geo_redundant_backup_enabled = var.environment == "prod" ? true : false

  lifecycle {
    ignore_changes = [zone, high_availability[0].standby_availability_zone]
  }

  tags = local.common_tags
}

# Application database (EF Core migrations target)
resource "azurerm_postgresql_flexible_server_database" "core" {
  name      = "core"
  server_id = azurerm_postgresql_flexible_server.postgres.id
  charset   = "UTF8"
  collation = "en_US.utf8"
}

# Enable required PostgreSQL extensions.
# UUID-OSSP: UUID generation for entity IDs
# PG_STAT_STATEMENTS: Query performance monitoring
resource "azurerm_postgresql_flexible_server_configuration" "extensions" {
  name      = "azure.extensions"
  server_id = azurerm_postgresql_flexible_server.postgres.id
  value     = "UUID-OSSP,PG_STAT_STATEMENTS"
}

# Allow Azure-hosted services to connect
resource "azurerm_postgresql_flexible_server_firewall_rule" "allow_azure_services" {
  name             = "AllowAzureServices"
  server_id        = azurerm_postgresql_flexible_server.postgres.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}
