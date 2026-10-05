using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Not.Application.HTTP;

/// <summary>
/// Where a client finds the resources of the Api, which move as JSON:API documents (ADR-0008), and what it sends with a
/// write. The address is the one of the host that serves the Api, such as the host that served the page, and is not the
/// one of the legacy host that <see cref="NHttpSettings"/> names, so a family that has moved is reached while the ones
/// that have not are still reached where they are.
/// </summary>
public class JsonApiSettings
{
    /// <summary>The address of the resources, up to and including the prefix of the routes: <c>https://host/api</c>.</summary>
    public string? Url { get; set; }

    /// <summary>The headers a write carries, which the Api asks for on every write of a signed-in caller.</summary>
    public Dictionary<string, string> WriteHeaders { get; } = [];
}

/// <summary>One of the errors of a JSON:API response: the status and the stable code a client branches on, and what a person reads.</summary>
public class JsonApiError
{
    public JsonApiError(int status, string? code, string? title, string? detail)
    {
        Status = status;
        Code = code;
        Title = title;
        Detail = detail;
    }

    public int Status { get; }

    /// <summary>The stable, kebab-case identifier of what went wrong, such as <c>event-started</c>.</summary>
    public string? Code { get; }

    public string? Title { get; }
    public string? Detail { get; }

    /// <summary>What to tell a person: the title, and the detail when there is one.</summary>
    public string Message
    {
        get
        {
            var title = string.IsNullOrWhiteSpace(Title) ? $"The server answered {Status}." : Title;
            return string.IsNullOrWhiteSpace(Detail) ? title : $"{title} {Detail}";
        }
    }
}

/// <summary>An answer of the Api: its status, its document when it has one, where it says a new resource is, and its error.</summary>
public class JsonApiResponse
{
    public JsonApiResponse(HttpStatusCode status, JsonElement? document, string? location, JsonApiError? error)
    {
        Status = status;
        Document = document;
        Location = location;
        Error = error;
    }

    public HttpStatusCode Status { get; }
    public bool IsSuccess => (int)Status is >= 200 and < 300;
    public bool IsNotFound => Status == HttpStatusCode.NotFound;

    /// <summary>The document of the body: <c>data</c>, <c>links</c>, <c>meta</c> or <c>errors</c>.</summary>
    public JsonElement? Document { get; }

    public string? Location { get; }

    /// <summary>The first error of a response that failed, or one made of its status when the body said none.</summary>
    public JsonApiError? Error { get; }
}

/// <summary>
/// Sends requests to the Api and reads its answers as JSON:API documents (ADR-0008): the media type on both sides, the
/// headers a write carries, and the errors read with their <c>code</c> and not thrown away. An answer that is not a success
/// is a response with its error, as the status always reports the outcome; a request that did not arrive is the exception
/// of the transport. The session is the cookie the browser keeps, which a request of the same origin carries.
/// </summary>
public class JsonApiClient
{
    public const string MEDIA_TYPE = "application/vnd.api+json";

    readonly HttpClient _http;
    readonly JsonApiSettings _settings;

    public JsonApiClient(IHttpClientFactory httpClientFactory, IOptions<JsonApiSettings> options)
    {
        _http = httpClientFactory.CreateClient(nameof(JsonApiClient));
        _settings = options.Value;
    }

    /// <summary>What a document is read with: camelCase members and enums as their names.</summary>
    public static JsonSerializerOptions Options { get; } = CreateOptions(JsonIgnoreCondition.WhenWritingNull);

    /// <summary>
    /// What a document is written with: the same, and a member that is null is written as null, because on a change
    /// null is a member taken away, and leaving it out would leave it as it was.
    /// </summary>
    public static JsonSerializerOptions WriteOptions { get; } = CreateOptions(JsonIgnoreCondition.Never);

    public async Task<JsonApiResponse> Send(
        HttpMethod method,
        string endpoint,
        JsonElement? document = null,
        CancellationToken cancellationToken = default
    )
    {
        using var request = new HttpRequestMessage(method, BuildUrl(endpoint));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(MEDIA_TYPE));
        if (method != HttpMethod.Get)
        {
            foreach (var (name, value) in _settings.WriteHeaders)
            {
                request.Headers.TryAddWithoutValidation(name, value);
            }
        }

        if (document != null)
        {
            request.Content = new StringContent(document.Value.GetRawText(), Encoding.UTF8, MEDIA_TYPE);
        }

        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var parsed = Parse(body);
        return new JsonApiResponse(
            response.StatusCode,
            parsed,
            response.Headers.Location?.OriginalString,
            response.IsSuccessStatusCode ? null : ErrorOf(response.StatusCode, parsed)
        );
    }

    Uri BuildUrl(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(_settings.Url))
        {
            throw new InvalidOperationException("JsonApiSettings.Url is required to send requests to the Api.");
        }

        return new Uri($"{_settings.Url.TrimEnd('/')}/{endpoint.TrimStart('/')}");
    }

    static JsonElement? Parse(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var parsed = JsonDocument.Parse(body);
            return parsed.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    static JsonApiError ErrorOf(HttpStatusCode status, JsonElement? document)
    {
        if (
            document is { ValueKind: JsonValueKind.Object } root
            && root.TryGetProperty("errors", out var errors)
            && errors.ValueKind == JsonValueKind.Array
            && errors.GetArrayLength() > 0
        )
        {
            var first = errors[0];
            return new JsonApiError((int)status, Text(first, "code"), Text(first, "title"), Text(first, "detail"));
        }

        return new JsonApiError((int)status, null, null, null);
    }

    static string? Text(JsonElement element, string member)
    {
        return element.TryGetProperty(member, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    /// <summary>camelCase members and enums as their names, as the Api writes and reads them.</summary>
    static JsonSerializerOptions CreateOptions(JsonIgnoreCondition ignoreCondition)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = ignoreCondition,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
