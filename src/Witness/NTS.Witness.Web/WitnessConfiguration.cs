using Not.Application.HTTP;
using Not.Injection;

namespace NTS.Witness.Web;

public static class WitnessConfiguration
{
    public static IServiceCollection AddWitnessServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.ConfigureNtsBlazor(configuration).AddNHttp(configuration).RegisterConventionalServices();

        return services;
    }
}
