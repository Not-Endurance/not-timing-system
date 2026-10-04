using System.Text;
using Not.Serialization.JSON;

namespace NTS.Tests.Integration.Infrastructure;

/// <summary>A request to the Functions API for a route that <c>FunctionsApiDriver</c> has no method for.</summary>
internal static class FunctionsRequests
{
    public static async Task Send(Uri baseUrl, HttpMethod method, string endpoint, object? payload = null)
    {
        using var client = new HttpClient { BaseAddress = baseUrl };
        using var request = new HttpRequestMessage(method, endpoint);
        if (payload != null)
        {
            request.Content = new StringContent(payload.ToJson(), Encoding.UTF8, "application/json");
        }

        using var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode,
            $"{method} {endpoint} answered {(int)response.StatusCode}: {content}"
        );
    }
}
