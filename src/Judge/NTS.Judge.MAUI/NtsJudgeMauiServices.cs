using System.Reflection;
using Microsoft.Extensions.Configuration;
using NTS.Blazor;
using NTS.Judge.MAUI.Platforms.Windows;

namespace NTS.Judge.MAUI;

public static class NtsJudgeMauiServices
{
    public static IServiceCollection ConfigureJudgeMaui(this IServiceCollection services, IConfiguration configuration)
    {
        return services.AddPlatformServices(configuration).ConfigureNtsJudge(configuration);
    }
}
