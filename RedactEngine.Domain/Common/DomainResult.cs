namespace RedactEngine.Domain.Common;

/// <summary>
/// Represents the result of an operation that may succeed or fail.
/// </summary>
public class DomainResult
{
    /// <summary>
    /// Indicates whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Indicates whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Collection of error messages if the operation failed.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>
    /// Single error message (first error if multiple exist).
    /// </summary>
    public string? Error => Errors.FirstOrDefault();

    protected DomainResult(bool isSuccess, IReadOnlyList<string> errors)
    {
        IsSuccess = isSuccess;
        Errors = errors;
    }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static DomainResult Success() => new(true, []);

    /// <summary>
    /// Creates a failed result with a single error message.
    /// </summary>
    public static DomainResult Failure(string error) => new(false, [error]);

    /// <summary>
    /// Creates a failed result with multiple error messages.
    /// </summary>
    public static DomainResult Failure(IEnumerable<string> errors) => new(false, errors.ToList());
}
