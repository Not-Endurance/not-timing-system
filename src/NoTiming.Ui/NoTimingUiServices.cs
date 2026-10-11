using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Not.Application.HTTP;
using NoTiming.Ui.Features.Console;
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
        services.TryAddSingleton(TimeProvider.System); // the clock of the device, which the server's clock is measured against
        services.ConfigureNtsStorage(configuration).AddRestApiStorage();
        services.AddTransient<ConsoleParticipationWrites>(); // not a repository of the app: the dashboard's writes (#646)
        services.AddTransient<ConsoleRankingWrites>(); // and the Rankings the Console makes

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
