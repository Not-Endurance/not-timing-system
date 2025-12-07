using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Authentication.User;

namespace Not.Authentication;

public static class AuthenticationBuilderExtensions
{
    public static void RegisterServices(
        this AuthenticationBuilder authBuilder
    )
    {
        authBuilder.Services.AddSingleton<IUserDeserializer, NAuthenticationSettings>();
        authBuilder.Services.AddSingleton<IUserResolver, NUserResolver>();
    }


    public static AuthenticationBuilder AddGoogleAuth(
        this AuthenticationBuilder authBuilder,
        IConfiguration configuration
    )
    {
        authBuilder.RegisterServices();

        authBuilder.AddGoogle(options =>
        {
            options.ClientId = configuration["Authentication:Google:ClientId"]!;
            options.ClientSecret = configuration["Authentication:Google:ClientSecret"]!;
            options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");

            options.Events = new OAuthEvents
            {
                OnTicketReceived = context =>
                {
                    var userDeserializer = context.HttpContext.RequestServices.GetRequiredService<IUserDeserializer>();
                    var userResolver = context.HttpContext.RequestServices.GetRequiredService<IUserResolver>();
                    var allowedUsersByEmail = userDeserializer.GetAllowedUsers(configuration);
                    return userResolver.UserResolution(context, allowedUsersByEmail);
                },
            };
        });

        return authBuilder;
    }

    public static bool EndsWithProviderSuffix(this string email, string[] suffixes)
    {
        foreach (var suffix in suffixes)
        {
            if (email.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
