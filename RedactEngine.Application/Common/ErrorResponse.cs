using System.Text.Json.Serialization;

namespace RedactEngine.Application.Common;

/// <summary>
/// Standardized error response format for API errors.
/// </summary>
/// <param name="Error">The error details.</param>
public record ErrorResponse(ErrorDetails Error)
{
    /// <summary>
    /// Creates an ErrorResponse from an error code and message.
    /// </summary>
    public static ErrorResponse Create(string code, string message, object? details = null)
    {
        return new ErrorResponse(new ErrorDetails(code, message, details));
    }

    /// <summary>
    /// Creates a validation error response.
    /// </summary>
    public static ErrorResponse ValidationError(string message, object? details = null)
    {
        return Create("VALIDATION_ERROR", message, details);
    }

    /// <summary>
    /// Creates a not found error response.
    /// </summary>
    public static ErrorResponse NotFound(string message)
    {
        return Create("NOT_FOUND", message);
    }

    /// <summary>
    /// Creates a conflict error response (e.g., duplicate resource).
    /// </summary>
    public static ErrorResponse Conflict(string message)
    {
        return Create("CONFLICT", message);
    }

    /// <summary>
    /// Creates an unauthorized error response.
    /// </summary>
    public static ErrorResponse Unauthorized(string message = "Authentication required.")
    {
        return Create("UNAUTHORIZED", message);
    }

    /// <summary>
    /// Creates a forbidden error response.
    /// </summary>
    public static ErrorResponse Forbidden(string message = "Access denied.")
    {
        return Create("FORBIDDEN", message);
    }

    /// <summary>
    /// Creates an internal server error response.
    /// </summary>
    public static ErrorResponse InternalError(string message = "An unexpected error occurred.")
    {
        return Create("INTERNAL_ERROR", message);
    }

    /// <summary>
    /// Creates a rate limit exceeded error response.
    /// </summary>
    public static ErrorResponse RateLimited(string message, object? details = null)
    {
        return Create("RATE_LIMIT_EXCEEDED", message, details);
    }

    /// <summary>
    /// Creates a quota exceeded error response.
    /// </summary>
    public static ErrorResponse QuotaExceeded(string message, object? details = null)
    {
        return Create("QUOTA_EXCEEDED", message, details);
    }
}

/// <summary>
/// Error details containing code, message, and optional additional details.
/// </summary>
/// <param name="Code">Machine-readable error code (e.g., "VALIDATION_ERROR", "NOT_FOUND").</param>
/// <param name="Message">Human-readable error message.</param>
/// <param name="Details">Optional additional error details.</param>
public record ErrorDetails(
    string Code,
    string Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    object? Details = null);
