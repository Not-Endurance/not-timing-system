using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Application.Authentication;
using Not.Blazor;
using NTS.Application;

namespace NTS.Witness;

public static class NtsWitnessServices
{
    public static IServiceCollection AddNtsWitness(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .ConfigureNtsApplication(configuration, Assembly.GetCallingAssembly())
            .AddStartlist()
            .ConfigureN()
            .AddRpcClient()
            .AddDomainEvents();

        services.AddNts(configuration).AddNBlazor(configuration).ConfigureAuthentication(configuration);

        return services;
    }

    static IServiceCollection ConfigureAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.RegisterNAuthentication(configuration);
        services.AddAuthorization();
        return services;
    }
}
