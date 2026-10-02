using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Application.Authentication.Abstractions;
using Not.Application.RPC.SignalR;
using Not.Blazor.Client;
using Not.Krud.ServiceRegistration;
using NoTiming.Ui.Features.Profile;
using NoTiming.Ui.Features.Socket;
using NoTiming.Ui.Storage.Repositories;
using NTS;
using NTS.Application;

namespace NoTiming.Ui;

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

        return services.AddNts(configuration).NClientSideBlazor(configuration);
    }
}
