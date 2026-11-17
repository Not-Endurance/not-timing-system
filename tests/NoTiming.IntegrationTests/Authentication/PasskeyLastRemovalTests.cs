using System.Buffers.Text;
using NoTiming.IntegrationTests.Authentication.Harness;
using NoTiming.IntegrationTests.Infrastructure;
using static NoTiming.IntegrationTests.Authentication.Harness.AccountTestData;

namespace NoTiming.IntegrationTests.Authentication;

/// <summary>
/// Removing a user's only passkey. Kept apart from <see cref="PasskeyTests"/>: each class starts its own database, and
/// registrations are limited per address per hour, so this check needs a budget of its own.
/// </summary>
public sealed class PasskeyLastRemovalTests : IClassFixture<NoTimingIntegrationFixture>, IDisposable
{
    readonly AccountClient _client;
    readonly MongoAccess _mongo;

    public PasskeyLastRemovalTests(NoTimingIntegrationFixture fixture)
    {
        _client = new AccountClient(fixture.NoTimingBaseUrl, fixture.NoTimingPort);
        _mongo = new MongoAccess(fixture.MongoConnectionString);
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    [Fact]
    public async Task RemovePasskey_WhenItIsTheLastOne_ShouldUnsetThePasskeysField()
    {
        var email = Address("last-unset");
        var cookie = await RegisterAndSignInAsync(_client, _mongo, email);
        var userId = await _mongo.UserIdByEmailAsync(email);
        var only = NewCredential();
        await _mongo.SeedPasskeysAsync(userId, (only, "Phone"));

        await _client.RemovePasskeyAsync(cookie, Base64Url.EncodeToString(only));

        var user = await _mongo.ReadUserAsync(userId);
        Assert.False(user!.Contains("Passkeys"));
    }

    static byte[] NewCredential()
    {
        return System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
    }
}
