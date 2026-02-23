using Microsoft.Extensions.Configuration;
using NTS.Judge.Blazor;
using NTS.Judge.MAUI.Platforms.Windows;
using NTS.Storage;

namespace NTS.Judge.MAUI;

public static class NtsJudgeMauiServices
{
    public static IServiceCollection AddJudgeMaui(this IServiceCollection services, IConfiguration configuration)
    {
        services.ConfigureNtsStorage(configuration).AddJsonStorage().AddRestApiStorage();
        return services.AddMauiPlatformServices(configuration).AddNtsJudge(configuration).AddJudgeBlazor(configuration);
    }
}
