using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Not.Application.Authentication.Abstractions;
using Not.Blazor.Browser;
using Not.Blazor.Client.Authentication;
using Not.Blazor.Client.Authentication.Services;
using Not.Blazor.Client.Browser;

namespace Not.Blazor.Client;

public static class NBlazorClientServices
{
    public static IServiceCollection NClientSideBlazor(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddTransient<INAuthentication, NBlazorClientAuthenticationService>();
        services.TryAddTransient<IFileDownloadService, BrowserFileDownloadService>();
        return services.AddNBlazor(configuration).AddNClientAuthentication(configuration);
    }
}
