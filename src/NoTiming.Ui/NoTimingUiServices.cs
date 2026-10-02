using System.Reflection;
using NoTiming.Ui.Storage;

namespace NoTiming.Ui;

public static class NoTimingUiServices
{
    public static IServiceCollection AddNoTimingUi(
        this IServiceCollection services,
        IConfiguration configuration,
        string baseUrl,
        Assembly rootAssembly
    )
    {
        services.ConfigureNtsStorage(configuration).AddRestApiStorage();
        return services.AddNtsWitness(configuration, baseUrl, rootAssembly);
    }
}
