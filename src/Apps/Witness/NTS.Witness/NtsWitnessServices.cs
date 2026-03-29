using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Application.Configurations;
using Not.Application.RPC.SignalR;
using Not.Blazor.Client;
using Not.Krud.ServiceRegistration;
using NTS.Application;
using NTS.Witness.Features.Socket;

namespace NTS.Witness;

public static class NtsWitnessServices
{
    public static IServiceCollection AddNtsWitness(
        this IServiceCollection services,
        IConfiguration configuration,
        string baseUrl
    )
    {
        services.ConfigureKrud();
        services.AddScoped<IRpcAccessTokenProvider, NtsClientRpcAccessTokenProvider>();
        services
            .ConfigureNtsApplication(configuration, Assembly.GetCallingAssembly())
            .AddSharedCoreDomainServices()
            .ConfigureN()
            .AddRpcClient()
            .AddDomainEvents()
            .AddHttp(settings => settings.Host = baseUrl)
            .AddUserSessions();

        return services.AddNts(configuration).NClientSideBlazor(configuration);
    }
}
