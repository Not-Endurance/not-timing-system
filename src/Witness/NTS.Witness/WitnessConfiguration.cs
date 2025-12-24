using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Application.HTTP;
using Not.Injection;

namespace NTS.Witness;

public static class WitnessConfiguration
{
    public static IServiceCollection AddWitnessServices(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .ConfigureNtsBlazor(configuration)
            .AddNHttp(configuration)
            .ConfigureAuthentication(configuration)
            .RegisterConventionalServices();

        return services;
    }
}
