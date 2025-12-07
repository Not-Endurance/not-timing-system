using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Application.RPC;
using Not.Authentication;
using Not.Blazor.Injection;
using Not.Localization;
using NTS.Localization.Resources;

namespace NTS;

public static class NtsServiceCollectionExtensions
{
    public static IServiceCollection ConfigureNts(this IServiceCollection services, IConfiguration configuration)
    {
        return services.AddNLocalization<LocalizedStrings>(configuration);
    }

    public static IServiceCollection ConfigureNtsBlazor(this IServiceCollection services, IConfiguration configuration)
    {
        services.ConfigureNts(configuration).AddNotBlazor(configuration).AddRpcSocket(configuration);

        return services;
    }

    public static IServiceCollection ConfigureAuthentication(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.RegisterAuthServices(configuration);
        services.AddAuthorization();
        return services;
    }
}
