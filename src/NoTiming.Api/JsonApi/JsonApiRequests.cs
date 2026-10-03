using System.Text.Json;

namespace NoTiming.Api.JsonApi;

/// <summary>
/// Reads the body of a write: a JSON:API document of one resource of the expected type. Requiring the media type
/// <c>application/vnd.api+json</c> also keeps another site from writing with a visitor's cookie: a browser may not
/// send that content type across origins without a preflight, which the CORS policy refuses.
/// </summary>
internal static class JsonApiRequests
{
    public static async Task<JsonApiRead<TAttributes>> ReadAsync<TAttributes>(HttpRequest request, string type)
        where TAttributes : class
    {
        if (!request.HasJsonApiContentType())
        {
            return JsonApiRead<TAttributes>.Rejected(
                JsonApiResults.Error(
                    StatusCodes.Status415UnsupportedMediaType,
                    "unsupported-media-type",
                    $"Send {JsonApiResults.MEDIA_TYPE}."
                )
            );
        }

        try
        {
            var document = await JsonSerializer.DeserializeAsync<JsonApiDocument<TAttributes>>(
                request.Body,
                JsonApiResults.Options,
                request.HttpContext.RequestAborted
            );
            if (document?.Data is not { Attributes: { } attributes } || document.Data.Type != type)
            {
                return JsonApiRead<TAttributes>.Rejected(
                    Malformed($"Send one resource of type '{type}' with attributes.")
                );
            }

            return JsonApiRead<TAttributes>.Accepted(attributes);
        }
        catch (JsonException)
        {
            return JsonApiRead<TAttributes>.Rejected(Malformed("The body is not valid JSON."));
        }
    }

    static IResult Malformed(string detail)
    {
        return JsonApiResults.Error(
            StatusCodes.Status400BadRequest,
            "malformed-request",
            "The request is malformed.",
            detail
        );
    }

    static bool HasJsonApiContentType(this HttpRequest request)
    {
        return request.ContentType != null
            && request.ContentType.StartsWith(JsonApiResults.MEDIA_TYPE, StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class JsonApiDocument<TAttributes>
    where TAttributes : class
{
    public JsonApiResourceObject<TAttributes>? Data { get; set; }
}

internal sealed class JsonApiResourceObject<TAttributes>
    where TAttributes : class
{
    public string? Type { get; set; }
    public string? Id { get; set; }
    public TAttributes? Attributes { get; set; }
}

internal sealed class JsonApiRead<TAttributes>
    where TAttributes : class
{
    public static JsonApiRead<TAttributes> Accepted(TAttributes attributes)
    {
        return new(attributes, null);
    }

    public static JsonApiRead<TAttributes> Rejected(IResult error)
    {
        return new(null, error);
    }

    JsonApiRead(TAttributes? attributes, IResult? error)
    {
        Attributes = attributes;
        Error = error;
    }

    public TAttributes? Attributes { get; }
    public IResult? Error { get; }
}
