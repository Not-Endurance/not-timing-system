using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Not.Application.HTTP;

/// <summary>
/// Where a client finds the resources of the Api, which move as JSON:API documents (ADR-0008), and what it sends with a
/// write. The address is the one of the host that serves the Api, such as the host that served the page.
/// </summary>
public class JsonApiSettings
{
    /// <summary>The address of the resources, up to and including the prefix of the routes: <c>https://host/api</c>.</summary>
    public string? Url { get; set; }

    /// <summary>The headers a write carries, which the Api asks for on every write of a signed-in caller.</summary>
    public Dictionary<string, string> WriteHeaders { get; } = [];

    /// <summary>
    /// The codes of a conflict that the Api says to try again later, such as <c>participation-busy</c>. An answer with one of
    /// them is an unavailable failure, not a refusal (ADR-0017).
    /// </summary>
    public HashSet<string> UnavailableCodes { get; } = [];

    /// <summary>
    /// What a document is read and written with: camelCase members, enums as their names, and a null written as null, which
    /// on a change takes the member away (ADR-0017).
    /// </summary>
    public JsonSerializerOptions Json { get; set; } = Defaults();

    /// <summary>
    /// camelCase members, enums as their names, and a null written as null: on a change null is a member taken away, and
    /// leaving it out would leave it as it was (ADR-0017).
    /// </summary>
    static JsonSerializerOptions Defaults()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
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
    public JsonApiResponse(
        HttpStatusCode status,
        JsonElement? document,
        string? location,
        JsonApiError? error,
        DateTimeOffset? serverTime = null,
        IReadOnlyDictionary<string, string>? headers = null
    )
    {
        Status = status;
        Document = document;
        Location = location;
        Error = error;
        ServerTime = serverTime;
        Headers = headers ?? new Dictionary<string, string>();
    }

    public HttpStatusCode Status { get; }
    public bool IsSuccess => (int)Status is >= 200 and < 300;
    public bool IsNotFound => Status == HttpStatusCode.NotFound;

    /// <summary>Whether the Api says that another page of the list follows: the document has <c>links.next</c>.</summary>
    public bool HasNextPage =>
        Document is { ValueKind: JsonValueKind.Object } document
        && document.TryGetProperty("links", out var links)
        && links.ValueKind == JsonValueKind.Object
        && links.TryGetProperty("next", out var next)
        && next.ValueKind == JsonValueKind.String;

    /// <summary>The document of the body: <c>data</c>, <c>links</c>, <c>meta</c> or <c>errors</c>.</summary>
    public JsonElement? Document { get; }

    public string? Location { get; }

    /// <summary>The time the server said it was when it answered (the <c>Date</c> header, to the second), when it said.</summary>
    public DateTimeOffset? ServerTime { get; }

    /// <summary>The headers of the answer, with the values of each joined by a comma: <c>Retry-After</c> says when to ask again.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; }

    /// <summary>The first error of a response that failed, or one made of its status when the body said none.</summary>
    public JsonApiError? Error { get; }

    /// <summary>
    /// What the Api answered when it did not succeed, as the exception of a caller that cannot go on: the words it said, and
    /// the stable code it said them with when it named one.
    /// </summary>
    public InvalidOperationException ToException()
    {
        var message = Error?.Message ?? Status.ToString();
        return new InvalidOperationException(Error?.Code == null ? message : $"{message} ({Error.Code})");
    }
}

