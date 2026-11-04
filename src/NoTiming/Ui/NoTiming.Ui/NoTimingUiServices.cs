using NoTiming.Storage;

namespace NoTiming.Ui;

public static class NoTimingUiServices
{
    public static IServiceCollection AddNoTimingUi(this IServiceCollection services, IConfiguration configuration)
    {
        services.ConfigureNoTimingStorage(configuration).AddRestApiStorage();
        return services;
    }
}
