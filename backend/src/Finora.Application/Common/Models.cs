namespace Finora.Application.Common;

public class Result
{
    public bool IsSuccess { get; }
    public string? Error { get; }
    public Dictionary<string, string[]>? ValidationErrors { get; }

    protected Result(bool isSuccess, string? error, Dictionary<string, string[]>? validationErrors = null)
    {
        IsSuccess = isSuccess;
        Error = error;
        ValidationErrors = validationErrors;
    }

    public static Result Success() => new(true, null);
    public static Result Failure(string error) => new(false, error);
    public static Result Invalid(Dictionary<string, string[]> validationErrors) => 
        new(false, "One or more validation errors occurred.", validationErrors);
}

public class Result<T> : Result
{
    public T? Value { get; }

    private Result(bool isSuccess, T? value, string? error, Dictionary<string, string[]>? validationErrors = null)
        : base(isSuccess, error, validationErrors)
    {
        Value = value;
    }

    public static Result<T> Success(T value) => new(true, value, null);
    public new static Result<T> Failure(string error) => new(false, default, error);
    public new static Result<T> Invalid(Dictionary<string, string[]> validationErrors) => 
        new(false, default, "One or more validation errors occurred.", validationErrors);
}

public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

