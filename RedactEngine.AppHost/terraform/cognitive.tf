# cognitive.tf
# Azure OpenAI resource for prompt translation (natural language -> Grounding DINO prompts)

resource "azurerm_cognitive_account" "openai" {
  name                  = "${local.resource_prefix}-openai"
  location              = coalesce(var.cognitive_location, var.location)
  resource_group_name   = azurerm_resource_group.rg.name
  kind                  = "OpenAI"
  sku_name              = var.cognitive_sku
  custom_subdomain_name = "${local.resource_prefix}-openai"
  local_auth_enabled    = true

  tags = local.common_tags
}

resource "azurerm_cognitive_deployment" "gpt_4o_mini" {
  name                 = "gpt-4o-mini"
  cognitive_account_id = azurerm_cognitive_account.openai.id

  model {
    format  = "OpenAI"
    name    = "gpt-4o-mini"
    version = "2024-07-18"
  }

  sku {
    name     = "GlobalStandard"
    capacity = var.cognitive_deployment_capacity
  }
}

# Grant the app identity "Cognitive Services OpenAI User" so the managed identity
# could be used in the future. Today the app authenticates with the primary key
# (injected via Container App secret below), consistent with the Postgres pattern.
resource "azurerm_role_assignment" "app_openai_user" {
  scope                = azurerm_cognitive_account.openai.id
  role_definition_name = "Cognitive Services OpenAI User"
  principal_id         = azurerm_user_assigned_identity.app_identity.principal_id
}
