namespace NoTiming.Api.JsonApi;

/// <summary>
/// An error of the Api before it is an answer (the rest-api skill): the status, the stable code a client branches on, and
/// what a person reads. A request that takes one answer says it as the whole document, and an entry of a group says it
/// where its own result is.
/// </summary>
internal sealed class JsonApiFailure
{
    public JsonApiFailure(int status, string code, string title, string? detail = null)
    {
        Status = status;
        Code = code;
        Title = title;
        Detail = detail;
    }

    public int Status { get; }
    public string Code { get; }
    public string Title { get; }
    public string? Detail { get; }

    /// <summary>The error as a member of the <c>errors</c> of a document.</summary>
    public object AsError()
    {
        return new
        {
            status = Status.ToString(),
            code = Code,
            title = Title,
            detail = Detail,
        };
    }

    public IResult AsResult()
    {
        return JsonApiResults.Error(Status, Code, Title, Detail);
    }
}
