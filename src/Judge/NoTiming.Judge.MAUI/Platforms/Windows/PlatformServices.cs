using Microsoft.Extensions.Configuration;

namespace NoTiming.Judge.MAUI.Platforms.Windows;

public static class PlatformServices
{
    public static IServiceCollection AddMauiPlatformServices(this IServiceCollection services, IConfiguration _)
    {
        services.AddMauiBlazorWebView();
        return services.AddBlazorWebViewDeveloperTools();
    }
}
