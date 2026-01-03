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
    public static void RegisterNAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IAuthenticationSettings, NAuthenticationSettings>();
        services.AddSingleton<IUserResolver, NUserResolver>();
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));
        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
            })
            .AddCookie()
            .AddGoogleAuth(configuration);
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
                OnTicketReceived = context =>
                {
                    var userResolver = context.HttpContext.RequestServices.GetRequiredService<IUserResolver>();
                    return userResolver.Resolve(context);
                },
            };
        });

        return authBuilder;
    }
}
