namespace Momentum.Application.Common;

/// <summary>
/// Lightweight result type for the (uncommon) cases where a service wants to
/// signal "this didn't work" without the cost/semantics of throwing. Most error
/// flow should still use the AppException hierarchy so the global handler's
/// error mapping applies uniformly; reach for Result when the caller is expected
/// to branch on the outcome rather than treat it as exceptional.
/// </summary>
public readonly struct Result
{
    private Result(bool isSuccess, string? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public string? Error { get; }

    public static Result Success() => new(true, null);

    public static Result Failure(string error) => new(false, error);
}

/// <inheritdoc cref="Result"/>
public readonly struct Result<T>
{
    private Result(bool isSuccess, T? value, string? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public T? Value { get; }

    public string? Error { get; }

    public static Result<T> Success(T value) => new(true, value, null);

    public static Result<T> Failure(string error) => new(false, default, error);
}
