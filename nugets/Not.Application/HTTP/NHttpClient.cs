using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Not.Serialization.JSON;

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
        _httpClient = httpClientFactory.CreateClient("NHttpClient");
        _logger = logger;
    }

    public async Task<string?> Get(string endpoint)
    {
        var url = BuildUrl(endpoint);
        var response = await _httpClient.GetAsync(url);
        return await ReadResponse(response);
    }

    public async Task<T?> GetJson<T>(string endpoint)
        where T : class
    {
        var contents = await Get(endpoint);
        if (string.IsNullOrWhiteSpace(contents))
        {
            return null;
        }
        return contents.FromJson<T>();
    }

    public async Task<string> Delete(string endpoint)
    {
        var url = BuildUrl(endpoint);
        try
        {
            var response = await _httpClient.DeleteAsync(url);
            return await ReadResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during DELETE request to {Url}", url);
            throw;
        }
    }

    public async Task<string> Post<T>(string endpoint, T payload)
        where T : class
    {
        return await SendPayload(HttpMethod.Post, endpoint, payload);
    }

    public async Task<string> Patch<T>(string endpoint, T payload)
        where T : class
    {
        return await SendPayload(HttpMethod.Patch, endpoint, payload);
    }

    async Task<string> SendPayload<T>(HttpMethod method, string endpoint, T payload)
        where T : class
    {
        var url = BuildUrl(endpoint);
        try
        {
            var content = payload.ToJson();
            using var request = new HttpRequestMessage(method, url)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json"),
            };

            using var response = await _httpClient.SendAsync(request);
            return await ReadResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during {Method} request to {Url}", method, url);
            throw;
        }
    }

    async Task<string> ReadResponse(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    Uri BuildUrl(string endpoint)
    {
        return new Uri($"{_baseUrl}/{endpoint}");
    }
}
