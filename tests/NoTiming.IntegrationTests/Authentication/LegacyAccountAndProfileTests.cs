using System.Net;
using MongoDB.Bson;
using NoTiming.IntegrationTests.Authentication.Harness;
using NoTiming.IntegrationTests.Infrastructure;
using static NoTiming.IntegrationTests.Authentication.Harness.AccountTestData;

namespace NoTiming.IntegrationTests.Authentication;

/// <summary>
/// Existing users keep their id and roles when they first prove their address, the profile needs its required details,
/// and a code is refused after five attempts.
/// </summary>
public sealed class LegacyAccountAndProfileTests : IClassFixture<NoTimingIntegrationFixture>, IDisposable
{
    readonly AccountClient _client;
    readonly MongoAccess _mongo;

    public LegacyAccountAndProfileTests(NoTimingIntegrationFixture fixture)
    {
        _client = new AccountClient(fixture.NoTimingBaseUrl, fixture.NoTimingPort);
        _mongo = new MongoAccess(fixture.MongoConnectionString);
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    [Fact]
    public async Task SignIn_WhenLegacyAddressIsStoredInMixedCase_ShouldAdoptTheRow()
    {
        var lowerCase = Address("mixed-case");
        var mixedCase = lowerCase.Replace("mixed-case", "Mixed-Case");
        var userId = AccountTestData.NewId();
        await _mongo.SeedUserAsync(userId, mixedCase, proved: false, roles: ["Official"]);

        var code = await _client.RequestSignInCodeAsync(lowerCase);
        var accepted = code.StatusCode == HttpStatusCode.Accepted;
        var sentCode = await _mongo.WaitForCodeAsync(lowerCase, CodeTimeout);
        var verified = await _client.VerifyAsync(lowerCase, sentCode);

        Assert.True(accepted);
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        var adopted = await _mongo.ReadUserAsync(userId);
        Assert.Equal(userId, adopted!["_id"].AsInt32);
        Assert.Equal(lowerCase, adopted["Email"].AsString);
        Assert.Equal(new[] { "Official" }, adopted["Roles"].AsBsonArray.Select(x => x.AsString).ToArray());
    }

    [Fact]
    public async Task UpdateProfile_WhenCountryIsMissing_ShouldRefuseTheSave()
    {
        var cookie = await RegisterAndSignInAsync(_client, _mongo, Address("profile"));

        var refused = await _client.UpdateProfileAsync(
            cookie,
            new
            {
                givenName = "Ana",
                surname = "Ivanova",
                profile = new { countryIsoCode = "" },
            }
        );
        var saved = await _client.UpdateProfileAsync(
            cookie,
            new
            {
                givenName = "Ana",
                surname = "Ivanova",
                profile = new { countryIsoCode = "BG" },
            }
        );

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);
    }

    [Fact]
    public async Task SignIn_WhenLegacyUserHasAMixedCaseEmail_ShouldAdoptTheRowAndKeepItsIdAndRoles()
    {
        var id = NewId();
        var stored = "Legacy.Person." + Guid.NewGuid().ToString("N") + "@Example.test";
        var address = stored.ToLowerInvariant();
        await _mongo.SeedUserAsync(id, stored, proved: false, roles: ["Official"]);

        Assert.Equal(HttpStatusCode.Accepted, (await _client.RequestSignInCodeAsync(stored)).StatusCode);
        var code = await _mongo.WaitForCodeAsync(address, CodeTimeout);
        var verified = await _client.VerifyAsync(stored, code);

        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        var row = await _mongo.ReadUserAsync(id);
        Assert.NotNull(row);
        Assert.Equal(address, row!["Email"].AsString);
        Assert.True(row.Contains("ProvedAtUtc"));
        Assert.Equal("Official", row["Roles"].AsBsonArray[0].AsString);
    }

    [Fact]
    public async Task SignIn_WhenMoreThanOneRecordMatchesTheAddress_ShouldRefuseTheSignIn()
    {
        var address = "twin." + Guid.NewGuid().ToString("N") + "@example.test";
        await _mongo.SeedUserAsync(NewId(), address.ToUpperInvariant(), proved: false, roles: []);
        await _mongo.SeedUserAsync(NewId(), address, proved: false, roles: []);

        Assert.Equal(HttpStatusCode.Accepted, (await _client.RequestSignInCodeAsync(address)).StatusCode);
        var code = await _mongo.WaitForCodeAsync(address, CodeTimeout);
        var refused = await _client.VerifyAsync(address, code);

        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
    }

    [Fact]
    public async Task Verify_WhenFiveAttemptsAreUsedUp_ShouldRefuseTheCorrectCode()
    {
        var email = Address("attempts");
        Assert.Equal(HttpStatusCode.Accepted, (await _client.RegisterAsync(email)).StatusCode);
        var correct = await _mongo.WaitForCodeAsync(email, CodeTimeout);
        var wrong = correct == "000000" ? "000001" : "000000";

        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await _client.VerifyAsync(email, wrong)).StatusCode);
        }

        var sixth = await _client.VerifyAsync(email, correct);

        Assert.Equal(HttpStatusCode.Unauthorized, sixth.StatusCode);
    }
}
