using Microsoft.Extensions.Configuration;

namespace NTS.Judge.MAUI.Platforms.Windows;

public static class PlatformServices
{
    public static IServiceCollection AddMauiPlatformServices(this IServiceCollection services, IConfiguration _)
    {
        services.AddMauiBlazorWebView();
        return services.AddBlazorWebViewDeveloperTools();
    }
}
