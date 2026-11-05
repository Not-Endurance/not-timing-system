using System.Net;
using System.Text.Json;
using NoTiming.IntegrationTests.Authentication.Harness;
using NoTiming.IntegrationTests.Infrastructure;
using static NoTiming.IntegrationTests.Authentication.Harness.AccountTestData;

namespace NoTiming.IntegrationTests.Authentication;

/// <summary>
/// Sign-in, registration, the session cookie and sign-out over HTTP, against a real host and a real MongoDB. Codes are
/// read from the outbox, as a person would read them from their mail. This class sends at most nine codes, which keeps
/// it under the limit of ten per client address per hour.
/// </summary>
public sealed class AccountSignInTests : IClassFixture<NoTimingIntegrationFixture>, IDisposable
{
    readonly AccountClient _client;
    readonly MongoAccess _mongo;

    public AccountSignInTests(NoTimingIntegrationFixture fixture)
    {
        _client = new AccountClient(fixture.NoTimingBaseUrl, fixture.NoTimingPort);
        _mongo = new MongoAccess(fixture.MongoConnectionString);
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    [Fact]
    public async Task SignInCode_WhenAddressHasAnAccount_ShouldAnswerLikeAnUnknownAddress()
    {
        var known = Address("known");
        await _mongo.SeedUserAsync(NewId(), known, proved: true, roles: []);
        var unknown = Address("unknown");

        var knownAnswer = await _client.RequestSignInCodeAsync(known);
        var unknownAnswer = await _client.RequestSignInCodeAsync(unknown);

        Assert.Equal(HttpStatusCode.Accepted, knownAnswer.StatusCode);
        Assert.Equal(knownAnswer.StatusCode, unknownAnswer.StatusCode);
        Assert.Equal(await knownAnswer.Content.ReadAsStringAsync(), await unknownAnswer.Content.ReadAsStringAsync());
        Assert.False(unknownAnswer.Headers.Contains("Retry-After"));
        Assert.False(unknownAnswer.Headers.Contains("Set-Cookie"));
        await _mongo.WaitForCodeAsync(known, CodeTimeout);
        Assert.Equal(0, await _mongo.CountOutboxAsync(unknown));
    }

    [Fact]
    public async Task Register_WhenCodeIsCorrect_ShouldSetASessionCookieWithTheRequiredAttributes()
    {
        var email = Address("cookie");
        Assert.Equal(HttpStatusCode.Accepted, (await _client.RegisterAsync(email)).StatusCode);
        var code = await _mongo.WaitForCodeAsync(email, CodeTimeout);

        var verified = await _client.VerifyAsync(email, code);

        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        var cookie = AccountClient.SessionSetCookieOf(verified);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Me_WhenSignedIn_ShouldReportTheDisplayName()
    {
        var cookie = await RegisterAndSignInAsync(_client, _mongo, Address("me"));

        var me = await _client.MeAsync(cookie);

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using var json = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("signedIn").GetBoolean());
        Assert.Equal("Ana Ivanova", json.RootElement.GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task SignOut_WhenSignedIn_ShouldEndTheSession()
    {
        var cookie = await RegisterAndSignInAsync(_client, _mongo, Address("signout"));

        var signedOut = await _client.SignOutAsync(cookie);

        Assert.Equal(HttpStatusCode.NoContent, signedOut.StatusCode);
        Assert.False(await IsSignedInAsync(await _client.MeAsync(cookie)));
    }

    [Fact]
    public async Task SignOut_WhenRequestIsCrossSite_ShouldBeForbiddenAndKeepTheSession()
    {
        var cookie = await RegisterAndSignInAsync(_client, _mongo, Address("crosssite"));

        var refused = await _client.SignOutAsync(cookie, crossSite: true);

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.True(await IsSignedInAsync(await _client.MeAsync(cookie)));
    }

    [Fact]
    public async Task Verify_WhenCodeIsWrong_ShouldAnswerTheSameAsAMissingCode()
    {
        var email = Address("wrong");
        Assert.Equal(HttpStatusCode.Accepted, (await _client.RegisterAsync(email)).StatusCode);
        var correct = await _mongo.WaitForCodeAsync(email, CodeTimeout);
        var wrong = correct == "000000" ? "000001" : "000000";

        var wrongAnswer = await _client.VerifyAsync(email, wrong);
        var missingAnswer = await _client.VerifyAsync(email, null);

        Assert.Equal(HttpStatusCode.Unauthorized, wrongAnswer.StatusCode);
        Assert.Equal(missingAnswer.StatusCode, wrongAnswer.StatusCode);
        Assert.Equal(await missingAnswer.Content.ReadAsStringAsync(), await wrongAnswer.Content.ReadAsStringAsync());
        Assert.False(wrongAnswer.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Verify_WhenCodeWasAlreadyUsed_ShouldRefuseIt()
    {
        var email = Address("reuse");
        Assert.Equal(HttpStatusCode.Accepted, (await _client.RegisterAsync(email)).StatusCode);
        var code = await _mongo.WaitForCodeAsync(email, CodeTimeout);
        Assert.Equal(HttpStatusCode.OK, (await _client.VerifyAsync(email, code)).StatusCode);

        var again = await _client.VerifyAsync(email, code);

        Assert.Equal(HttpStatusCode.Unauthorized, again.StatusCode);
    }

    [Fact]
    public async Task Register_WhenHoneypotIsFilled_ShouldAnswerAcceptedAndSendNoEmail()
    {
        var email = Address("bot");

        var answer = await _client.RegisterAsync(email, website: "https://spam.example");

        Assert.Equal(HttpStatusCode.Accepted, answer.StatusCode);
        await Task.Delay(TimeSpan.FromSeconds(2));
        Assert.Equal(0, await _mongo.CountOutboxAsync(email));
    }

    [Fact]
    public async Task Register_WhenTheAddressIsCoolingDown_ShouldRefuseWithRetryAfter()
    {
        var email = Address("cooldown");
        Assert.Equal(HttpStatusCode.Accepted, (await _client.RegisterAsync(email)).StatusCode);

        var refused = await _client.RegisterAsync(email);

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        var retryAfter = refused.Headers.RetryAfter?.Delta;
        Assert.True(retryAfter is { } delta && delta > TimeSpan.Zero && delta <= TimeSpan.FromSeconds(60));
    }
}