/// <summary>
/// Sends requests to the Api and reads its answers as JSON:API documents (ADR-0008): the media type on both sides, the
/// headers a write carries, and the errors read with their <c>code</c> and not thrown away. An answer that is not a success
/// is a response with its error, as the status always reports the outcome; a request that did not arrive is the exception
/// of the transport. The session is the cookie the browser keeps, which a request of the same origin carries, and the language
/// is the one the app is in.
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

    /// <summary>The options this client reads and writes documents with: those of its settings.</summary>
    public JsonSerializerOptions Json => _settings.Json;

    /// <summary>The codes of a conflict the Api says to try again later, as the settings name them.</summary>
    public IReadOnlySet<string> UnavailableCodes => _settings.UnavailableCodes;

    /// <summary>
    /// A list of a collection, or any route that answers with one: its filter, its sort and one page of it, as the rest-api
    /// skill names them. A route that is not a list is asked with no query.
    /// </summary>
    public Task<JsonApiResponse> Get(string path, ListQuery? query = null, CancellationToken cancellationToken = default)
    {
        var parameters = new List<string>();
        if (query?.Filter is { } filter)
        {
            parameters.Add($"filter={Uri.EscapeDataString(filter)}");
        }

        if (query?.Sort is { } sort)
        {
            parameters.Add($"sort={Uri.EscapeDataString(sort)}");
        }

        if (query?.PageSize is { } size)
        {
            parameters.Add($"{Uri.EscapeDataString("page[size]")}={size}");
        }

        if (query?.PageNumber is { } number)
        {
            parameters.Add($"{Uri.EscapeDataString("page[number]")}={number}");
        }

        var endpoint = parameters.Count == 0 ? path : $"{path}?{string.Join('&', parameters)}";
        return Send(HttpMethod.Get, endpoint, null, cancellationToken);
    }

    /// <summary>
    /// Every resource of a list, a page at a time, until the Api says there is no next page. A list that goes on beyond
    /// <paramref name="maxPages"/> pages is not cut short: it is refused, as a list that cannot be shown whole. A refusal of
    /// a page is thrown as a <see cref="JsonApiException"/>.
    /// </summary>
    public async Task<IReadOnlyList<JsonElement>> GetAll(
        string path,
        ListQuery query,
        int maxPages,
        CancellationToken cancellationToken = default
    )
    {
        var resources = new List<JsonElement>();
        for (var number = 1; number <= maxPages; number++)
        {
            var response = await Get(path, query.OnPage(number), cancellationToken);
            if (!response.IsSuccess)
            {
                throw new JsonApiException(response, UnavailableCodes);
            }

            resources.AddRange(response.Document!.Value.GetProperty("data").EnumerateArray());
            if (!response.HasNextPage)
            {
                return resources;
            }
        }

        throw new InvalidOperationException($"The list at {path} goes on beyond {maxPages} pages.");
    }

    /// <summary>One resource of a collection, by its id.</summary>
    public Task<JsonApiResponse> GetOne(string collection, Guid id, CancellationToken cancellationToken = default)
    {
        return Send(HttpMethod.Get, ItemPath(collection, id), null, cancellationToken);
    }

    /// <summary>Makes a resource of a collection. The id the model holds is the id of the document, when it holds one.</summary>
    public Task<JsonApiResponse> Post(string collection, object model, CancellationToken cancellationToken = default)
    {
        return Send(HttpMethod.Post, collection, ResourceDocument(collection, model, null, null), cancellationToken);
    }

    /// <summary>An action of a route, such as linking a row, which takes no document: it is posted to its path.</summary>
    public Task<JsonApiResponse> Post(string path, CancellationToken cancellationToken = default)
    {
        return Send(HttpMethod.Post, path, null, cancellationToken);
    }

    /// <summary>
    /// An action that takes several resources in one request, such as sending a group of Snapshots: each is a resource of its
    /// type and its id, with the attributes its model holds, and the answer says what became of each (ADR-0008).
    /// </summary>
    public Task<JsonApiResponse> PostMany(
        string path,
        IEnumerable<(string Type, string? Id, object Model)> resources,
        CancellationToken cancellationToken = default
    )
    {
        var data = new JsonArray(
            resources.Select(x => (JsonNode?)ResourceData(x.Type, x.Model, x.Id, null)).ToArray()
        );
        var document = JsonSerializer.SerializeToElement(new JsonObject { ["data"] = data });
        return Send(HttpMethod.Post, path, document, cancellationToken);
    }

    /// <summary>
    /// Changes the members of a resource that the model holds. <paramref name="meta"/> carries what the change is based on,
    /// such as the version of a resource that counts its writes.
    /// </summary>
    public Task<JsonApiResponse> Patch(
        string collection,
        Guid id,
        object model,
        JsonObject? meta = null,
        CancellationToken cancellationToken = default
    )
    {
        return Send(
            HttpMethod.Patch,
            ItemPath(collection, id),
            ResourceDocument(collection, model, id.ToString(), meta),
            cancellationToken
        );
    }

    /// <summary>
    /// Changes a resource that is not addressed as <c>collection/{id}</c>: a singleton such as the profile of the caller, or a
    /// row whose id is text. The path says where it is, and the type and the id say what the document is: a singleton has
    /// no id, and none is written.
    /// </summary>
    public Task<JsonApiResponse> Patch(
        string path,
        string type,
        string? id,
        object model,
        JsonObject? meta = null,
        CancellationToken cancellationToken = default
    )
    {
        return Send(HttpMethod.Patch, path, ResourceDocument(type, model, id, meta), cancellationToken);
    }

    /// <summary>Removes a resource of a collection, by its id.</summary>
    public Task<JsonApiResponse> Delete(string collection, Guid id, CancellationToken cancellationToken = default)
    {
        return Send(HttpMethod.Delete, ItemPath(collection, id), null, cancellationToken);
    }

    /// <summary>Removes what a path addresses, such as the current session, which is not a row of a collection.</summary>
    public Task<JsonApiResponse> Delete(string path, CancellationToken cancellationToken = default)
    {
        return Send(HttpMethod.Delete, path, null, cancellationToken);
    }

    async Task<JsonApiResponse> Send(
        HttpMethod method,
        string endpoint,
        JsonElement? document = null,
        CancellationToken cancellationToken = default
    )
    {
        using var request = new HttpRequestMessage(method, BuildUrl(endpoint));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(MEDIA_TYPE));
        if (CultureInfo.CurrentUICulture.Name is { Length: > 0 } language)
        {
            // The words and the names the Api tells are in the language the app is in, which is not always the browser's.
            request.Headers.AcceptLanguage.TryParseAdd(language); // a name that is no header value is left out, and does not fail every request
        }

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
            response.IsSuccessStatusCode ? null : ErrorOf(response.StatusCode, parsed),
            response.Headers.Date,
            HeadersOf(response)
        );
    }

    static string ItemPath(string collection, Guid id)
    {
        return $"{collection}/{id}";
    }

    /// <summary>
    /// The document of one resource: its type is the collection, its id is the one given or else the one the model holds,
    /// and every other member of the model is an attribute. The id is never an attribute, and a null is written as null.
    /// An id that is empty is not written at all.
    /// </summary>
    JsonElement ResourceDocument(string collection, object model, string? id, JsonObject? meta)
    {
        return JsonSerializer.SerializeToElement(
            new JsonObject { ["data"] = ResourceData(collection, model, id, meta) }
        );
    }

    /// <summary>One resource of a document, as <see cref="ResourceDocument"/> writes it, without the document around it.</summary>
    JsonObject ResourceData(string type, object model, string? id, JsonObject? meta)
    {
        var attributes = new JsonObject();
        string? modelId = null;
        foreach (var member in JsonSerializer.SerializeToElement(model, Json).EnumerateObject())
        {
            if (member.Name == "id")
            {
                modelId = member.Value.GetString();
            }
            else
            {
                attributes[member.Name] = JsonNode.Parse(member.Value.GetRawText());
            }
        }

        var data = new JsonObject { ["type"] = type };
        if ((id ?? modelId) is { Length: > 0 } documentId && documentId != Guid.Empty.ToString())
        {
            data["id"] = documentId;
        }

        data["attributes"] = attributes;
        if (meta != null)
        {
            data["meta"] = meta;
        }

        return data;
    }

    static IReadOnlyDictionary<string, string> HeadersOf(HttpResponseMessage response)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in response.Headers.Concat(response.Content.Headers))
        {
            headers[header.Key] = string.Join(", ", header.Value);
        }

        return headers;
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
}
