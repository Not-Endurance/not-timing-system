using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NC.Application.Configurations;
using NC.Application.Print;
using NC.Blazor.Client.Authentication;
using NC.Blazor.Client.Browser;
using NC.Files.Abstractions;
using NC.Print;

namespace NC.Blazor.Client;

public static class NBlazorClientServices
{
    public static IServiceCollection NClientSideBlazor(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSettings<NPrintClientSettings>(configuration);
        services.TryAddTransient<INPrintApiService, NApiPrintService>();
        services.TryAddTransient<BrowserPrintService>();
        services.TryAddTransient<IFileService>(provider => provider.GetRequiredService<BrowserPrintService>());
        services.TryAddTransient<INPrintService>(provider => provider.GetRequiredService<BrowserPrintService>());
        return services.AddNBlazor(configuration).AddNClientAuthentication(configuration);
    }
}
