using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using RedactEngine.Application.Common;

namespace RedactEngine.Infrastructure.Services;

/// <summary>
/// Azure Blob Storage implementation of IBlobService.
/// Uploads media files to a configured container and returns accessible URLs.
/// </summary>
public class AzureBlobService : IBlobService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<AzureBlobService> _logger;
    private const string DefaultContainerName = "media";

    public AzureBlobService(BlobServiceClient blobServiceClient, ILogger<AzureBlobService> logger)
    {
        _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> UploadAsync(Stream stream, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(DefaultContainerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);

            // Generate unique blob name with timestamp to avoid collisions
            var uniqueFileName = $"{DateTime.UtcNow:yyyyMMdd}/{Guid.NewGuid()}/{fileName}";
            var blobClient = containerClient.GetBlobClient(uniqueFileName);

            var uploadOptions = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType
                }
            };

            // Reset stream position if possible
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            await blobClient.UploadAsync(stream, uploadOptions, cancellationToken);

            _logger.LogInformation("Uploaded blob {FileName} to container {ContainerName}, URL: {BlobUri}",
                uniqueFileName, DefaultContainerName, blobClient.Uri);

            return blobClient.Uri.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload blob {FileName} to container {ContainerName}",
                fileName, DefaultContainerName);
            throw;
        }
    }

    public async Task DeleteAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            var uri = new Uri(url);
            var blobName = uri.AbsolutePath.TrimStart('/').Substring(DefaultContainerName.Length + 1); // Remove container name and leading slash
            var containerClient = _blobServiceClient.GetBlobContainerClient(DefaultContainerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            _logger.LogInformation("Deleted blob {BlobName} from container {ContainerName}", blobName, DefaultContainerName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete blob from URL {Url}", url);
            throw;
        }
    }
}
