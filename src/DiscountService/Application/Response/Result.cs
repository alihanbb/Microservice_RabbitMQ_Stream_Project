namespace DiscountService.Application.Response;

public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }
    public IReadOnlyList<string> Errors { get; }

    private Result(bool isSuccess, T? value, string? error, IReadOnlyList<string>? errors = null)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
        Errors = errors ?? Array.Empty<string>();
    }

    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(string error) => new(false, default, error);
    public static Result<T> Failure(IReadOnlyList<string> errors) => new(false, default, errors.FirstOrDefault(), errors);

    public static implicit operator Result<T>(T value) => Success(value);
}

public class Result
{
    public bool IsSuccess { get; }
    public string? Error { get; }
    public IReadOnlyList<string> Errors { get; }

    private Result(bool isSuccess, string? error, IReadOnlyList<string>? errors = null)
    {
        IsSuccess = isSuccess;
        Error = error;
        Errors = errors ?? Array.Empty<string>();
    }

    public static Result Success() => new(true, null);
    public static Result Failure(string error) => new(false, error);
    public static Result Failure(IReadOnlyList<string> errors) => new(false, errors.FirstOrDefault(), errors);
}
