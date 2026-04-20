using Aspire.Hosting.Azure;
using Aspire.Hosting.Python;

namespace RedactEngine.AppHost.Infrastructure.Inference;

/// <summary>
/// Adds the Python inference service using Aspire's Python hosting.
/// Uses AddUvicornApp to run the FastAPI app via Uvicorn.
/// In production this is deployed as a separate Container App via Terraform.
/// </summary>
public static class InferenceExtensions
{
    public static IResourceBuilder<UvicornAppResource> AddRedactEngineInferenceService(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<AzureBlobStorageResource> blobs,
        IResourceBuilder<ProjectResource> apiService)
    {
        // Note on SAM 2 device selection: we intentionally do NOT force
        // SAM2_DEVICE=mps on macOS. MPS + PYTORCH_ENABLE_MPS_FALLBACK tested
        // slower than pure CPU for SAM 2 propagation because enough ops hit
        // the fallback path that the MPS<->CPU tensor copy overhead dominates.
        // The Python side's _select_sam2_device() defaults to CUDA if
        // available, else CPU — which is the right behavior on every OS.
        return builder.AddUvicornApp(
                name: "inference-service",
                appDirectory: "../RedactEngine.InferenceService",
                app: "app.main:app")
            .WithHttpHealthCheck("/health")
            .WithEnvironment("INFERENCE_MODE", "real")
            // HuggingFace tokenizers warn loudly when forked by uvicorn's
            // reloader. Silences the noise without changing behavior.
            .WithEnvironment("TOKENIZERS_PARALLELISM", "false")
            // Async /redact: the Python service uploads the redacted MP4 to
            // blob storage and posts a completion callback to the API. Both
            // endpoints + the blob connection string are injected by Aspire.
            .WithEnvironment("BLOB_STORAGE_CONNECTION", blobs.Resource.ConnectionStringExpression)
            .WithEnvironment("BLOB_CONTAINER_NAME", "media")
            .WithEnvironment("INFERENCE_CALLBACK_URL", apiService.GetEndpoint("http"));
    }
}
