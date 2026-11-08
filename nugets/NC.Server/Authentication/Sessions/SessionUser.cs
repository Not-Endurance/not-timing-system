using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace NC.Server.Authentication.Sessions;

/// <summary>Reads the signed-in user from the session cookie, through the session scheme, whatever the default scheme is.</summary>
public static class SessionUser
{
    public static async Task<int?> IdOfAsync(HttpContext context)
    {
        var result = await context.AuthenticateAsync(SessionAuthenticationHandler.SCHEME);
        if (!result.Succeeded || result.Principal is null)
        {
            return null;
        }

        return int.TryParse(result.Principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    }
}
