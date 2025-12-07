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
        this AuthenticationBuilder authBuilder, IConfiguration configuration
    )
    {
        authBuilder.Services.AddSingleton<IAuthenticationSettings, NAuthenticationSettings>();
        authBuilder.Services.AddSingleton<IUserResolver, NUserResolver>();
        authBuilder.Services.Configure<AuthOptions>(
        configuration.GetSection(AuthOptions.SectionName));
    }

    public static AuthenticationBuilder AddGoogleAuth(
        this AuthenticationBuilder authBuilder,
        IConfiguration configuration
    )
    {
        authBuilder.RegisterServices(configuration);
        authBuilder.AddGoogle(options =>
        {
            options.ClientId = configuration["Authentication:Google:ClientId"]!;
            options.ClientSecret = configuration["Authentication:Google:ClientSecret"]!;
            options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
            options.Events = new OAuthEvents
            {
                OnTicketReceived = context =>
                {
                    var userResolver = context.HttpContext.RequestServices.GetRequiredService<IUserResolver>();
                    return userResolver.UserResolution(context);
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
