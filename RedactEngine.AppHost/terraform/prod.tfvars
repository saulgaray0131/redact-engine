environment = "prod"
location    = "centralus"
# subscription_id and admin_object_id are injected via TF_VAR_* env vars in CI

# --- Database ---
postgres_sku        = "B_Standard_B1ms"
postgres_storage_mb = 32768 # 32 GB

# --- CORS ---

# --- Messaging ---
sb_sku = "Basic"

# --- Compute ---
api_config = {
  min_replicas = 1
  max_replicas = 1
  cpu          = 0.5
  memory       = "1.0Gi"
}

worker_config = {
  min_replicas = 1
  max_replicas = 1
  cpu          = 0.5
  memory       = "1.0Gi"
}

# admin_object_id: injected via TF_VAR_admin_object_id in CI
