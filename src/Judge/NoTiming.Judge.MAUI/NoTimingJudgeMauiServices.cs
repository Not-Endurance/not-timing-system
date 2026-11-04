using System.Reflection;
using Microsoft.Extensions.Configuration;
using NoTiming.Judge.Blazor;
using NoTiming.Judge.MAUI.Platforms.Windows;
using NoTiming.Storage;

namespace NoTiming.Judge.MAUI;

public static class NoTimingJudgeMauiServices
{
    public static IServiceCollection AddJudgeMaui(this IServiceCollection services, IConfiguration configuration)
    {
        services.ConfigureNoTimingStorage(configuration).AddRestApiStorage();
        return services
            .AddMauiPlatformServices(configuration)
            .AddNoTimingJudge(configuration, Assembly.GetExecutingAssembly())
            .AddJudgeBlazor(configuration);
    }
}
