namespace RedactEngine.Domain.Common;

/// <summary>
/// Represents the result of an operation that may return a value or fail.
/// </summary>
/// <typeparam name="T">The type of the value.</typeparam>
public class DomainResult<T> : DomainResult
{
    private readonly T? _value;

    /// <summary>
    /// The value if the operation was successful.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessing Value on a failed result.</exception>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access Value on a failed result.");

    private DomainResult(T value) : base(true, [])
    {
        _value = value;
    }

    private DomainResult(IReadOnlyList<string> errors) : base(false, errors)
    {
        _value = default;
    }

    /// <summary>
    /// Creates a successful result with a value.
    /// </summary>
    public static DomainResult<T> Success(T value) => new(value);

    /// <summary>
    /// Creates a failed result with a single error message.
    /// </summary>
    public new static DomainResult<T> Failure(string error) => new([error]);

    /// <summary>
    /// Creates a failed result with multiple error messages.
    /// </summary>
    public new static DomainResult<T> Failure(IEnumerable<string> errors) => new(errors.ToList());

    /// <summary>
    /// Implicitly converts a value to a successful result.
    /// </summary>
    public static implicit operator DomainResult<T>(T value) => Success(value);
}
