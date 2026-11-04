using NoTiming.IntegrationTests.Infrastructure;

namespace NoTiming.IntegrationTests;

public sealed class NoTimingCorsTests : IClassFixture<NoTimingIntegrationFixture>
{
    // Any localhost origin passes NoTiming's CORS rule. No client has to be running for this preflight.
    const string LOCAL_CLIENT_ORIGIN = "http://localhost:7077";

    readonly NoTimingIntegrationFixture _fixture;

    public NoTimingCorsTests(NoTimingIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task Preflight_AllowsRestMethodFromLocalUiOrigin(string method)
    {
        using var http = new HttpClient { BaseAddress = _fixture.NoTimingBaseUrl };
        using var request = new HttpRequestMessage(HttpMethod.Options, "api/countries");
        request.Headers.TryAddWithoutValidation("Origin", LOCAL_CLIENT_ORIGIN);
        request.Headers.TryAddWithoutValidation("Access-Control-Request-Method", method);
        request.Headers.TryAddWithoutValidation("Access-Control-Request-Headers", "content-type");

        using var response = await http.SendAsync(request);

        Assert.True(response.IsSuccessStatusCode, $"Preflight returned {(int)response.StatusCode}.");
        Assert.Equal(LOCAL_CLIENT_ORIGIN, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Contains(method, response.Headers.GetValues("Access-Control-Allow-Methods").Single());
    }
}
