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

inference_config = {
  # Base is min=0; a KEDA cron rule (see inference_warm_schedule) holds
  # min=1 during business hours so the pod is pre-warmed when users are
  # working, and sleeps overnight/weekends. Off-hours requests will hit
  # the 5-10 minute cold start and likely fail until the ACR is colocated
  # in eastus and the image is slimmed enough to fit inside KEDA's
  # 5-minute scale-to-zero cooldown.
  min_replicas = 0
  max_replicas = 1
  # Consumption-GPU-NC8as-T4 nodes are 8 vCPU / 56 GiB / 1x NVIDIA T4.
  # Container allocation must match the node sku on GPU workload profiles.
  cpu    = 8
  memory = "56Gi"
}

# Pre-warm schedule: 8am-6pm America/Chicago, Mon-Fri. Adjust if your
# working hours differ. Outside this window the inference pod scales to 0.
inference_warm_schedule = {
  timezone = "America/Chicago"
  start    = "0 8 * * 1-5"
  end      = "0 18 * * 1-5"
}

# Inference service lives in its own ACA env in a GPU-supported region.
# centralus does not offer Consumption-GPU workload profiles as of 2026-04.
inference_location = "eastus"

# admin_object_id: injected via TF_VAR_admin_object_id in CI
# inference_service_key: injected via TF_VAR_inference_service_key in CI
