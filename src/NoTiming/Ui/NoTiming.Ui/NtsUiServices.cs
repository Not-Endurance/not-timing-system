using NTS.Storage;

namespace NoTiming.Ui;

public static class NtsUiServices
{
    public static IServiceCollection AddNtsUi(this IServiceCollection services, IConfiguration configuration)
    {
        services.ConfigureNtsStorage(configuration).AddRestApiStorage();
        return services;
    }
}
