using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

public sealed class WarpCorsTests : IClassFixture<NtsIntegrationFixture>
{
    const string LOCAL_WITNESS_ORIGIN = "http://localhost:7077";

    readonly NtsIntegrationFixture _fixture;

    public WarpCorsTests(NtsIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task Preflight_AllowsRestMethodFromLocalWitnessOrigin(string method)
    {
        using var http = new HttpClient { BaseAddress = _fixture.WarpBaseUrl };
        using var request = new HttpRequestMessage(HttpMethod.Options, "api/countries");
        request.Headers.TryAddWithoutValidation("Origin", LOCAL_WITNESS_ORIGIN);
        request.Headers.TryAddWithoutValidation("Access-Control-Request-Method", method);
        request.Headers.TryAddWithoutValidation("Access-Control-Request-Headers", "content-type");

        using var response = await http.SendAsync(request);

        Assert.True(response.IsSuccessStatusCode, $"Preflight returned {(int)response.StatusCode}.");
        Assert.Equal(LOCAL_WITNESS_ORIGIN, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Contains(method, response.Headers.GetValues("Access-Control-Allow-Methods").Single());
    }
}
