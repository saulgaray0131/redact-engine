namespace RedactEngine.Application.Common;

public class Result
{
    public bool IsSuccess { get; private set; }
    public bool IsFailure => !IsSuccess;
    public string Error { get; private set; } = string.Empty;
    public Dictionary<string, string[]> ValidationErrors { get; private set; } = [];

    protected Result(bool isSuccess, string error, Dictionary<string, string[]>? validationErrors = null)
    {
        if (isSuccess && !string.IsNullOrEmpty(error))
            throw new InvalidOperationException("Successful result cannot have an error");

        if (!isSuccess && string.IsNullOrEmpty(error))
            throw new InvalidOperationException("Failed result must have an error");

        IsSuccess = isSuccess;
        Error = error;
        ValidationErrors = validationErrors ?? [];
    }

    public bool HasValidationErrors => ValidationErrors.Count > 0;

    public static Result Success() => new(true, string.Empty);

    public static Result Failure(string error) => new(false, error);

    public static Result Failure(string error, Dictionary<string, string[]> validationErrors)
        => new(false, error, validationErrors);

    public static Result<T> Success<T>(T value) => new(value, true, string.Empty);

    public static Result<T> Failure<T>(string error) => new(default, false, error);

    public static Result<T> Failure<T>(string error, Dictionary<string, string[]> validationErrors)
        => new(default, false, error, validationErrors);

    public static Result<T> Failure<T>(IEnumerable<string> errorMessages)
        => new(default, false, string.Join('.', errorMessages));
}

public class Result<T> : Result
{
    public T? Value { get; private set; }

    protected internal Result(T? value, bool isSuccess, string error, Dictionary<string, string[]>? validationErrors = null)
        : base(isSuccess, error, validationErrors)
    {
        Value = value;
    }

    public static new Result<T> Failure(string error, Dictionary<string, string[]> validationErrors)
        => new(default, false, error, validationErrors);

    public static implicit operator Result<T>(T value) => Success(value);
}

public static class ResultExtensions
{
    public static Result<T> Ensure<T>(this Result<T> result, Func<T, bool> predicate, string error)
    {
        if (result.IsFailure)
            return result;

        return predicate(result.Value!)
            ? result
            : Result.Failure<T>(error);
    }

    public static Result<TOut> Map<TIn, TOut>(this Result<TIn> result, Func<TIn, TOut> mapper)
    {
        return result.IsSuccess
            ? Result.Success(mapper(result.Value!))
            : Result.Failure<TOut>(result.Error);
    }

    public static async Task<Result<TOut>> MapAsync<TIn, TOut>(this Task<Result<TIn>> resultTask, Func<TIn, TOut> mapper)
    {
        var result = await resultTask;
        return result.Map(mapper);
    }

    public static async Task<Result<TOut>> BindAsync<TIn, TOut>(this Task<Result<TIn>> resultTask, Func<TIn, Task<Result<TOut>>> binder)
    {
        var result = await resultTask;
        return result.IsSuccess
            ? await binder(result.Value!)
            : Result.Failure<TOut>(result.Error);
    }
}