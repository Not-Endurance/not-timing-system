using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NC.Server.Authentication.Identity;
using NC.Server.Authentication.Settings;

namespace NC.Server.Authentication.Sessions;

/// <summary>
/// The one place a verified user becomes signed in: the session row, its token, its expiry and the cookie. Both sign-in
/// paths end here, so they cannot disagree about what a session is, and so they log a sign-in the same way.
/// </summary>
internal sealed class SignInCompletion
{
    readonly ISessionRepository _sessions;
    readonly IOptions<AuthenticationSettings> _settings;
    readonly TimeProvider _time;
    readonly ILogger<SignInCompletion> _logger;

    public SignInCompletion(
        ISessionRepository sessions,
        IOptions<AuthenticationSettings> settings,
        TimeProvider time,
        ILogger<SignInCompletion> logger
    )
    {
        _sessions = sessions;
        _settings = settings;
        _time = time;
        _logger = logger;
    }

    /// <summary>Starts a session for a verified user, and writes its cookie. A user without a security stamp is refused.</summary>
    public async Task<SignInOutcome> CompleteAsync(
        HttpContext httpContext,
        IdentityUserDocument user,
        CancellationToken cancellationToken
    )
    {
        if (user.SecurityStamp is null)
        {
            return new SignInOutcome { SignedIn = false };
        }

        var now = _time.GetUtcNow();
        var issued = SessionToken.Issue();
        await _sessions.Create(issued.TokenHash, user.Id, user.SecurityStamp, now, cancellationToken);
        httpContext.Response.Cookies.Append(
            _settings.Value.CookieName,
            issued.Token,
            SessionCookie.Options(SessionRules.ExpiresAt(now))
        );
        _logger.LogInformation("User {UserId} signed in.", user.Id);

        return new SignInOutcome { SignedIn = true, User = AuthenticatedUser.From(user) };
    }
}
