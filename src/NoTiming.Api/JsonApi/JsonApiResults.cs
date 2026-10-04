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
        return location == null ? document : new HeaderResult(document, HeaderNames.Location, location);
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

    public static JsonSerializerOptions Options { get; } =
        new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

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
