using System.Reflection;
using Not.Application.HTTP;
using NoTiming.Ui.Storage;
using NTS.Contracts;

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

        // The families that moved to the Api are reached where the page came from, which is the Api (ADR-0011), and not
        // where the legacy host is: the one is the page's own address and the other is a setting.
        services.Configure<JsonApiSettings>(settings =>
        {
            settings.Url = $"{baseUrl.TrimEnd('/')}/api";
            settings.WriteHeaders[ApplicationConstants.WRITE_HEADER] = ApplicationConstants.WRITE_HEADER_VALUE;
        });
        return services.AddNtsWitness(configuration, baseUrl, rootAssembly);
    }
}
