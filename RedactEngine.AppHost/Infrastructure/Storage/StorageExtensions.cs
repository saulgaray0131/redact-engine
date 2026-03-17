using Aspire.Hosting.Azure;
using RedactEngine.AppHost.Infrastructure.Configuration;

namespace RedactEngine.AppHost.Infrastructure.Storage;

/// <summary>
/// Storage extensions for local development using Azurite emulator.
/// </summary>
public static class StorageExtensions
{
    public static (IResourceBuilder<AzureStorageResource> storage, IResourceBuilder<AzureBlobStorageResource> blobs)
        AddRedactEngineStorage(
            this IDistributedApplicationBuilder builder,
            InfrastructureOptions options)
    {
        var storage = builder.AddAzureStorage(options.Storage.Name)
            .RunAsEmulator(emulator => emulator.WithLifetime(ContainerLifetime.Persistent));

        var blobs = storage.AddBlobs("BlobStorage");

        return (storage, blobs);
    }
}
