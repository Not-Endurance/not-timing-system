using Not.Application.Configurations;
using Not.Logging.Builder;
using Not.MAUI.Logging;
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

        builder.Configuration.AddNAppsettings();

        builder.Logging.AddSerilog();
        //builder.ConfigureLogging().AddFilesystemLogger();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif
        builder.Services.AddWitnessServices(builder.Configuration);

        return builder.Build();
    }
}
