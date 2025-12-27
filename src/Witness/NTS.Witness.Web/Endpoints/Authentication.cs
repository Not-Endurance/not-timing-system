using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;

namespace NTS.Witness.Web.Endpoints;

public static class AuthEndpointMapping
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        // Endpoint that triggers Google challenge
        app.MapGet(
            "/signin",
            async (ctx) =>
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
            async (ctx) =>
            {
                await ctx.SignOutAsync();
                ctx.Response.Redirect("/profile");
            }
        );
    }
}
