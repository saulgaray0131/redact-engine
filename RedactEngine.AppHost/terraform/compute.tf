# terraform/compute.tf

# --- 1. Container App Environment ---
resource "azurerm_container_app_environment" "aca_env" {
  name                       = "${local.resource_prefix}-aca"
  location                   = azurerm_resource_group.rg.location
  resource_group_name        = azurerm_resource_group.rg.name
  log_analytics_workspace_id = azurerm_log_analytics_workspace.logs.id

  tags = local.common_tags
}

resource "azurerm_role_assignment" "acr_pull" {
  scope                = azurerm_container_registry.acr.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_user_assigned_identity.app_identity.principal_id
}

# --- 2. API Service ---
resource "azurerm_container_app" "api" {
  name                         = "${local.resource_prefix}-api"
  container_app_environment_id = azurerm_container_app_environment.aca_env.id
  resource_group_name          = azurerm_resource_group.rg.name
  revision_mode                = "Single"

  dapr {
    app_id       = "${local.resource_prefix}-api"
    app_port     = 8080
    app_protocol = "http"
  }

  secret {
    name  = "postgres-conn"
    value = "Server=${azurerm_postgresql_flexible_server.postgres.fqdn};Database=core;User Id=adminuser;Password=${var.postgres_admin_password};SSL Mode=Require;Maximum Pool Size=${var.environment == "prod" ? 30 : 10};Minimum Pool Size=1;"
  }

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.app_identity.id]
  }

  registry {
    server   = azurerm_container_registry.acr.login_server
    identity = azurerm_user_assigned_identity.app_identity.id
  }

  template {
    min_replicas = var.api_config.min_replicas
    max_replicas = var.api_config.max_replicas

    container {
      name   = "api-service"
      image  = "mcr.microsoft.com/azuredocs/containerapps-helloworld:latest"
      cpu    = var.api_config.cpu
      memory = var.api_config.memory

      # --- Connection Strings ---
      env {
        name        = "ConnectionStrings__Core"
        secret_name = "postgres-conn"
      }

      env {
        name  = "ConnectionStrings__keyvault"
        value = azurerm_key_vault.kv.vault_uri
      }

      # --- Observability ---
      env {
        name  = "APPLICATIONINSIGHTS_CONNECTION_STRING"
        value = azurerm_application_insights.appinsights.connection_string
      }

      # --- Identity & Runtime ---
      env {
        name  = "AZURE_CLIENT_ID"
        value = azurerm_user_assigned_identity.app_identity.client_id
      }

      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = var.environment == "dev" ? "Development" : "Production"
      }

      env {
        name  = "ASPNETCORE_URLS"
        value = "http://+:8080"
      }

      # --- Health Probes (30s interval to reduce App Insights telemetry volume) ---
      liveness_probe {
        transport = "HTTP"
        path      = "/alive"
        port      = 8080

        initial_delay    = 5
        interval_seconds = 60
        timeout          = 5
        failure_count_threshold = 3
      }

      readiness_probe {
        transport = "HTTP"
        path      = "/health"
        port      = 8080

        interval_seconds = 60
        timeout          = 5
        failure_count_threshold = 3
      }

      startup_probe {
        transport = "HTTP"
        path      = "/health"
        port      = 8080

        interval_seconds = 10
        timeout          = 5
        failure_count_threshold = 10
      }
    }
  }

  lifecycle {
    ignore_changes = [
      template[0].container[0].image,
    ]
  }

  ingress {
    external_enabled = true
    target_port      = 8080
    traffic_weight {
      percentage      = 100
      latest_revision = true
    }

  }

  tags = local.common_tags
}

