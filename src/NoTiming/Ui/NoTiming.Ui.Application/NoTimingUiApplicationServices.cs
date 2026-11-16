using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using NC.Application.Authentication.Abstractions;
using NC.Application.HTTP;
using NC.Application.RPC;
using NC.Application.RPC.SignalR;
using NC.Blazor.Client;
using NC.Krud.ServiceRegistration;
using NoTiming.Application;
using NoTiming.Shared;
using NoTiming.Ui.Application.Features.Account;
using NoTiming.Ui.Application.Features.Profile;
using NoTiming.Ui.Application.Features.Socket;
using NoTiming.Ui.Application.Storage.Repositories;

namespace NoTiming.Ui.Application;

public static class NoTimingUiApplicationServices
{
    public static IServiceCollection AddNoTimingUiApplication(
        this IServiceCollection services,
        IConfiguration configuration,
        string uiBaseAddress,
        Assembly rootAssembly
    )
    {
        // The Ui is served under its own path (/ui/), but the API and the hubs sit at the origin. A call relative to the
        // Ui's path would reach /ui/api/..., which the Ui answers with its index.html, so every call goes to the origin.
        var serverOrigin = new Uri(uiBaseAddress).GetLeftPart(UriPartial.Authority);

        services.ConfigureKrud();
        services.AddScoped<IRpcAccessTokenProvider, CookieRpcAccessTokenProvider>();
        services.AddTransient<IUserRegister, UiUserApiRepository>();
        services.AddTransient<IUiUserProfileRepository, UiUserApiRepository>();
        services
            .ConfigureNoTimingApplication(configuration, rootAssembly)
            .AddSharedCoreDomainServices()
            .ConfigureN()
            .AddRpcClient()
            .AddDomainEvents()
            .AddHttp(settings => settings.Host = serverOrigin)
            .AddUserSessions();

        // Every write the Ui makes carries the header the server's state-change guard requires.
        services.AddTransient<UiRequestGuardHandler>();
        services.AddHttpClient(nameof(NHttpClient)).AddHttpMessageHandler<UiRequestGuardHandler>();

        // Deployed builds leave RpcSettings.Host empty and call the origin the UI is served from.
        services.PostConfigure<RpcSettings>(settings =>
        {
            if (string.IsNullOrWhiteSpace(settings.Host))
            {
                settings.Host = serverOrigin;
            }
        });

        return services.AddNoTiming(configuration).NClientSideBlazor(configuration);
    }
}
