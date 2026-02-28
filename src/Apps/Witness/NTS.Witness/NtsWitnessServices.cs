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
        services.ConfigureWitnessCore(configuration);
        return services.ConfigureServerAuthentication(configuration);
    }

    public static IServiceCollection AddNtsWitnessWasm(this IServiceCollection services, IConfiguration configuration)
    {
        services.ConfigureWitnessCore(configuration);
        return services.ConfigureWasmAuthentication(configuration);
    }

    static IServiceCollection ConfigureWitnessCore(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .ConfigureNtsApplication(configuration, Assembly.GetCallingAssembly())
            .AddStartlist()
            .ConfigureN()
            .AddRpcClient()
            .AddDomainEvents();

        return services.AddNts(configuration).AddNBlazor(configuration);
    }

    static IServiceCollection ConfigureServerAuthentication(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.RegisterNAuthentication(configuration);
        services.AddAuthorization();
        return services;
    }

    static IServiceCollection ConfigureWasmAuthentication(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.RegisterNAuthenticationWasm(configuration);
        services.AddAuthorizationCore();
        return services;
    }
}
