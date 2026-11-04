using NTS.Storage;

namespace NoTiming.Ui;

public static class NtsWitnessWebServices
{
    public static IServiceCollection AddNtsWitnessWeb(this IServiceCollection services, IConfiguration configuration)
    {
        services.ConfigureNtsStorage(configuration).AddRestApiStorage();
        return services;
    }
}
