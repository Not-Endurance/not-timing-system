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

    /// <summary>The id the document gave its resource, as the text it gave; none when it gave none or the part is not a document.</summary>
    string? DocumentId { get; }

    /// <summary>What is wrong with the content of the part (400, 409, 422); none when it is valid.</summary>
    JsonApiFailure? Content();
}

/// <summary>What a document says of the id of its resource.</summary>
internal enum DocumentIds
{
    /// <summary>It is no business of the Api's: a document that gives one is not refused for it.</summary>
    Ignored = 0,

    /// <summary>It may be left out, and a document that gives one gives a Guid (ADR-0009): the id a client makes.</summary>
    OptionalGuid,

    /// <summary>A change: it may be left out, and a document that gives one gives the id of the route.</summary>
    MatchesRoute,

    /// <summary>It has to be given, and to be a Guid that is not the empty one (ADR-0009): the id a device makes for what is recorded once.</summary>
    RequiredGuid,
}

/// <summary>The attributes of a resource a body carries: the type of resource it is, and whether what it says is valid.</summary>
internal interface IResourceAttributes
{
    /// <summary>
    /// The type of the resource, as the document names it, when the attributes are of one resource only; a route that is made
    /// for each of several (the families) says it with <see cref="ResourceAttribute"/>.
    /// </summary>
    static virtual string? ResourceType => null;

    /// <summary>What the document says of the id of its resource.</summary>
    static virtual DocumentIds Ids => DocumentIds.Ignored;

    /// <summary>What is said when the id of a change is not the one of the route.</summary>
    static virtual JsonApiFailure IdMismatch => JsonApiFailures.IdMismatch;

    /// <summary>What is said when a document that has to give an id gives none.</summary>
    static virtual JsonApiFailure IdRequired => JsonApiFailures.IdRequired;

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
        var type =
            context.GetEndpoint()?.Metadata.GetMetadata<ResourceAttribute>()?.Type
            ?? TAttributes.ResourceType
            ?? throw new InvalidOperationException(
                $"The route does not say what resource {typeof(TAttributes).Name} is."
            );

        return new JsonApiBody<TAttributes>(
            await JsonApiRequests.ReadAsync<TAttributes>(context.Request, type),
            context.GetRouteValue("id")?.ToString()
        );
    }

    readonly JsonApiRead<TAttributes> _read;
    readonly string? _routeId;

    JsonApiBody(JsonApiRead<TAttributes> read, string? routeId)
    {
        _read = read;
        _routeId = routeId;
    }

    /// <summary>The attributes; they are valid in the handler, which the filter does not reach before it knows.</summary>
    public TAttributes Attributes => _read.Attributes!;

    /// <summary>The id the document gave its resource, when it gave one; a Guid, when the attributes say it has to be.</summary>
    public string? Id => _read.Id;

    /// <summary>The <c>meta</c> the document gave its resource, when it gave one.</summary>
    public System.Text.Json.JsonElement? Meta => _read.Meta;

    public IResult? Transport => _read.Error;

    public string? NamedEvent => (_read.Attributes as INamesEvent)?.EventId;

    public string? DocumentId => _read.Id;

    public JsonApiFailure? Content()
    {
        if (_read.Attributes?.Invalid() is { } invalid)
        {
            return invalid;
        }

        return TAttributes.Ids switch
        {
            DocumentIds.OptionalGuid when Id != null && !Guid.TryParse(Id, out _) => JsonApiFailures.InvalidId,
            DocumentIds.MatchesRoute when Id != null && Id != _routeId => TAttributes.IdMismatch,
            DocumentIds.RequiredGuid when Id is null => TAttributes.IdRequired,
            DocumentIds.RequiredGuid when !Guid.TryParse(Id, out var given) || given == Guid.Empty =>
                JsonApiFailures.InvalidId,
            _ => null,
        };
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

    public string? DocumentId => null;

    public JsonApiFailure? Content()
    {
        return null;
    }
}

/// <summary>The failures of a request that more than one resource has, said once.</summary>
internal static class JsonApiFailures
{
    public static JsonApiFailure InvalidId { get; } =
        new(StatusCodes.Status400BadRequest, "invalid-id", "The id of a resource is a Guid.");

    public static JsonApiFailure IdMismatch { get; } =
        new(StatusCodes.Status409Conflict, "id-mismatch", "The id of the resource is not the one of the route.");

    public static JsonApiFailure IdRequired { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "id-required",
            "The resource needs the id a client makes for it.",
            "Send a Guid as the id of the resource."
        );
}
