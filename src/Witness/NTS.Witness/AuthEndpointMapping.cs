using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace NTS.Witness;

public static class AuthEndpointMapping
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        // Endpoint that triggers Google challenge
        app.MapGet(
            "/signin",
            async (HttpContext ctx) =>
            {
                await ctx.ChallengeAsync(
                    GoogleDefaults.AuthenticationScheme,
                    new AuthenticationProperties { RedirectUri = "/profile" }
                );
            }
        );

        // Endpoint to sign out
        app.MapGet(
            "/signout",
            async (HttpContext ctx) =>
            {
                await ctx.SignOutAsync();
                ctx.Response.Redirect("/profile");
            }
        );
    }
}
