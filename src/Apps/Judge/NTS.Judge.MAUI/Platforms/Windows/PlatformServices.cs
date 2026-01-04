using Microsoft.Extensions.Configuration;

namespace NTS.Judge.MAUI.Platforms.Windows;

public static class PlatformServices
{
    public static IServiceCollection AddPlatformServices(this IServiceCollection services, IConfiguration _)
    {
        services.AddMauiBlazorWebView();
        return services.AddBlazorWebViewDeveloperTools();
    }
}
