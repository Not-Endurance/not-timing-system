using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NC.Server.Authentication;
using NC.Server.Authentication.Account;
using NC.Server.Authentication.Codes;
using NC.Server.Authentication.Email;
using NC.Server.Authentication.Identity;
using NC.Server.Authentication.Passkeys;
using NC.Server.Authentication.RateLimits;
using NC.Server.Authentication.Sessions;
using NC.Server.Authentication.Settings;
using NC.Server.Authentication.SignIn;
using NC.Server.Tests.Authentication.Fakes;
using Xunit;

namespace NC.Server.Tests.Authentication;

/// <summary>Sign-in through the public interface. The users, sessions and codes are faked behind it.</summary>
public sealed class AuthenticationServiceTests
{
    const string CookieName = "__Host-session";
    const string Secret = "test-secret";
    const string Address = "ana@example.test";
    static readonly DateTimeOffset Now = new(2026, 11, 9, 10, 0, 0, TimeSpan.Zero);
    static readonly byte[] Credential = [1, 2, 3, 4];

    [Fact]
    public async Task SignInWithPasskey_WhenTheAnswerIsAccepted_ShouldStartASessionAndSetTheCookie()
    {
        var users = new FakeIdentityUsers(User(7));
        var ceremony = new FakePasskeyCeremony
        {
            Assertion = new PasskeyAssertion { User = users.Stored(7), Passkey = Passkey(Credential) },
        };
        var sessions = new FakeSessionRepository();
        var service = CreateService(users, sessions, ceremony);
        var http = new DefaultHttpContext();

        var outcome = await service.SignInWithPasskey(http, "{}", CancellationToken.None);

        Assert.True(outcome.SignedIn);
        Assert.Equal(7, outcome.User?.Id);
        Assert.Single(sessions.Created);
        Assert.Contains($"{CookieName}=", http.Response.Headers["Set-Cookie"].ToString());
    }

    [Fact]
    public async Task SignInWithPasskey_WhenTheUserHasNoSecurityStamp_ShouldRefuseAndStartNoSession()
    {
        var users = new FakeIdentityUsers(User(7, stamp: null));
        var ceremony = new FakePasskeyCeremony
        {
            Assertion = new PasskeyAssertion { User = users.Stored(7), Passkey = Passkey(Credential) },
        };
        var sessions = new FakeSessionRepository();
        var service = CreateService(users, sessions, ceremony);

        var outcome = await service.SignInWithPasskey(new DefaultHttpContext(), "{}", CancellationToken.None);

        Assert.False(outcome.SignedIn);
        Assert.Empty(sessions.Created);
    }

    [Fact]
    public async Task SignInWithCode_WhenTheCodeIsCorrect_ShouldStartASessionAndSetTheCookie()
    {
        var users = new FakeIdentityUsers(User(7));
        var challenges = new FakeCodeChallenges();
        challenges.Store(Challenge("123456"));
        var sessions = new FakeSessionRepository();
        var service = CreateService(users, sessions, challenges: challenges);
        var http = new DefaultHttpContext();

        var outcome = await service.SignInWithCode(http, Address, "123456", CancellationToken.None);

        Assert.True(outcome.SignedIn);
        Assert.Equal(7, outcome.User?.Id);
        Assert.Single(sessions.Created);
        Assert.Contains($"{CookieName}=", http.Response.Headers["Set-Cookie"].ToString());
    }

    [Fact]
    public async Task SignInWithCode_WhenTheCodeIsWrong_ShouldRefuseAndStartNoSession()
    {
        var users = new FakeIdentityUsers(User(7));
        var challenges = new FakeCodeChallenges();
        challenges.Store(Challenge("123456"));
        var sessions = new FakeSessionRepository();
        var service = CreateService(users, sessions, challenges: challenges);

        var outcome = await service.SignInWithCode(new DefaultHttpContext(), Address, "000000", CancellationToken.None);

        Assert.False(outcome.SignedIn);
        Assert.Empty(sessions.Created);
    }

