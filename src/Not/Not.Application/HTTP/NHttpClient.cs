using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Logging;
using Not.Serialization;

namespace Not.Application.HTTP;

public class NHttpClient
{
    static readonly string HOST = "http://localhost:7019/api"; // new("https://nts-nexus-functions.azurewebsites.net/api"); // TODO: app configuration, environment

    readonly HttpClient _httpClient;
    readonly ILogger<NHttpClient> _logger;

    public NHttpClient(IHttpClientFactory httpClientFactory, ILogger<NHttpClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("NHttpClient");
        _logger = logger;
    }

    public async Task<string> Get(string endpoint)
    {
        var url = BuildUrl(endpoint);
        try
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await ReadResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during GET request to {Url}", url);
            throw;
        }
    }

    public async Task<string> Delete(string url)
    {
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
            var request = new HttpRequestMessage(method, url)
            {
                Content = new StringContent(payload.ToJson(), Encoding.UTF8, "application/json")
            };

            var response = await _httpClient.SendAsync(request);
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
        return new Uri($"{HOST}/{endpoint}");
    }
}
