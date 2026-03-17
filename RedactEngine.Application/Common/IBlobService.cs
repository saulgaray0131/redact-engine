namespace RedactEngine.Application.Common;

/// <summary>
/// Service interface for blob storage operations.
/// Used for uploading and managing media files (audio, images).
/// </summary>
public interface IBlobService
{
    /// <summary>
    /// Uploads a stream to blob storage.
    /// </summary>
    /// <param name="stream">The stream to upload.</param>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="contentType">The MIME content type (e.g., "audio/mpeg", "image/png").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The public/accessible URL of the uploaded blob.</returns>
    Task<string> UploadAsync(Stream stream, string fileName, string contentType, CancellationToken cancellationToken = default);
}
