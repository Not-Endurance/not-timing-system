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
            .AddDomainEvents()
            .AddHttp();

        return services
            .AddNts(configuration)
            .AddNBlazor(configuration)
            .AddNClientAuthentication(configuration);
    }
}
