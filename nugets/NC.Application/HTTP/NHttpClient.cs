using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Not.Serialization.JSON;
using Not.Structures;

namespace Not.Application.HTTP;

public class NHttpClient
{
    readonly string _baseUrl;
    readonly HttpClient _httpClient;
    readonly ILogger<NHttpClient> _logger;

    public NHttpClient(
        IHttpClientFactory httpClientFactory,
        ILogger<NHttpClient> logger,
        IOptions<NHttpSettings> options
    )
    {
        _baseUrl = options.Value.Url!;
        _httpClient = httpClientFactory.CreateClient(nameof(NHttpClient));
        _logger = logger;
    }

    public async Task<Result<T>> Get<T>(string endpoint)
        where T : class
    {
        return await SendRequest<T>(HttpMethod.Get, endpoint);
    }

    public async Task<Result<Result.Empty>> Delete(string endpoint)
    {
        return await SendRequest<Result.Empty>(HttpMethod.Delete, endpoint);
    }

    public async Task<Result<Result.Empty>> Delete(string endpoint, object payload)
    {
        return await SendRequest<Result.Empty>(HttpMethod.Delete, endpoint, payload);
    }

    public async Task<Result<T>> Post<T>(string endpoint, T payload)
        where T : class
    {
        return await SendRequest<T>(HttpMethod.Post, endpoint, payload);
    }

    public async Task<Result<TResult>> Post<TResult>(string endpoint, object payload)
        where TResult : class
    {
        return await SendRequest<TResult>(HttpMethod.Post, endpoint, payload);
    }

    public async Task<NHttpResponseContent> PostContent(
        string endpoint,
        object payload,
        CancellationToken cancellationToken = default
    )
    {
        return await SendContentRequest(HttpMethod.Post, endpoint, payload, cancellationToken);
    }

    public async Task<Result<T>> Patch<T>(string endpoint, T payload)
        where T : class
    {
        return await SendRequest<T>(HttpMethod.Patch, endpoint, payload);
    }

    public async Task<Result<TResult>> Patch<TResult>(string endpoint, object payload)
        where TResult : class
    {
        return await SendRequest<TResult>(HttpMethod.Patch, endpoint, payload);
    }

    Uri BuildUrl(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(_baseUrl))
        {
            throw new InvalidOperationException("NHttpSettings.Url is required to send HTTP requests.");
        }

        return new Uri($"{HttpHelper.NormalizeUri(_baseUrl)}/{HttpHelper.NormalizeUri(endpoint)}");
    }

    async Task<Result<TResult>> SendRequest<TResult>(HttpMethod method, string endpoint, object? payload = null)
        where TResult : class
    {
        var content = await SendRequestCore(method, endpoint, payload);
        if (string.IsNullOrWhiteSpace(content))
        {
            return Result.Success<TResult>(null!);
        }

        return content.FromJson<Result<TResult>>();
    }

    async Task<string> SendRequestCore(HttpMethod method, string endpoint, object? payload = null)
    {
        var url = BuildUrl(endpoint);

        try
        {
            using var request = CreateRequest(method, url, payload);
            using var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw CreateUnhandledResponseException(response, content);
            }

            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during {Method} request to {Url}", method, url);
            throw;
        }
    }

    async Task<NHttpResponseContent> SendContentRequest(
        HttpMethod method,
        string endpoint,
        object? payload,
        CancellationToken cancellationToken
    )
    {
        var url = BuildUrl(endpoint);

        try
        {
            using var request = CreateRequest(method, url, payload);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw CreateUnhandledResponseException(response, Encoding.UTF8.GetString(content));
            }

            return new NHttpResponseContent(
                content,
                response.Content.Headers.ContentType?.MediaType,
                ResolveFileName(response.Content.Headers.ContentDisposition)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during {Method} request to {Url}", method, url);
            throw;
        }
    }

    static HttpRequestMessage CreateRequest(HttpMethod method, Uri url, object? payload = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (payload != null)
        {
            request.Content = new StringContent(payload.ToJson(), Encoding.UTF8, "application/json");
        }

        return request;
    }

    static Exception CreateUnhandledResponseException(HttpResponseMessage response, string responseContent)
    {
        var requestMethod = response.RequestMessage?.Method.Method ?? "HTTP";
        var requestUri = response.RequestMessage?.RequestUri?.ToString() ?? "unknown endpoint";
        var message =
            $"{requestMethod} {requestUri} failed with status code {(int)response.StatusCode} ({response.ReasonPhrase}).";
        if (!string.IsNullOrWhiteSpace(responseContent))
        {
            message = $"{message} {responseContent}";
        }

        return new HttpRequestException(message, null, response.StatusCode);
    }

    static string? ResolveFileName(ContentDispositionHeaderValue? contentDisposition)
    {
        var fileName = contentDisposition?.FileNameStar ?? contentDisposition?.FileName;
        return string.IsNullOrWhiteSpace(fileName) ? null : fileName.Trim('"');
    }
}
