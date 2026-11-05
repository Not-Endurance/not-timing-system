using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NC.Application.Authentication.User;
using NC.Application.Configurations;
using NC.Blazor.Client.Authentication.Services;
using NC.BLazor.Client.Browser;

namespace NC.Blazor.Client.Authentication;

public static class AuthenticationExtensions
{
    /// <summary>The browser's signed-in state comes from the server's session cookie. There is no client-side sign-in library.</summary>
    public static IServiceCollection AddNClientAuthentication(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddAuthorizationCore();
        services.AddScoped<SessionAuthenticationStateProvider>();
        services.AddScoped<AuthenticationStateProvider>(provider =>
            provider.GetRequiredService<SessionAuthenticationStateProvider>()
        );

        return services
            .AddScoped<NUserResolver>()
            .AddTransient<IBrowserLocalStorage, BrowserLocalStorage>();
    }
}
