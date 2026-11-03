using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Application.Authentication.Abstractions;
using Not.Application.RPC;
using Not.Application.RPC.SignalR;
using Not.Blazor.Client;
using Not.Krud.ServiceRegistration;
using NTS.Application;
using NTS.Witness.Features.Profile;
using NTS.Witness.Features.Socket;
using NTS.Witness.Storage.Repositories;

namespace NTS.Witness;

public static class NtsWitnessServices
{
    public static IServiceCollection AddNtsWitness(
        this IServiceCollection services,
        IConfiguration configuration,
        string baseUrl,
        Assembly rootAssembly
    )
    {
        services.ConfigureKrud();
        services.AddScoped<IRpcAccessTokenProvider, NtsClientRpcAccessTokenProvider>();
        services.AddScoped<IWitnessAuthenticationRedirector, WitnessAuthenticationRedirector>();
        services.AddTransient<IUserRegister, UserApiRepository>();
        services.AddTransient<IWitnessUserProfileRepository, UserApiRepository>();
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
