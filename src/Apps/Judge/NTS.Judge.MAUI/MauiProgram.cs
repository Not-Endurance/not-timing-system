#pragma warning disable CA1416 // We use only Windows

using Not.Application.Configurations;
using Not.Logging.Builder;
using Not.MAUI;
using NTS.Judge.MAUI.Platforms.Services;
using NTS.Judge.MAUI.Platforms.Windows.Services;

namespace NTS.Judge.MAUI;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>().ConfigureFonts(fonts => fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"));

        builder.UseNLog().AddFilesystemLogger("NTS.Judge");
        builder.Services.AddJudgeMaui(builder.Configuration);

        var assembly = typeof(MauiProgram).Assembly;
        builder.Configuration.AddNAppsettings(assembly, "judge");
        builder.Services.AddSingleton<IMauiProcessService, WindowsProcessService>();
        return builder.Build();
    }
}

#pragma warning restore CA1416 // Validate platform compatibility
