#pragma warning disable CA1416 // We use only Windows

using NC.Application.Configurations;
using NC.Application.Environments;
using NC.Logging.Builder;
using NC.MAUI;
using NoTiming.Judge.MAUI.Platforms.Services;
using NoTiming.Judge.MAUI.Platforms.Windows.Services;

namespace NoTiming.Judge.MAUI;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>().ConfigureFonts(fonts => fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"));

        var assembly = typeof(MauiProgram).Assembly;
        var targetEnvironment = EnvironmentHelper.UseResolvedEnvironment(assembly);
        builder.Configuration.AddNAppsettings(assembly, "judge");

        builder.UseNLog().AddFilesystemLogger("NoTiming.Judge");
        builder.Services.AddSingleton(new NEnvironment(targetEnvironment));
        builder.Services.AddJudgeMaui(builder.Configuration);

        builder.Services.AddSingleton<IMauiProcessService, WindowsProcessService>();
        return builder.Build();
    }
}

#pragma warning restore CA1416 // Validate platform compatibility
