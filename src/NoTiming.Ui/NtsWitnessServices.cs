using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Application.RPC.SignalR;
using Not.Blazor.Client;
using Not.Krud.ServiceRegistration;
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
        services
            .ConfigureNtsApplication(configuration, rootAssembly)
            .AddSharedCoreDomainServices()
            .ConfigureN()
            .AddRpcClient()
            .AddDomainEvents()
            .AddHttp(settings => settings.Host = baseUrl);

        return services.AddNts(configuration).NClientSideBlazor(configuration);
    }
}
