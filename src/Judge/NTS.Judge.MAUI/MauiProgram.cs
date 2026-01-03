using System.Diagnostics;
using MudBlazor;
using Not.Application.Configurations;
using Not.Application.Environments;
using Not.Logging.Builder;
using Not.MAUI;
using NTS.Judge.MAUI.Platforms.Services;
using NTS.Judge.MAUI.Platforms.Windows.Services;
using NTS.Judge.Warp;

namespace NTS.Judge.MAUI;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>().ConfigureFonts(fonts => fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"));

        builder.Services.ConfigureJudgeMaui(builder.Configuration);

        builder.UseNLog().AddFilesystemLogger();

        var assembly = typeof(MauiProgram).Assembly;
        builder.Configuration.AddNAppsettings(assembly);
        builder.Services.AddSingleton<IAppName, AppNameService>();
        var app = builder.Build();

        if (EnvironmentHelper.IsLocalhost() && EnvironmentHelper.Is(JudgeVariables.NO_WARP))
        {
            StartHub();
        }

        return app;
    }

    static void StartHub()
    {
        try
        {
            var parentPid = Environment.ProcessId;
            var currentDirectory = Directory.GetCurrentDirectory();
            var info = new ProcessStartInfo
            {
                FileName = Path.Combine(currentDirectory, "NTS.Judge.Warp.exe"),
                Arguments = JudgeWarpConstants.PARENT_PID_KEY + parentPid,
            };
            Process.Start(info);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
}

public static class JudgeVariables
{
    public const string NO_WARP = nameof(NO_WARP);
}
