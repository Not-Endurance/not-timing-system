using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Authentication;
using Not.Blazor;
using Not.Filesystem;
using NTS.Application;

namespace NTS.Witness;

public static class NtsWitnessServices
{
    public static IServiceCollection ConfigureNtsWitness(this IServiceCollection services, IConfiguration configuration)
    {
        services.ConfigureNtsApplication(configuration, Assembly.GetCallingAssembly())
            .AddStartlist()
            .ConfigureN()
            .AddSignalR();
        
        services.ConfigureNts(configuration).AddNBlazor(configuration).ConfigureAuthentication(configuration);

        return services;
    }

    static IServiceCollection ConfigureAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.RegisterNAuthentication(configuration);
        services.AddAuthorization();
        return services;
    }
}
