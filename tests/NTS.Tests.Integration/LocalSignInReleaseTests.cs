#if !DEBUG
using System.Net;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.LocalSignInHarness;

namespace NTS.Tests.Integration;

/// <summary>
/// What a Release host does with the local sign in as (#607) when it is told to have it: nothing. CI builds and tests in
/// Release, so this is the assertion over the host that is deployed. The tests of the route itself are
/// LocalSignInTests, which exist in a Debug build.
/// </summary>
public sealed class LocalSignInReleaseTests : IClassFixture<MongoFixture>
{
    readonly MongoFixture _mongo;

    public LocalSignInReleaseTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task A_Release_host_has_no_route_and_no_page_even_in_Development_with_an_allow_list()
    {
        var email = UserSeed.NewEmail("local");
        await UserSeed.AddLegacyUserAsync(_mongo.ConnectionString, email);
        await SetMarkerAsync(_mongo.ConnectionString, "Staging");
        await using var api = ApiOver(_mongo.ConnectionString, email);
        using var client = api.CreateClient();

        var post = await SignInAsAsync(client, email);
        var page = await GetPageAsync(client);

        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
        Assert.DoesNotContain("sign-in-as-page", await page.Content.ReadAsStringAsync());
        Assert.Null(SessionCookie.From(post));
    }
}
#endif
