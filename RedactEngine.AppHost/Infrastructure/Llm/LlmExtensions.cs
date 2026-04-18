using Microsoft.Extensions.Configuration;

namespace RedactEngine.AppHost.Infrastructure.Llm;

/// <summary>
/// Wires Azure OpenAI configuration for prompt translation into the API service.
/// Defaults to mock mode so local dev runs without an Azure key. Production values
/// are provided via user secrets or environment variables locally, or injected via
/// Container App secrets (Terraform-managed) in deployed environments.
/// </summary>
public static class LlmExtensions
{
    public static IResourceBuilder<ProjectResource> WithLlmTranslation(
        this IResourceBuilder<ProjectResource> service,
        IDistributedApplicationBuilder builder)
    {
        var config = builder.Configuration;
        var endpoint = config["Llm:Endpoint"] ?? string.Empty;
        var deployment = config["Llm:Deployment"] ?? string.Empty;
        var apiKey = config["Llm:ApiKey"] ?? string.Empty;

        // Default to mock unless all three Azure OpenAI settings are present.
        var configuredMode = config["Llm:Mode"];
        var mode = !string.IsNullOrWhiteSpace(configuredMode)
            ? configuredMode
            : (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(deployment) && !string.IsNullOrWhiteSpace(apiKey)
                ? "azure-openai"
                : "mock");

        return service
            .WithEnvironment("Llm__Mode", mode)
            .WithEnvironment("Llm__Endpoint", endpoint)
            .WithEnvironment("Llm__Deployment", deployment)
            .WithEnvironment("Llm__ApiKey", apiKey);
    }
}
