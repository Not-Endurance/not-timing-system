using System.Diagnostics;
using NTS.Judge.Warp;

namespace NTS.Judge.MAUI;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"))
            .ConfigureJudgeMaui();

        var app = builder.Build();

        StartHub();

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
