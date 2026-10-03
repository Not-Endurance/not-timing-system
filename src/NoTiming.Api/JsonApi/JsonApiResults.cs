using System.Text.Json;
using System.Text.Json.Serialization;

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

    public static IResult Resource(int status, string type, string id, object attributes, string? location = null)
    {
        var document = Results.Json(
            new
            {
                data = new
                {
                    type,
                    id,
                    attributes,
                },
            },
            Options,
            MEDIA_TYPE,
            status
        );
        return location == null ? document : new LocatedResult(document, location);
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

    public static JsonSerializerOptions Options { get; } =
        new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    sealed class LocatedResult : IResult
    {
        readonly IResult _inner;
        readonly string _location;

        public LocatedResult(IResult inner, string location)
        {
            _inner = inner;
            _location = location;
        }

        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.Headers.Location = _location;
            return _inner.ExecuteAsync(httpContext);
        }
    }
}
