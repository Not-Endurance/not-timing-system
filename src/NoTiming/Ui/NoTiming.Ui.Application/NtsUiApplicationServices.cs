using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Application.Authentication.Abstractions;
using Not.Application.RPC;
using Not.Application.RPC.SignalR;
using Not.Blazor.Client;
using Not.Krud.ServiceRegistration;
using NTS;
using NTS.Application;
using NoTiming.Ui.Application.Features.Profile;
using NoTiming.Ui.Application.Features.Socket;
using NoTiming.Ui.Application.Storage.Repositories;

namespace NoTiming.Ui.Application;

public static class NtsUiApplicationServices
{
    public static IServiceCollection AddNtsUiApplication(
        this IServiceCollection services,
        IConfiguration configuration,
        string baseUrl,
        Assembly rootAssembly
    )
    {
        services.ConfigureKrud();
        services.AddScoped<IRpcAccessTokenProvider, NtsClientRpcAccessTokenProvider>();
        services.AddScoped<IUiAuthenticationRedirector, UiAuthenticationRedirector>();
        services.AddTransient<IUserRegister, UiUserApiRepository>();
        services.AddTransient<IUiUserProfileRepository, UiUserApiRepository>();
        services
            .ConfigureNtsApplication(configuration, rootAssembly)
            .AddSharedCoreDomainServices()
            .ConfigureN()
            .AddRpcClient()
            .AddDomainEvents()
            .AddHttp(settings => settings.Host = baseUrl)
            .AddUserSessions();

        // Deployed builds leave RpcSettings.Host empty and call the origin the UI is served from.
        services.PostConfigure<RpcSettings>(settings =>
        {
            if (string.IsNullOrWhiteSpace(settings.Host))
            {
                settings.Host = baseUrl.TrimEnd('/');
            }
        });

        return services.AddNts(configuration).NClientSideBlazor(configuration);
    }
}
