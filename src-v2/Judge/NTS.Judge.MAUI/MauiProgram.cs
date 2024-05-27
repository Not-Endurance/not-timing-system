using NTS.Persistence.Startup;
using NTS.Judge.Blazor.Startup;
using Not.Injection;
using Not.MAUI.Logging;
using Not.Logging;
using NTS.Judge.Startup;

namespace NTS.Judge.MAUI;

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
            })
            .ConfigureBlazor();

        return builder.Build();
    }
}

public static class ServiceCollectionExtensions
{
    public static MauiAppBuilder ConfigureBlazor(this MauiAppBuilder builder)
    {
        builder.Services.AddMauiBlazorWebView();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif
        builder
            .ConfigureLogging()
            .AddFilesystemLogger<JudgeContext>()
            .AddHttpLogger<JudgeContext>();

        builder.Services
            .AddJudgeBlazor()
            .AddInversedDependencies();
        return builder;
    }

    public static IServiceCollection AddInversedDependencies(this IServiceCollection services)
    {
        services
            .AddPersistence()
            .AddJudge()
            .GetConventionalAssemblies()
            .RegisterConventionalServices();
        return services;
    }
}