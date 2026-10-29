using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Net.Http.Headers;

namespace NoTiming.Api.JsonApi;

/// <summary>
/// The documents of the rest-api skill: JSON:API 1.1, <c>application/vnd.api+json</c>, and errors with a stable
/// <c>code</c> that clients branch on.
/// </summary>
internal static class JsonApiResults
{
    public const string MEDIA_TYPE = "application/vnd.api+json";

    public static IResult Error(int status, string code, string title, string? detail = null)
    {
        return Results.Json(
            new
            {
                errors = new[]
                {
                    new
                    {
                        status = status.ToString(),
                        code,
                        title,
                        detail,
                    },
                },
            },
            Options,
            MEDIA_TYPE,
            status
        );
    }

    public static IResult Resource(
        int status,
        string type,
        string id,
        object attributes,
        string? location = null,
        object? meta = null
    )
    {
        var document = Results.Json(
            new
            {
                data = new
                {
                    type,
                    id,
                    attributes,
                    meta,
                },
            },
            Options,
            MEDIA_TYPE,
            status
        );
        return location == null ? document : new HeaderResult(document, HeaderNames.Location, location);
    }

    /// <summary>
    /// The members of a collection: each is a resource of the type, 200 with an empty list when there are none. A
    /// collection that is paged says where it is and where the next page is in its <c>links</c>, and which page it is in
    /// its <c>meta</c>. A named collection gives each item a <c>self</c> link to where the item is canonically.
    /// </summary>
    public static IResult Collection(
        string type,
        IEnumerable<(string Id, object Attributes)> items,
        object? links = null,
        object? meta = null,
        Func<string, string>? itemLink = null,
        Func<string, object?>? itemMeta = null
    )
    {
        return Results.Json(
            new
            {
                data = items.Select(item => new
                {
                    type,
                    id = item.Id,
                    attributes = item.Attributes,
                    links = itemLink == null ? null : new { self = itemLink(item.Id) },
                    meta = itemMeta?.Invoke(item.Id),
                }),
                links,
                meta,
            },
            Options,
            MEDIA_TYPE,
            StatusCodes.Status200OK
        );
    }

    /// <summary>
    /// 404 <c>not-found</c>. It is the answer for a record that is missing and for one that is somebody else's alike, so
    /// that nobody learns which ids exist.
    /// </summary>
    public static IResult NotFound()
    {
        return Error(StatusCodes.Status404NotFound, "not-found", "Not found");
    }

    /// <summary>400 <c>invalid-id</c>: the id of a resource that a client makes is a Guid (ADR-0009).</summary>
    public static IResult InvalidId()
    {
        return Error(StatusCodes.Status400BadRequest, "invalid-id", "The id of a resource is a Guid.");
    }

    /// <summary>409 <c>id-taken</c>: the id a client made is another Tenant's, which is not told about.</summary>
    public static IResult IdTaken()
    {
        return Error(
            StatusCodes.Status409Conflict,
            "id-taken",
            "A resource with that id exists and is not yours to see."
        );
    }

    /// <summary>409 <c>id-mismatch</c>: the document of a change is of another resource than the one of the route.</summary>
    public static IResult IdMismatch()
    {
        return Error(
            StatusCodes.Status409Conflict,
            "id-mismatch",
            "The id of the resource is not the one of the route."
        );
    }

    /// <summary>
    /// 429 <c>rate-limited</c> with the seconds until there is room again. It is the same for whatever was asked, and
    /// whatever its address has, so it tells nothing about accounts.
    /// </summary>
    public static IResult RateLimited(TimeSpan retryAfter)
    {
        var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
        return new HeaderResult(
            Error(
                StatusCodes.Status429TooManyRequests,
                "rate-limited",
                "Too many requests.",
                "Wait a little and try again."
            ),
            HeaderNames.RetryAfter,
            seconds.ToString(CultureInfo.InvariantCulture)
        );
    }

    /// <summary>The answer of every route that needs a caller when there is none: 401, never a redirect.</summary>
    public static IResult NotSignedIn()
    {
        return Error(StatusCodes.Status401Unauthorized, "not-signed-in", "Sign in to do this.");
    }

    /// <summary>Writes the error without an endpoint, for the events of the authentication handlers.</summary>
    public static async Task WriteErrorAsync(HttpContext context, int status, string code, string title)
    {
        await Error(status, code, title).ExecuteAsync(context);
    }

    public static JsonSerializerOptions Options { get; } = CreateOptions();

    /// <summary>camelCase members, no member that is null, and enums as their names: a name says what a number cannot.</summary>
    static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    sealed class HeaderResult : IResult
    {
        readonly IResult _inner;
        readonly string _name;
        readonly string _value;

        public HeaderResult(IResult inner, string name, string value)
        {
            _inner = inner;
            _name = name;
            _value = value;
        }

        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.Headers[_name] = _value;
            return _inner.ExecuteAsync(httpContext);
        }
    }
}
