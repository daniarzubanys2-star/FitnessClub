namespace FitnessCrm;

// Result: either a success with a value, or a failure with an error message.
public class Result<T>
{
    public bool IsSuccess { get; }
    public T Value { get; }        // only meaningful when IsSuccess is true
    public string Error { get; }   // only meaningful when IsSuccess is false

    private Result(bool isSuccess, T value, string error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result<T> Ok(T value) => new Result<T>(true, value, "");

    public static Result<T> Fail(string error) => new Result<T>(false, default!, error);

    // Map: change the value inside, function cannot fail
    public Result<U> Map<U>(Func<T, U> f)
    {
        if (IsSuccess)
            return Result<U>.Ok(f(Value));
        return Result<U>.Fail(Error);
    }

    // Bind: next step can fail too. If we already failed, f is never called.
    public Result<U> Bind<U>(Func<T, Result<U>> f)
    {
        if (IsSuccess)
            return f(Value);
        return Result<U>.Fail(Error);
    }

    // Match: turn the result into one value (e.g. a message)
    public U Match<U>(Func<T, U> onSuccess, Func<string, U> onFailure)
    {
        return IsSuccess ? onSuccess(Value) : onFailure(Error);
    }
}

// Maybe: explicit "found / not found" instead of null.
public class Maybe<T>
{
    public bool HasValue { get; }
    public T Value { get; }

    private Maybe(bool hasValue, T value)
    {
        HasValue = hasValue;
        Value = value;
    }

    public static Maybe<T> Some(T value) => new Maybe<T>(true, value);

    public static Maybe<T> None() => new Maybe<T>(false, default!);

    public Maybe<U> Map<U>(Func<T, U> f)
    {
        return HasValue ? Maybe<U>.Some(f(Value)) : Maybe<U>.None();
    }

    // Convert "not found" into a failure with a clear message
    public Result<T> ToResult(string errorIfMissing)
    {
        return HasValue ? Result<T>.Ok(Value) : Result<T>.Fail(errorIfMissing);
    }
}
