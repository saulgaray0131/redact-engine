# messaging.tf - Azure Service Bus

resource "azurerm_servicebus_namespace" "sb" {
  name                = "${local.resource_prefix}-bus"
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
  sku                 = var.sb_sku

  tags = local.common_tags
}

# Grant the app identity access to manage Service Bus queues and topics.
resource "azurerm_role_assignment" "sb_data_owner" {
  scope                = azurerm_servicebus_namespace.sb.id
  role_definition_name = "Azure Service Bus Data Owner"
  principal_id         = azurerm_user_assigned_identity.app_identity.principal_id
}

resource "azurerm_servicebus_namespace_authorization_rule" "dapr_pubsub" {
  name         = "dapr-pubsub"
  namespace_id = azurerm_servicebus_namespace.sb.id

  listen = true
  send   = true
  manage = false
}

resource "azurerm_container_app_environment_dapr_component" "pubsub" {
  name                         = "pubsub"
  container_app_environment_id = azurerm_container_app_environment.aca_env.id
  component_type               = "pubsub.azure.servicebus"
  version                      = "v1"
  ignore_errors                = false
  scopes = [
    azurerm_container_app.api.name,
    azurerm_container_app.worker.name,
  ]

  secret {
    name  = "servicebus-connection-string"
    value = azurerm_servicebus_namespace_authorization_rule.dapr_pubsub.primary_connection_string
  }

  metadata {
    name        = "connectionString"
    secret_name = "servicebus-connection-string"
  }

  metadata {
    name  = "consumerID"
    value = local.resource_prefix
  }
}
