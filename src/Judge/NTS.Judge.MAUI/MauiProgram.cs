using System.Diagnostics;
using Not.Application.Configurations;
using Not.Application.Environments;
using NTS.Judge.Warp;

namespace NTS.Judge.MAUI;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp
            .CreateBuilder()
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"))
            .ConfigureJudgeMaui();

        builder.Configuration.AddNAppsettings();

        var app = builder.Build();
        
        if (EnvironmentHelper.IsLocalhost() && EnvironmentHelper.Is(JudgeVariables.DEBUG_WARP))
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
    public const string DEBUG_WARP = nameof(DEBUG_WARP);
}
