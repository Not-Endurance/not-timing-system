using Not.Application.Configurations;
using NTS.Witness.Platforms.Services;
using Serilog;

namespace NTS.Witness;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

        var assembly = typeof(MauiProgram).Assembly;
        builder.Configuration.AddNAppsettings(assembly);

        builder.Logging.AddSerilog();
        //builder.ConfigureLogging().AddFilesystemLogger();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif
        builder.Services.AddWitnessServices(builder.Configuration);
        builder.Services.AddSingleton<IDialService, DialService>();
        return builder.Build();
    }
}
