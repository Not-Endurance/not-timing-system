using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Application.Authentication.Abstractions;
using Not.Blazor;
using Not.Blazor.Client.Auth;

namespace Not.Blazor.Client;

public static class NBlazorClientServices
{
    public static IServiceCollection NClientSideBlazor(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddTransient<INAuthentication, BlazorClientSideAuthenticationService>();
        return services.AddNBlazor(configuration);
    }
}
