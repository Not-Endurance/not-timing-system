using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Builder;

namespace Not.Authentication;

public static class GmailAuth
{
    public static AuthenticationBuilder AddGmailAuth(this AuthenticationBuilder authBuilder, WebApplicationBuilder builder)
    {
        var authConfig = Auth.GetAuthConfigFromAppSettings(builder);
        var allowedUsersByEmail = authConfig
            .Users.Where(u => !string.IsNullOrWhiteSpace(u.Email))
            .ToDictionary(u => u.Email, StringComparer.OrdinalIgnoreCase);

        authBuilder.AddGoogle(options =>
        {
            options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
            options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
            options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");

            options.Events = new OAuthEvents
            {
                OnTicketReceived = context =>
                {
                    var email = context.Principal?.FindFirst(ClaimTypes.Email)?.Value;
                    var oldIdentity = (ClaimsIdentity)context.Principal!.Identity!;

                    var newIdentity = new ClaimsIdentity(
                        oldIdentity.Claims,
                        oldIdentity.AuthenticationType,
                        ClaimTypes.Name,
                        ClaimTypes.Role
                    );

                    // clear old identities and add new one
                    context.Principal = new ClaimsPrincipal(newIdentity);

                    // deny if no email / not gmail / not in allow list
                    if (
                        string.IsNullOrWhiteSpace(email)
                        || !email.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase)
                        || !allowedUsersByEmail.TryGetValue(email, out var authUser)
                    )
                    {
                        context.Response.Redirect("/access-denied");
                        context.Response.StatusCode = 403;
                        context.HandleResponse();
                        return Task.CompletedTask;
                    }

                    foreach (var role in authUser.Roles ?? [])
                    {
                        if (!string.IsNullOrWhiteSpace(role))
                        {
                            newIdentity!.AddClaim(new Claim(ClaimTypes.Role, role));
                        }
                    }

                    return Task.CompletedTask;
                },
            };
        });

        return authBuilder;
    }
}
