using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Not.Blazor.CRUD.Forms;
using Not.Blazor.Dialogs;
using Not.Blazor.Mud;
using Not.Blazor.Navigation;

namespace Not.Blazor.Injection;

public static class BlazorInjectionExtensions
{
    public static IServiceCollection AddNotBlazor(this IServiceCollection services, IConfiguration _)
    {
        return services
            .AddNotMudBlazor()
            .AddTransient(typeof(CrudeDialog<,>))
            .AddTransient(typeof(FormManager<,>))
            .AddTransient<ILandNavigator, BlazorCrumbsNavigator>()
            .AddTransient<ICrumbsNavigator, BlazorCrumbsNavigator>();
    }

    public static IServiceCollection AddNotMudBlazor(
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