# --- 3. Worker Service ---
resource "azurerm_container_app" "worker" {
  name                         = "${local.resource_prefix}-worker"
  container_app_environment_id = azurerm_container_app_environment.aca_env.id
  resource_group_name          = azurerm_resource_group.rg.name
  revision_mode                = "Single"

  dapr {
    app_id       = "${local.resource_prefix}-worker"
    app_port     = 8080
    app_protocol = "http"
  }

  secret {
    name  = "postgres-conn"
    value = "Server=${azurerm_postgresql_flexible_server.postgres.fqdn};Database=core;User Id=adminuser;Password=${var.postgres_admin_password};SSL Mode=Require;Maximum Pool Size=${var.environment == "prod" ? 30 : 10};Minimum Pool Size=1;"
  }

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.app_identity.id]
  }

  registry {
    server   = azurerm_container_registry.acr.login_server
    identity = azurerm_user_assigned_identity.app_identity.id
  }

  template {
    min_replicas = var.worker_config.min_replicas
    max_replicas = var.worker_config.max_replicas

    container {
      name   = "worker-service"
      image  = "mcr.microsoft.com/azuredocs/containerapps-helloworld:latest"
      cpu    = var.worker_config.cpu
      memory = var.worker_config.memory

      # --- Connection Strings ---
      env {
        name        = "ConnectionStrings__Core"
        secret_name = "postgres-conn"
      }

      env {
        name  = "ConnectionStrings__BlobStorage"
        value = azurerm_storage_account.storage.primary_blob_connection_string
      }

      env {
        name  = "ConnectionStrings__keyvault"
        value = azurerm_key_vault.kv.vault_uri
      }

      # --- Observability ---
      env {
        name  = "APPLICATIONINSIGHTS_CONNECTION_STRING"
        value = azurerm_application_insights.appinsights.connection_string
      }

      # --- Identity & Runtime ---
      env {
        name  = "AZURE_CLIENT_ID"
        value = azurerm_user_assigned_identity.app_identity.client_id
      }

      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = var.environment == "dev" ? "Development" : "Production"
      }

      env {
        name  = "ASPNETCORE_URLS"
        value = "http://+:8080"
      }

      # --- Health Probes (30s interval to reduce App Insights telemetry volume) ---
      liveness_probe {
        transport = "HTTP"
        path      = "/alive"
        port      = 8080

        initial_delay    = 5
        interval_seconds = 30
        timeout          = 5
        failure_count_threshold = 3
      }

      readiness_probe {
        transport = "HTTP"
        path      = "/health"
        port      = 8080

        interval_seconds = 30
        timeout          = 5
        failure_count_threshold = 3
      }

      startup_probe {
        transport = "HTTP"
        path      = "/health"
        port      = 8080

        interval_seconds = 10
        timeout          = 5
        failure_count_threshold = 10
      }
    }
  }

  lifecycle {
    ignore_changes = [
      template[0].container[0].image,
    ]
  }

  tags = local.common_tags
}

# --- 4. One-off Database Migration Job ---
resource "azurerm_container_app_job" "db_migrator" {
  name                         = "${local.resource_prefix}-migrator"
  location                     = azurerm_resource_group.rg.location
  resource_group_name          = azurerm_resource_group.rg.name
  container_app_environment_id = azurerm_container_app_environment.aca_env.id

  replica_timeout_in_seconds = 1800
  replica_retry_limit        = 0

  secret {
    name  = "postgres-conn"
    value = "Server=${azurerm_postgresql_flexible_server.postgres.fqdn};Database=core;User Id=adminuser;Password=${var.postgres_admin_password};SSL Mode=Require;Maximum Pool Size=5;Minimum Pool Size=1;"
  }

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.app_identity.id]
  }

  registry {
    server   = azurerm_container_registry.acr.login_server
    identity = azurerm_user_assigned_identity.app_identity.id
  }

  manual_trigger_config {
    parallelism              = 1
    replica_completion_count = 1
  }

  template {
    container {
      name   = "db-migrator"
      image  = "mcr.microsoft.com/k8se/quickstart-jobs:latest"
      cpu    = 0.5
      memory = "1Gi"

      env {
        name        = "ConnectionStrings__Core"
        secret_name = "postgres-conn"
      }

      env {
        name  = "APPLICATIONINSIGHTS_CONNECTION_STRING"
        value = azurerm_application_insights.appinsights.connection_string
      }

      env {
        name  = "AZURE_CLIENT_ID"
        value = azurerm_user_assigned_identity.app_identity.client_id
      }

      env {
        name  = "DOTNET_ENVIRONMENT"
        value = var.environment == "dev" ? "Development" : "Production"
      }
    }
  }

  lifecycle {
    ignore_changes = [
      template[0].container[0].image,
    ]
  }

  tags = local.common_tags
}
