using System.Buffers.Text;
using System.Net;
using System.Text.Json;
using NoTiming.IntegrationTests.Authentication.Harness;
using NoTiming.IntegrationTests.Infrastructure;
using static NoTiming.IntegrationTests.Authentication.Harness.AccountTestData;

namespace NoTiming.IntegrationTests.Authentication;

/// <summary>
/// Passkey options, enrolment and sign-in with a passkey. Enrolment and sign-in run the real WebAuthn ceremony through a
/// software authenticator, so the server runs its own checks. The list, rename and removal tests seed passkeys directly.
/// </summary>
public sealed class PasskeyTests : IClassFixture<NoTimingIntegrationFixture>, IDisposable
{
    const string RelyingPartyId = "localhost";

    readonly AccountClient _client;
    readonly MongoAccess _mongo;
    readonly string _origin;

    public PasskeyTests(NoTimingIntegrationFixture fixture)
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
    public async Task EnrolOptions_WhenSignedIn_ShouldRequireAResidentKeyUserVerificationAndNoAttestation()
    {
        var cookie = await RegisterAndSignInAsync(_client, _mongo, Address("options"));

        var options = await _client.PasskeyEnrolOptionsAsync(cookie);

        Assert.Equal(HttpStatusCode.OK, options.StatusCode);
        using var json = JsonDocument.Parse(await options.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal("localhost", root.GetProperty("rp").GetProperty("id").GetString());
        Assert.Equal("none", root.GetProperty("attestation").GetString());
        var selection = root.GetProperty("authenticatorSelection");
        Assert.Equal("required", selection.GetProperty("residentKey").GetString());
        Assert.Equal("required", selection.GetProperty("userVerification").GetString());
    }

    [Fact]
    public async Task EnrolOptions_WhenSignedIn_ShouldUseARandomUserHandleNotTheIntegerId()
    {
        var email = Address("handle");
        var cookie = await RegisterAndSignInAsync(_client, _mongo, email);
        var userId = await _mongo.UserIdByEmailAsync(email);

        var options = await _client.PasskeyEnrolOptionsAsync(cookie);

        using var json = JsonDocument.Parse(await options.Content.ReadAsStringAsync());
        var handle = json.RootElement.GetProperty("user").GetProperty("id").GetString()!;
        var decoded = Base64Url.DecodeFromChars(handle);
        Assert.NotEqual(userId.ToString(), handle);
        Assert.DoesNotContain(userId.ToString(), System.Text.Encoding.UTF8.GetString(decoded));
        Assert.InRange(decoded.Length, 16, 64);
    }

    [Fact]
    public async Task EnrolOptions_WhenSignedOut_ShouldBeUnauthorized()
    {
        var options = await _client.PasskeyEnrolOptionsAsync(cookie: string.Empty);

        Assert.Equal(HttpStatusCode.Unauthorized, options.StatusCode);
    }

    [Fact]
    public async Task SignInOptions_ShouldBeAnonymousAndNameTheRelyingParty()
    {
        var options = await _client.PasskeySignInOptionsAsync();

        Assert.Equal(HttpStatusCode.OK, options.StatusCode);
        using var json = JsonDocument.Parse(await options.Content.ReadAsStringAsync());
        Assert.Equal("localhost", json.RootElement.GetProperty("rpId").GetString());
    }

    [Fact]
    public async Task SignInWithPasskey_WhenTheAnswerIsNotAPasskey_ShouldAnswerUnauthorized()
    {
        var answer = await _client.PasskeySignInVerifyAsync(new { id = "not-a-passkey" });

        Assert.Equal(HttpStatusCode.Unauthorized, answer.StatusCode);
    }

    [Fact]
    public async Task ListPasskeys_WhenUserHasTwo_ShouldListBoth()
    {
        var email = Address("list");
        var cookie = await RegisterAndSignInAsync(_client, _mongo, email);
        var userId = await _mongo.UserIdByEmailAsync(email);
        var first = NewCredential();
        var second = NewCredential();
        await _mongo.SeedPasskeysAsync(userId, (first, "Phone"), (second, "Laptop"));

        var list = await _client.ListPasskeysAsync(cookie);

        using var json = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        var names = json.RootElement.EnumerateArray().Select(x => x.GetProperty("name").GetString()).ToArray();
        Assert.Equal(new[] { "Phone", "Laptop" }, names);
    }

    [Fact]
    public async Task RemovePasskey_WhenAnotherRemains_ShouldRemoveIt()
    {
        var email = Address("remove");
        var cookie = await RegisterAndSignInAsync(_client, _mongo, email);
        var userId = await _mongo.UserIdByEmailAsync(email);
        var first = NewCredential();
        var second = NewCredential();
        await _mongo.SeedPasskeysAsync(userId, (first, "Phone"), (second, "Laptop"));

        var removed = await _client.RemovePasskeyAsync(cookie, Base64Url.EncodeToString(first));

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(1, await CountPasskeysAsync(cookie));
    }

    [Fact]
    public async Task RemovePasskey_WhenItIsTheLastOne_ShouldRefuseWithConflict()
    {
        var email = Address("last");
        var cookie = await RegisterAndSignInAsync(_client, _mongo, email);
        var userId = await _mongo.UserIdByEmailAsync(email);
        var only = NewCredential();
        await _mongo.SeedPasskeysAsync(userId, (only, "Phone"));

        var refused = await _client.RemovePasskeyAsync(cookie, Base64Url.EncodeToString(only));

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal(1, await CountPasskeysAsync(cookie));
    }

    [Fact]
    public async Task RenamePasskey_ShouldChangeItsName()
    {
        var email = Address("rename");
        var cookie = await RegisterAndSignInAsync(_client, _mongo, email);
        var userId = await _mongo.UserIdByEmailAsync(email);
        var credential = NewCredential();
        await _mongo.SeedPasskeysAsync(userId, (credential, "Phone"), (NewCredential(), "Laptop"));

        var renamed = await _client.RenamePasskeyAsync(cookie, Base64Url.EncodeToString(credential), "Work phone");

        Assert.Equal(HttpStatusCode.NoContent, renamed.StatusCode);
        var list = await _client.ListPasskeysAsync(cookie);
        using var json = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.Contains(json.RootElement.EnumerateArray(), x => x.GetProperty("name").GetString() == "Work phone");
    }

    [Fact]
    public async Task RemovePasskey_WhenIdIsUnknown_ShouldBeNotFound()
    {
        var email = Address("unknown");
        var cookie = await RegisterAndSignInAsync(_client, _mongo, email);
        var userId = await _mongo.UserIdByEmailAsync(email);
        await _mongo.SeedPasskeysAsync(userId, (NewCredential(), "Phone"), (NewCredential(), "Laptop"));

        var missing = await _client.RemovePasskeyAsync(cookie, Base64Url.EncodeToString(NewCredential()));

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Enrol_WhenTheAuthenticatorAnswers_ShouldStoreThePasskeyForTheUser()
    {
        var cookie = await RegisterAndSignInAsync(_client, _mongo, Address("enrol"));
        using var authenticator = new SoftwareAuthenticator();
        var options = await _client.PasskeyEnrolOptionsAsync(cookie);
        var credential = authenticator.Register(await options.Content.ReadAsStringAsync(), _origin, RelyingPartyId);

        var enrolled = await _client.EnrolPasskeyAsync(
            Join(cookie, AccountClient.CookiesOf(options)),
            "Phone",
            credential
        );

        Assert.Equal(HttpStatusCode.Created, enrolled.StatusCode);
        Assert.Equal(new[] { "Phone" }, await PasskeyNamesAsync(cookie));
    }

    [Fact]
    public async Task SignInWithPasskey_WhenTheAuthenticatorAnswers_ShouldStartASession()
    {
        var cookie = await RegisterAndSignInAsync(_client, _mongo, Address("passkey-sign-in"));
        using var authenticator = new SoftwareAuthenticator();
        var enrolOptions = await _client.PasskeyEnrolOptionsAsync(cookie);
        var credential = authenticator.Register(
            await enrolOptions.Content.ReadAsStringAsync(),
            _origin,
            RelyingPartyId
        );
        await _client.EnrolPasskeyAsync(Join(cookie, AccountClient.CookiesOf(enrolOptions)), "Phone", credential);

        var signInOptions = await _client.PasskeySignInOptionsAsync();
        var answer = authenticator.Answer(await signInOptions.Content.ReadAsStringAsync(), _origin, RelyingPartyId);
        var verified = await _client.PasskeySignInVerifyAsync(
            JsonDocument.Parse(answer).RootElement.Clone(),
            AccountClient.CookiesOf(signInOptions)
        );

        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        Assert.True(await IsSignedInAsync(await _client.MeAsync(AccountClient.SessionCookieOf(verified))));
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

    static string Join(string cookie, string ceremony)
    {
        return ceremony.Length == 0 ? cookie : $"{cookie}; {ceremony}";
    }

    async Task<List<string>> PasskeyNamesAsync(string cookie)
    {
        var response = await _client.ListPasskeysAsync(cookie);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.EnumerateArray().Select(x => x.GetProperty("name").GetString()!).ToList();
    }
}
