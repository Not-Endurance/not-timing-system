using System.Net;
using System.Text.Json;
using NoTiming.IntegrationTests.Authentication.Harness;
using NoTiming.IntegrationTests.Infrastructure;
using static NoTiming.IntegrationTests.Authentication.Harness.AccountTestData;

namespace NoTiming.IntegrationTests.Authentication;

/// <summary>
/// Adding a passkey for another device to an account that already has one, as the Passkeys page offers. Kept apart from
/// <see cref="PasskeyTests"/>: each class starts its own database, and registrations are limited per address per hour,
/// so this check needs a budget of its own.
/// </summary>
public sealed class PasskeySecondEnrolmentTests : IClassFixture<NoTimingIntegrationFixture>, IDisposable
{
    const string RelyingPartyId = "localhost";

    readonly AccountClient _client;
    readonly MongoAccess _mongo;
    readonly string _origin;

    public PasskeySecondEnrolmentTests(NoTimingIntegrationFixture fixture)
    {
        _client = new AccountClient(fixture.NoTimingBaseUrl, fixture.NoTimingPort);
        _mongo = new MongoAccess(fixture.MongoConnectionString);
        _origin = $"http://app.localhost:{fixture.NoTimingPort}";
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    [Fact]
    public async Task Enrol_WhenTheAccountAlreadyHasAPasskey_ShouldStoreAnotherPasskey()
    {
        var cookie = await RegisterAndSignInAsync(_client, _mongo, Address("second-enrol"));
        await EnrolAsync(cookie, "Phone");

        await EnrolAsync(cookie, "Laptop");

        var list = await _client.ListPasskeysAsync(cookie);
        using var json = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        var names = json.RootElement.EnumerateArray().Select(x => x.GetProperty("name").GetString()).ToArray();
        Assert.Equal(2, names.Length);
        Assert.Contains("Phone", names);
        Assert.Contains("Laptop", names);
    }

    /// <summary>Each passkey comes from its own authenticator, as each device has its own key.</summary>
    async Task EnrolAsync(string cookie, string name)
    {
        using var authenticator = new SoftwareAuthenticator();
        var options = await _client.PasskeyEnrolOptionsAsync(cookie);
        var credential = authenticator.Register(await options.Content.ReadAsStringAsync(), _origin, RelyingPartyId);

        var enrolled = await _client.EnrolPasskeyAsync(Join(cookie, AccountClient.CookiesOf(options)), name, credential);

        Assert.Equal(HttpStatusCode.Created, enrolled.StatusCode);
    }

    static string Join(string cookie, string ceremony)
    {
        return ceremony.Length == 0 ? cookie : $"{cookie}; {ceremony}";
    }
}
