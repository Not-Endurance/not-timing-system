using Microsoft.AspNetCore.Http;

namespace NoTiming.Api.Application.Authentication.Sessions;

/// <summary>The one shape of the session cookie: host-only, Secure, HttpOnly, SameSite=Lax, at path /, expiring with the session.</summary>
public static class SessionCookie
{
    public static CookieOptions Options(DateTimeOffset expiresUtc)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Domain = null,
            IsEssential = true,
            Expires = expiresUtc,
        };
    }
}