    [Fact]
    public async Task SignInWithCode_WhenTheUserHasNoSecurityStamp_ShouldRepairTheStampAndSignIn()
    {
        var users = new FakeIdentityUsers(User(7, stamp: null));
        var challenges = new FakeCodeChallenges();
        challenges.Store(Challenge("123456"));
        var service = CreateService(users, new FakeSessionRepository(), challenges: challenges);

        var outcome = await service.SignInWithCode(new DefaultHttpContext(), Address, "123456", CancellationToken.None);

        Assert.True(outcome.SignedIn);
        Assert.NotNull(users.Stored(7).SecurityStamp);
    }

    [Fact]
    public async Task SignInWithCode_WhenTheRepairedStampCannotBeSaved_ShouldRefuseAndStartNoSession()
    {
        var users = new FakeIdentityUsers(User(7, stamp: null)) { FailUpdates = true };
        var challenges = new FakeCodeChallenges();
        challenges.Store(Challenge("123456"));
        var sessions = new FakeSessionRepository();
        var service = CreateService(users, sessions, challenges: challenges);

        var outcome = await service.SignInWithCode(new DefaultHttpContext(), Address, "123456", CancellationToken.None);

        Assert.False(outcome.SignedIn);
        Assert.Empty(sessions.Created);
    }

    [Fact]
    public async Task SignOut_WhenASessionCookieIsPresent_ShouldRemoveThatSession()
    {
        var sessions = new FakeSessionRepository();
        var service = CreateService(new FakeIdentityUsers(User(7)), sessions);
        var http = new DefaultHttpContext();
        http.Request.Headers.Cookie = $"{CookieName}=session-token";

        await service.SignOut(http, CancellationToken.None);

        Assert.Equal(SessionToken.HashOf("session-token"), Assert.Single(sessions.Removed));
    }

    static AuthenticationService CreateService(
        FakeIdentityUsers users,
        FakeSessionRepository sessions,
        FakePasskeyCeremony? ceremony = null,
        FakeCodeChallenges? challenges = null
    )
    {
        var settings = Options.Create(new AuthenticationSettings { CookieName = CookieName, CodeSecret = Secret });
        var time = new FixedTime(Now);
        var mail = new EmailDispatchQueue();
        var challengeStore = challenges ?? new FakeCodeChallenges();
        var languages = new EmailLanguage(["en"]);

        var passkeys = new PasskeyService(
            ceremony ?? new FakePasskeyCeremony(),
            users,
            mail,
            languages,
            time,
            NullLogger<PasskeyService>.Instance
        );
        var signIn = new AccountSignInService(
            users,
            challengeStore,
            new CodeSendGate(new FakeRateCounters(), challengeStore),
            mail,
            [],
            settings,
            time,
            NullLogger<AccountSignInService>.Instance
        );
        var completion = new SignInCompletion(sessions, settings, time, NullLogger<SignInCompletion>.Instance);
        return new AuthenticationService(
            passkeys,
            signIn,
            new AccountService(users, sessions),
            completion,
            languages,
            settings
        );
    }

    static IdentityUserDocument User(int id, string? stamp = "stamp")
    {
        return new IdentityUserDocument
        {
            Id = id,
            Email = Address,
            SecurityStamp = stamp,
            ProvedAtUtc = Now.UtcDateTime,
        };
    }

    static CodeChallengeDocument Challenge(string code)
    {
        return new CodeChallengeDocument
        {
            Email = Address,
            Purpose = CodeChallengePurpose.SIGN_IN,
            Mac = CodeMac.Compute(Encoding.UTF8.GetBytes(Secret), Address, code),
            AttemptsLeft = 5,
            LastSentAtUtc = Now.UtcDateTime,
            ExpiresAtUtc = Now.AddMinutes(10).UtcDateTime,
        };
    }

    static UserPasskeyInfo Passkey(byte[] credentialId)
    {
        return new UserPasskeyInfo(credentialId, [5, 6], Now, 0, [], true, false, false, [], []) { Name = "Phone" };
    }
}
