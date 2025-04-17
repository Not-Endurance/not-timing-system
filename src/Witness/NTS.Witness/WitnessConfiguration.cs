using Microsoft.Extensions.Configuration;
using Not.Blazor.Injection;

namespace NTS.Witness;

public static class WitnessConfiguration
{
    public static IServiceCollection AddWitnessServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddNotBlazor(configuration);

        return services;
    }
}
