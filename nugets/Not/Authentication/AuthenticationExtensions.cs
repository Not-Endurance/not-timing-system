using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Authentication.User;

namespace Not.Authentication;

public static class AuthenticationExtensions
{
    public static void RegisterNAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<AuthenticationBuilder, IConfiguration>? providerConfiguration = null
    )
    {
        services.AddScoped<IAuthenticationSettings, NAuthenticationSettings>();
        services.AddSingleton<IUserResolver, NUserResolver>();
        var defaultChallengeScheme =
            configuration["Authentication:DefaultChallengeScheme"] ?? GoogleDefaults.AuthenticationScheme;
        var authBuilder = services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = defaultChallengeScheme;
            })
            .AddCookie();

        if (providerConfiguration == null)
        {
            authBuilder.AddGoogleAuth(configuration);
            return;
        }

        providerConfiguration(authBuilder, configuration);
    }

    public static AuthenticationBuilder AddGoogleAuth(
        this AuthenticationBuilder authBuilder,
        IConfiguration configuration
    )
    {
        authBuilder.AddGoogle(options =>
        {
            options.ClientId = configuration["Authentication:Google:ClientId"]!;
            options.ClientSecret = configuration["Authentication:Google:ClientSecret"]!;
            options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
            options.Events = new OAuthEvents
            {
                OnTicketReceived = ResolveTicketReceived,
            };
        });

        return authBuilder;
    }

    public static Task ResolveTicketReceived(TicketReceivedContext context)
    {
        var userResolver = context.HttpContext.RequestServices.GetRequiredService<IUserResolver>();
        return userResolver.Resolve(context);
    }
}
