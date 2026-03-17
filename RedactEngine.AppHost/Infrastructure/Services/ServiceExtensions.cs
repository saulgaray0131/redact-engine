using Aspire.Hosting.Azure;
using CommunityToolkit.Aspire.Hosting.Dapr;
using RedactEngine.AppHost.Infrastructure.Configuration;

namespace RedactEngine.AppHost.Infrastructure.Services;

/// <summary>
/// Service extensions for local development.
/// Azure deployment is handled by Terraform - this is for local Aspire setup only.
/// </summary>
public static class ServiceExtensions
{
    public static IResourceBuilder<ProjectResource> AddRedactEngineApiService(
        this IDistributedApplicationBuilder builder,
        string serviceName,
        IResourceBuilder<PostgresDatabaseResource> database,
        IResourceBuilder<AzureBlobStorageResource> blobs,
        InfrastructureOptions options)
    {
        return builder.AddProject<Projects.RedactEngine_ApiService>(serviceName, "https")
            .WithDaprSidecar(new DaprSidecarOptions
            {
                AppId = serviceName
            })
            .WithHttpHealthCheck("/health")
            .WithReference(database)
            .WaitFor(database)
            .WithReference(blobs)
            .WaitFor(blobs)
            .WithEnvironment("DOTNET_ENVIRONMENT", options.Environment)
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", options.Environment);
    }

    public static IResourceBuilder<ProjectResource> AddRedactEngineWorkerService(
        this IDistributedApplicationBuilder builder,
        string serviceName,
        IResourceBuilder<PostgresDatabaseResource> database,
        IResourceBuilder<AzureBlobStorageResource> blobs,
        InfrastructureOptions options)
    {
        return builder.AddProject<Projects.RedactEngine_Worker>(serviceName, "http")
            .WithDaprSidecar(new DaprSidecarOptions
            {
                AppId = serviceName
            })
            .WithHttpHealthCheck("/health")
            .WithReference(database)
            .WaitFor(database)
            .WithReference(blobs)
            .WaitFor(blobs)
            .WithEnvironment("DOTNET_ENVIRONMENT", options.Environment)
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", options.Environment);
    }
}
