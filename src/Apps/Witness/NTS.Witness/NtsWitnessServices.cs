using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Application.Authentication;
using Not.Application.Environments;
using Not.Application.HTTP;
using Not.Blazor;
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
        services
            .ConfigureNtsApplication(configuration, Assembly.GetCallingAssembly())
            .AddStartlist()
            .ConfigureN()
            .AddRpcClient()
            .AddDomainEvents()
            .AddHttp();

        services.AddHttpClient(
            nameof(NHttpClient),
            client =>
            {
                // Keep localhost to target containers, but in environments use same-host of Azure Static Web Apps
                if (EnvironmentHelper.IsLocalhost())
                {
                    return;
                }
                client.BaseAddress = new Uri(baseUrl);
            }
        );

        return services.AddNts(configuration).AddNBlazor(configuration).AddNClientAuthentication(configuration);
    }
}
