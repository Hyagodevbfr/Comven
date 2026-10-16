namespace Comven.Domain.Common;

public class DomainResult
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string Error { get; } = string.Empty;

    protected DomainResult(bool isSuccess, string? error)
    {
        IsSuccess = isSuccess;
        Error = error ?? string.Empty;
    }

    public static DomainResult Success()
    {
        return new DomainResult(true, null);
    }

    public static DomainResult Failure(string error) 
    {
        return new DomainResult(false, error);
    }

}

public class DomainResult<T> : DomainResult
{
    public T? Value { get; }

    private DomainResult(bool isSuccess, T? value, string? error) : base(isSuccess, error)
    {
        Value = value;
    }

    public static DomainResult<T> Success(T value)
    {
        return new DomainResult<T>(true, value, null);
    }

    public static new DomainResult<T> Failure(string error)
    {
        return new DomainResult<T>(false, default, error);
    }
}
