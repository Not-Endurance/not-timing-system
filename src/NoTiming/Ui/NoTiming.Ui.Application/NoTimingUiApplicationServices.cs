using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NC.Application.Authentication.Abstractions;
using NC.Application.RPC;
using NC.Application.RPC.SignalR;
using NC.Blazor.Client;
using NC.Krud.ServiceRegistration;
using NoTiming.Application;
using NoTiming.Shared;
using NoTiming.Ui.Application.Features.Profile;
using NoTiming.Ui.Application.Features.Socket;
using NoTiming.Ui.Application.Storage.Repositories;

namespace NoTiming.Ui.Application;

public static class NoTimingUiApplicationServices
{
    public static IServiceCollection AddNoTimingUiApplication(
        this IServiceCollection services,
        IConfiguration configuration,
        string baseUrl,
        Assembly rootAssembly
    )
    {
        services.ConfigureKrud();
        services.AddScoped<IRpcAccessTokenProvider, NoTimingClientRpcAccessTokenProvider>();
        services.AddScoped<IUiAuthenticationRedirector, UiAuthenticationRedirector>();
        services.AddTransient<IUserRegister, UiUserApiRepository>();
        services.AddTransient<IUiUserProfileRepository, UiUserApiRepository>();
        services
            .ConfigureNoTimingApplication(configuration, rootAssembly)
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

        return services.AddNoTiming(configuration).NClientSideBlazor(configuration);
    }
}
