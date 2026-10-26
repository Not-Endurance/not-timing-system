using System.Reflection;
using NoTiming.Api.JsonApi;

namespace NoTiming.Api.Features.Access;

/// <summary>
/// A part of a request that the endpoint filter looks at before the handler is reached: the body of a write or the query of a
/// list. The transport of it (its media type, its size, whether it is a document of the Api at all) is answered first, then
/// the place it names, and its content last, after the caller has been asked what they hold.
/// </summary>
internal interface IJsonApiInput
{
    /// <summary>The answer that refuses how the part came (415, 413, 400); none when it is whole.</summary>
    IResult? Transport { get; }

    /// <summary>The Event the part names, as the text it gave; none when it names none.</summary>
    string? NamedEvent { get; }

    /// <summary>What is wrong with the content of the part (400, 422); none when it is valid.</summary>
    JsonApiFailure? Content();
}

/// <summary>The attributes of a resource a body carries: the type of resource it is, and whether what it says is valid.</summary>
internal interface IResourceAttributes
{
    /// <summary>The type of the resource, as the document names it.</summary>
    static abstract string ResourceType { get; }

    /// <summary>What is wrong with the attributes, said once and in the same words wherever they are sent; none when they are valid.</summary>
    JsonApiFailure? Invalid();
}

/// <summary>Attributes that name the Event they are about, as a text: the Event of a request whose place is in its body.</summary>
internal interface INamesEvent
{
    string? EventId { get; }
}

/// <summary>
/// The body of a write: one resource of the type its attributes are. It is read when the parameter is bound, carries its own
/// error, and says nothing until the endpoint filter has asked the caller what they hold. The handler that has it is handed
/// attributes that are valid.
/// </summary>
internal sealed class JsonApiBody<TAttributes> : IJsonApiInput
    where TAttributes : class, IResourceAttributes
{
    public static async ValueTask<JsonApiBody<TAttributes>?> BindAsync(HttpContext context)
    {
        return new JsonApiBody<TAttributes>(
            await JsonApiRequests.ReadAsync<TAttributes>(context.Request, TAttributes.ResourceType)
        );
    }

    readonly JsonApiRead<TAttributes> _read;

    JsonApiBody(JsonApiRead<TAttributes> read)
    {
        _read = read;
    }

    /// <summary>The attributes; they are valid in the handler, which the filter does not reach before it knows.</summary>
    public TAttributes Attributes => _read.Attributes!;

    /// <summary>The id the document gave its resource, when it gave one.</summary>
    public string? Id => _read.Id;

    public IResult? Transport => _read.Error;

    public string? NamedEvent => (_read.Attributes as INamesEvent)?.EventId;

    public JsonApiFailure? Content()
    {
        return _read.Attributes?.Invalid();
    }
}

/// <summary>The members a list takes, on the parameter that has the query: the one it may be sorted by, and the ones it may be filtered by.</summary>
[AttributeUsage(AttributeTargets.Parameter)]
internal sealed class ListOfAttribute : Attribute
{
    public ListOfAttribute(string sortedBy, params string[] filteredBy)
    {
        SortedBy = sortedBy;
        FilteredBy = filteredBy;
    }

    public string SortedBy { get; }
    public string[] FilteredBy { get; }
}

/// <summary>The query of a list, read when the parameter is bound: the filter, the sort and the page, or the error that refuses them.</summary>
internal sealed class ListQuery : IJsonApiInput
{
    const string EVENT_ID = "eventId";

    public static ValueTask<ListQuery?> BindAsync(HttpContext context, ParameterInfo parameter)
    {
        var members =
            parameter.GetCustomAttribute<ListOfAttribute>()
            ?? throw new InvalidOperationException(
                $"The list '{parameter.Name}' names no member it is sorted or filtered by."
            );

        return new ValueTask<ListQuery?>(
            new ListQuery(JsonApiQuery.ReadList(context.Request, members.SortedBy, members.FilteredBy))
        );
    }

    readonly JsonApiList _list;

    ListQuery(JsonApiList list)
    {
        _list = list;
    }

    public bool Descending => _list.Descending;
    public int Skip => _list.Skip;
    public int Size => _list.Size;
    public int Number => _list.Number;
    public IReadOnlyDictionary<string, string> EqualTo => _list.EqualTo;

    public IResult? Transport => _list.Refusal;

    public string? NamedEvent => _list.EqualTo.GetValueOrDefault(EVENT_ID);

    public JsonApiFailure? Content()
    {
        return null;
    }
}
