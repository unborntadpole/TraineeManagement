namespace SubmissionProcessor.Worker.DTO;

public class Result<T>
{
    public bool IsSuccess { get; }
    public T Value { get; }
    public string Error { get; }
    public int ErrorCode { get; }

    private Result(bool isSuccess, T value, string error, int code)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
        ErrorCode = code;
    }

    public static Result<T> Success(T value) => new(true, value, null, 0);

    public static Result<T> SuccessWithCode(T value, int code) => new(true, value, null, code);

    public static Result<T> Failure(string error) => new(false, default, error, 0);
    public static Result<T> ServerError(string error, int code) => new(false, default, error, code);
}

