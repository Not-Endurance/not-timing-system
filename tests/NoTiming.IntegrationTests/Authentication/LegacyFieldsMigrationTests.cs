using System.Net;
using System.Text.Json;
using NoTiming.IntegrationTests.Authentication.Harness;
using NoTiming.IntegrationTests.Infrastructure;
using static NoTiming.IntegrationTests.Authentication.Harness.AccountTestData;

namespace NoTiming.IntegrationTests.Authentication;

/// <summary>A row written with the profile fields at the top level is moved into its profile when the host starts.</summary>
public sealed class LegacyFieldsMigrationTests : IClassFixture<LegacyFieldsFixture>, IDisposable
{
    readonly AccountClient _client;
    readonly MongoAccess _mongo;

    public LegacyFieldsMigrationTests(LegacyFieldsFixture fixture)
    {
        _client = new AccountClient(fixture.NoTimingBaseUrl, fixture.NoTimingPort);
        _mongo = new MongoAccess(fixture.MongoConnectionString);
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    [Fact]
    public async Task Profile_WhenTheRowHasLegacyFields_ShouldMoveThemIntoTheProfile()
    {
        await WaitUntilMigratedAsync();
        var cookie = await SignInWithCodeAsync(_client, _mongo, LegacyFieldsFixture.Email);

        var response = await _client.ProfileAsync(cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var profile = json.RootElement.GetProperty("profile");
        Assert.Equal("Konarche", profile.GetProperty("club").GetString());
        Assert.Equal("20202020", profile.GetProperty("feiId").GetString());
        Assert.Equal("BG", profile.GetProperty("countryIsoCode").GetString());
    }

    [Fact]
    public async Task Migration_WhenTheRowHasLegacyFields_ShouldRemoveThem()
    {
        await WaitUntilMigratedAsync();

        var row = await _mongo.ReadUserAsync(LegacyFieldsFixture.UserId);

        Assert.NotNull(row);
        Assert.False(row.Contains("Club"));
        Assert.False(row.Contains("FeiId"));
        Assert.False(row.Contains("CountryRegion"));
    }

    async Task WaitUntilMigratedAsync()
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            var row = await _mongo.ReadUserAsync(LegacyFieldsFixture.UserId);
            if (row is not null && !row.Contains("Club"))
            {
                return;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException("The legacy profile fields were not moved into the profile.");
    }
}
