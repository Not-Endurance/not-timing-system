using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MudBlazor;
using MudBlazor.Services;
using Not.Blazor.Browser;
using Not.Blazor.Helpers;
using Not.Blazor.Navigation;
using Not.Blazor.Navigation.Abstractions;
using Not.Notify;

namespace Not.Blazor;

public static class NBlazorServices
{
    public static IServiceCollection AddNBlazor(this IServiceCollection services, IConfiguration _)
    {
        services.TryAddSingleton<Notifier>();
        services.TryAddSingleton<INotifier>(provider => provider.GetRequiredService<Notifier>());
        services.TryAddSingleton<INotificationStream>(provider => provider.GetRequiredService<Notifier>());

        return services
            .AddMudBlazor()
            .AddTransient<ILandNavigator, BlazorCrumbsNavigator>()
            .AddTransient<ICrumbsNavigator, BlazorCrumbsNavigator>();
    }

    static IServiceCollection AddMudBlazor(
        this IServiceCollection services,
        Action<MudServicesConfiguration>? customConfiguration = null
    )
    {
        return services.AddMudServices(config =>
        {
            config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomCenter;
            config.SnackbarConfiguration.ShowCloseIcon = true;
            config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
            config.SnackbarConfiguration.HideTransitionDuration = 200;
            config.SnackbarConfiguration.ShowTransitionDuration = 200;
            config.SnackbarConfiguration.SetVisibleDuration(TimeSpan.FromSeconds(10));

            customConfiguration?.Invoke(config);
        });
    }
}
