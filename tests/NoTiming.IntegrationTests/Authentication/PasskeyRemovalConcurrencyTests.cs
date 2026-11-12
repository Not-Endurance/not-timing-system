using System.Buffers.Text;
using System.Net;
using System.Text.Json;
using NoTiming.IntegrationTests.Authentication.Harness;
using NoTiming.IntegrationTests.Infrastructure;
using static NoTiming.IntegrationTests.Authentication.Harness.AccountTestData;

namespace NoTiming.IntegrationTests.Authentication;

/// <summary>
/// Two removals of passkeys at the same time. Kept apart from <see cref="PasskeyTests"/>: each class starts its own
/// database, and registrations are limited per address per hour, so these tests need a budget of their own.
/// </summary>
public sealed class PasskeyRemovalConcurrencyTests : IClassFixture<NoTimingIntegrationFixture>, IDisposable
{
    readonly AccountClient _client;
    readonly MongoAccess _mongo;

    public PasskeyRemovalConcurrencyTests(NoTimingIntegrationFixture fixture)
    {
        _client = new AccountClient(fixture.NoTimingBaseUrl, fixture.NoTimingPort);
        _mongo = new MongoAccess(fixture.MongoConnectionString);
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    [Fact]
    public async Task RemovePasskey_WhenTheSamePasskeyIsRemovedAtOnce_ShouldRemoveItOnceAndAnswerNotFound()
    {
        var email = Address("race-same");
        var cookie = await RegisterAndSignInAsync(_client, _mongo, email);
        var userId = await _mongo.UserIdByEmailAsync(email);
        var doomed = NewCredential();
        await _mongo.SeedPasskeysAsync(userId, (doomed, "Phone"), (NewCredential(), "Laptop"));
        var id = Base64Url.EncodeToString(doomed);

        var answers = await Task.WhenAll(
            _client.RemovePasskeyAsync(cookie, id),
            _client.RemovePasskeyAsync(cookie, id)
        );

        var statuses = answers.Select(x => x.StatusCode).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { HttpStatusCode.NoContent, HttpStatusCode.NotFound }, statuses);
        Assert.Equal(1, await CountPasskeysAsync(cookie));
    }

    [Fact]
    public async Task RemovePasskey_WhenTwoPasskeysAreRemovedAtOnce_ShouldKeepOneAndRefuseTheOther()
    {
        var email = Address("race-two");
        var cookie = await RegisterAndSignInAsync(_client, _mongo, email);
        var userId = await _mongo.UserIdByEmailAsync(email);
        var first = NewCredential();
        var second = NewCredential();
        await _mongo.SeedPasskeysAsync(userId, (first, "Phone"), (second, "Laptop"));

        var answers = await Task.WhenAll(
            _client.RemovePasskeyAsync(cookie, Base64Url.EncodeToString(first)),
            _client.RemovePasskeyAsync(cookie, Base64Url.EncodeToString(second))
        );

        var statuses = answers.Select(x => x.StatusCode).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { HttpStatusCode.NoContent, HttpStatusCode.Conflict }, statuses);
        Assert.Equal(1, await CountPasskeysAsync(cookie));
    }

    async Task<int> CountPasskeysAsync(string cookie)
    {
        var list = await _client.ListPasskeysAsync(cookie);
        using var json = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        return json.RootElement.GetArrayLength();
    }

    static byte[] NewCredential()
    {
        return System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
    }
}
