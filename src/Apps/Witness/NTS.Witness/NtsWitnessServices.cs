using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Blazor.Client;
using Not.Krud.ServiceRegistration;
using NTS.Application;

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
        services
            .ConfigureNtsApplication(configuration, Assembly.GetCallingAssembly())
            .AddSharedCoreDomainServices()
            .ConfigureN()
            .AddRpcClient()
            .AddDomainEvents()
            .AddHttp(settings => settings.Host = baseUrl);

        return services.AddNts(configuration).NClientSideBlazor(configuration);
    }
}
