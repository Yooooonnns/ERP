namespace DigitalisationERP.Core;

/// <summary>
/// Represents the result of an operation with success/failure status.
/// </summary>
/// <typeparam name="T">The type of the result value.</typeparam>
public class Result<T>
{
    /// <summary>
    /// Gets a value indicating whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets the result value if successful.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// Gets the error message if failed.
    /// </summary>
    public string? Error { get; }

    private Result(bool isSuccess, T? value, string? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static Result<T> Ok(T value) => new Result<T>(true, value, null);

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static Result<T> Fail(string error) => new Result<T>(false, default, error);
}

/// <summary>
/// Represents the result of an operation without a return value.
/// </summary>
public class Result
{
    /// <summary>
    /// Gets a value indicating whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets the error message if failed.
    /// </summary>
    public string? Error { get; }

    private Result(bool isSuccess, string? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static Result Ok() => new Result(true, null);

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static Result Fail(string error) => new Result(false, error);
}
