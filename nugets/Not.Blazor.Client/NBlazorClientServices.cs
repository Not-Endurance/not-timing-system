using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Application.Authentication.Abstractions;
using Not.Blazor.Client.Authentication;
using Not.Blazor.Client.Authentication.Services;

namespace Not.Blazor.Client;

public static class NBlazorClientServices
{
    public static IServiceCollection NClientSideBlazor(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddTransient<INAuthentication, NBlazorClientAuthenticationService>();
        return services.AddNBlazor(configuration).AddNClientAuthentication(configuration);
    }
}
