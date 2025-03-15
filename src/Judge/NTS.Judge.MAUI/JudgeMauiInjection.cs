using Microsoft.Extensions.Logging;
using Not.Injection;
using Not.Logging.Builder;
using Not.MAUI.Logging;
using NTS.Judge.Blazor;
using NTS.Storage;

namespace NTS.Judge.MAUI;

public static class JudgeMauiInjection
{
    public static MauiAppBuilder ConfigureJudgeMaui(this MauiAppBuilder builder)
    {
        builder.Services.AddMauiBlazorWebView();
#if DEBUG
        builder.Logging.AddDebug();
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif
        builder
            .Services.ConfigureStorage()
            .ConfigureJudge(builder.Configuration)
            .ConfigureJudgeBlazor(builder.Configuration)
            .RegisterConventionalServices();

        builder.ConfigureLogging().AddFilesystemLogger();

        return builder;
    }
}
