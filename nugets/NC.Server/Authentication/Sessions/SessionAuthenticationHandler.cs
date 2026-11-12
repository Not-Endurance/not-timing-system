using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NC.Server.Authentication.Identity;
using NC.Server.Authentication.Settings;

namespace NC.Server.Authentication.Sessions;

/// <summary>
/// Reads the session cookie on every request, including the hub negotiation and connection, and signs the user in when
/// the session is live and its user's security stamp has not changed. The cookie is extended at most once an hour.
/// </summary>
internal sealed class SessionAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SCHEME = "NoTimingSession";

    readonly ISessionRepository _sessions;
    readonly IIdentityUserRepository _users;
    readonly IOptions<AuthenticationSettings> _settings;
    readonly TimeProvider _time;

    public SessionAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory loggerFactory,
        UrlEncoder encoder,
        ISessionRepository sessions,
        IIdentityUserRepository users,
        IOptions<AuthenticationSettings> settings,
        TimeProvider time
    )
        : base(options, loggerFactory, encoder)
    {
        _sessions = sessions;
        _users = users;
        _settings = settings;
        _time = time;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var cookieName = _settings.Value.CookieName;
        if (!Request.Cookies.TryGetValue(cookieName, out var token) || string.IsNullOrEmpty(token))
        {
            return AuthenticateResult.NoResult();
        }

        var now = _time.GetUtcNow();
        var hash = SessionToken.HashOf(token);
        var session = await _sessions.FindLive(hash, now);
        if (session is null)
        {
            return AuthenticateResult.NoResult();
        }

        var user = await _users.FindByUserId(session.UserId);
        if (user is null || user.SecurityStamp != session.SecurityStamp)
        {
            await _sessions.Remove(hash);
            return AuthenticateResult.NoResult();
        }

        if (SessionRules.ShouldExtend(session.LastExtendedAtUtc, now))
        {
            await _sessions.Extend(hash, now);
            if (!Response.HasStarted)
            {
                Response.Cookies.Append(cookieName, token, SessionCookie.Options(SessionRules.ExpiresAt(now)));
            }
        }

        var identity = new ClaimsIdentity(Scheme.Name);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Name, user.DisplayName ?? user.Name ?? user.Email ?? string.Empty));
        if (user.Email != null)
        {
            identity.AddClaim(new Claim(ClaimTypes.Email, user.Email));
        }

        foreach (var role in user.Roles)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }
}
