using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace Not.Structures;

public class Result : ResultBase
{
    const string CANCELLED_ERROR = "Cancelled by user";

    public static Result Success()
    {
        return new();
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

    internal Result() { }

    [JsonConstructor]
    internal Result(IEnumerable<string> errors)
        : base(errors) { }
}

public class Result<T> : ResultBase
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
        : base(errors)
    {
        Data = data;
    }

    [MemberNotNullWhen(false, nameof(Data))]
    public new bool IsError => base.IsError;

    public T? Data { get; }
}

public abstract class ResultBase
{
    protected ResultBase() { }

    protected ResultBase(IEnumerable<string> errors)
    {
        Errors = errors.ToArray();
    }

    public bool IsError => Errors.Any();
    public string[] Errors { get; } = [];
}
