using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace Not.Structures;

public class Result : Result<Result.Empty>
{
    const string CANCELLED_ERROR = "Cancelled by user";

    public static Result Success()
    {
        return new(new Empty());
    }

    public static Result<T> Success<T>(T data)
    {
        return new(data);
    }

    public static Result Failure(params string[] errors)
    {
        return new(errors);
    }

    public static Result<T> Failure<T>(params string[] errors)
    {
        return new Result<T>(errors);
    }

    public static Result<T> Cancel<T>()
    {
        return new Result<T>([CANCELLED_ERROR]);
    }

    internal Result(Empty empty)
        : base(empty) { }

    [JsonConstructor]
    internal Result(IEnumerable<string> errors)
        : base(errors) { }

    public class Empty { }
}

public class Result<T>
{
    internal Result(T data)
        : this(data, [])
    {
        Data = data;
    }

    internal Result(IEnumerable<string> errors)
        : this(default, errors) { }

    [JsonConstructor]
    Result(T? data, IEnumerable<string> errors)
    {
        Data = data;
        Errors = errors.ToArray();
    }

    [MemberNotNullWhen(true, nameof(Data))]
    public bool IsSuccess => !Errors.Any();
    public T? Data { get; }
    public string[] Errors { get; } = [];
}
