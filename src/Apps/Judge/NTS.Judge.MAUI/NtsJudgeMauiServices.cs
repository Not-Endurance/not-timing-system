using Microsoft.Extensions.Configuration;
using NTS.Judge.MAUI.Platforms.Windows;
using NTS.Storage;

namespace NTS.Judge.MAUI;

public static class NtsJudgeMauiServices
{
    public static IServiceCollection ConfigureJudgeMaui(this IServiceCollection services, IConfiguration configuration)
    {
        services.ConfigureNtsStorage(configuration).AddJsonStorage().AddRestApiStorage();
        return services.AddPlatformServices(configuration).ConfigureNtsJudge(configuration);
    }
}
