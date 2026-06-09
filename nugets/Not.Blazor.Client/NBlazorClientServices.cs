using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Not.Application.Authentication.Abstractions;
using Not.Application.Print;
using Not.Blazor.Client.Authentication;
using Not.Blazor.Client.Authentication.Services;
using Not.Blazor.Client.Browser;
using Not.Files.Abstractions;
using Not.Print;

namespace Not.Blazor.Client;

public static class NBlazorClientServices
{
    public static IServiceCollection NClientSideBlazor(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddTransient<INAuthentication, NBlazorClientAuthenticationService>();
        services.TryAddTransient<INPrintApiService, NApiPrintService>();
        services.TryAddTransient<BrowserPrintService>();
        services.TryAddTransient<IFileService>(provider => provider.GetRequiredService<BrowserPrintService>());
        services.TryAddTransient<INPrintService>(provider => provider.GetRequiredService<BrowserPrintService>());
        return services.AddNBlazor(configuration).AddNClientAuthentication(configuration);
    }
}
