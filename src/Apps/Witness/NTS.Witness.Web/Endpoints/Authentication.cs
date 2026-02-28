using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace NTS.Witness.Web.Endpoints;

public static class AuthEndpointMapping
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        // Endpoint that triggers configured challenge scheme.
        app.MapGet(
            "/signin",
            async (ctx) =>
            {
                await ctx.ChallengeAsync(new AuthenticationProperties { RedirectUri = "/profile" });
            }
        );

        // Endpoint to sign out locally and from remote identity provider.
        app.MapGet(
            "/signout",
            async (ctx) =>
            {
                var authOptions = ctx.RequestServices.GetRequiredService<IOptions<AuthenticationOptions>>().Value;

                await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                if (
                    !string.IsNullOrWhiteSpace(authOptions.DefaultChallengeScheme)
                    && !string.Equals(
                        authOptions.DefaultChallengeScheme,
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        StringComparison.Ordinal
                    )
                )
                {
                    await ctx.SignOutAsync(
                        authOptions.DefaultChallengeScheme,
                        new AuthenticationProperties { RedirectUri = "/profile" }
                    );
                    return;
                }

                ctx.Response.Redirect("/profile");
            }
        );
    }
}
