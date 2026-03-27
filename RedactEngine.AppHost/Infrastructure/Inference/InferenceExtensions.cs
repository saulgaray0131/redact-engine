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
        this IDistributedApplicationBuilder builder)
    {
        return builder.AddUvicornApp(
                name: "inference-service",
                appDirectory: "../RedactEngine.InferenceService",
                app: "app.main:app")
            .WithHttpHealthCheck("/health")
            .WithEnvironment("INFERENCE_MODE", "mock");
    }
}
