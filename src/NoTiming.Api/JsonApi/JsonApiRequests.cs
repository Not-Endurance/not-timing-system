using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;

namespace NoTiming.Api.JsonApi;

/// <summary>
/// Reads the body of a write: a JSON:API document of one resource of the expected type. Requiring the media type
/// <c>application/vnd.api+json</c> also keeps another site from writing with a visitor's cookie: a browser may not
/// send that content type across origins without a preflight, which the CORS policy refuses. A body of more than a
/// mebibyte is refused with 413 before it is read: nothing the Api takes comes near that, and a signed-in caller could
/// otherwise fill the database a document at a time.
/// </summary>
internal static class JsonApiRequests
{
    public const int MAX_BODY_BYTES = 1_048_576;

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

        if (request.ContentLength > MAX_BODY_BYTES)
        {
            return JsonApiRead<TAttributes>.Rejected(TooLarge());
        }

        // A body that does not say how long it is (chunked) is cut off by the server when it passes the limit.
        var limit = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (limit is { IsReadOnly: false })
        {
            limit.MaxRequestBodySize = MAX_BODY_BYTES;
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

            return JsonApiRead<TAttributes>.Accepted(attributes, document.Data.Id, document.Data.Meta);
        }
        catch (JsonException)
        {
            return JsonApiRead<TAttributes>.Rejected(Malformed("The body is not valid JSON."));
        }
    }

    static IResult TooLarge()
    {
        return JsonApiResults.Error(
            StatusCodes.Status413PayloadTooLarge,
            "payload-too-large",
            "The body is too large.",
            $"Send at most {MAX_BODY_BYTES} bytes."
        );
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

    /// <summary>What the document says about the resource that is not its attributes, such as the version it was based on.</summary>
    public JsonElement? Meta { get; set; }
}

internal sealed class JsonApiRead<TAttributes>
    where TAttributes : class
{
    public static JsonApiRead<TAttributes> Accepted(TAttributes attributes, string? id = null, JsonElement? meta = null)
    {
        return new(attributes, id, meta, null);
    }

    public static JsonApiRead<TAttributes> Rejected(IResult error)
    {
        return new(null, null, null, error);
    }

    JsonApiRead(TAttributes? attributes, string? id, JsonElement? meta, IResult? error)
    {
        Attributes = attributes;
        Id = id;
        Meta = meta;
        Error = error;
    }

    public TAttributes? Attributes { get; }

    /// <summary>The <c>meta</c> the document gave its resource, when it gave one.</summary>
    public JsonElement? Meta { get; }

    /// <summary>The id the document gave its resource, when it gave one.</summary>
    public string? Id { get; }

    public IResult? Error { get; }
}
