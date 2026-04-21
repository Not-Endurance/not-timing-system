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
        return new Uri($"{_baseUrl}/{endpoint}");
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
            using var request = new HttpRequestMessage(method, url);
            if (payload != null)
            {
                request.Content = new StringContent(payload.ToJson(), Encoding.UTF8, "application/json");
            }

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

    static Exception CreateUnhandledResponseException(HttpResponseMessage response, string responseContent)
    {
        _ = responseContent;

        var requestMethod = response.RequestMessage?.Method.Method ?? "HTTP";
        var requestUri = response.RequestMessage?.RequestUri?.ToString() ?? "unknown endpoint";
        var message =
            $"{requestMethod} {requestUri} failed with status code {(int)response.StatusCode} ({response.ReasonPhrase}).";

        return new HttpRequestException(message, null, response.StatusCode);
    }
}
