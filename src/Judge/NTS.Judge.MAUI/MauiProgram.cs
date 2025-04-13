using System.Diagnostics;
using Microsoft.Extensions.Configuration;
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

        var environment = EnvironmentHelper.GetEnvironment();

        var config = new ConfigurationBuilder()
            .AddJsonFile($"appsettings.{environment}.json", optional: false)
            .Build();

        builder.Configuration.AddConfiguration(config);

        var app = builder.Build();
        
        if (EnvironmentHelper.IsLocalhost())
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
