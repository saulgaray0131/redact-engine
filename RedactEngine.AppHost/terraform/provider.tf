terraform {
  required_version = ">= 1.5.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
    # azapi is used for the inference GPU managed environment only. The azurerm
    # provider always emits minimumCount/maximumCount on workload_profile, which
    # the Consumption-GPU SKU rejects with a 400. azapi lets us send the exact
    # payload the ARM API accepts.
    azapi = {
      source  = "Azure/azapi"
      version = "~> 2.0"
    }
  }

  # Remote state in Azure Blob Storage.
  # Container-level values are injected per-environment via
  #   terraform init -backend-config="key=<env>.tfstate"
  # in the CI/CD pipeline.
  backend "azurerm" {
    resource_group_name  = "redactengine-tfstate-rg"
    storage_account_name = "redactenginetfstate"
    container_name       = "tfstate"
    # key is set dynamically: dev.tfstate / prod.tfstate
  }
}

provider "azurerm" {
  features {
    resource_group {
      prevent_deletion_if_contains_resources = false
    }
    key_vault {
      purge_soft_delete_on_destroy = true
    }
  }

  subscription_id = var.subscription_id
}

provider "azapi" {
  subscription_id = var.subscription_id
  use_oidc        = true
}