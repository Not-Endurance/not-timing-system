using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NTS.Storage;

namespace NTS.Witness.Web;

public static class NtsWitnessWebServices
{
    public static IServiceCollection AddNtsWitnessWeb(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.ConfigureNtsStorage(configuration).AddRestApiStorage();
        return services;
    }
}
