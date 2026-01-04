namespace Not.Structures;

public class Result : ResultBase
{
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

    internal Result() { }

    internal Result(IEnumerable<string> errors)
        : base(errors) { }
}

public class Result<T> : ResultBase
{
    internal Result(T data)
    {
        Data = data;
    }

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
    public string[] Errors { get; } = Array.Empty<string>();
}
