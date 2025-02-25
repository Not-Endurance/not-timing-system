using System.Diagnostics;
using NTS.Judge.RPC;
using static NTS.Relay.Constants;
using static NTS.Judge.MAUI.Constants;
using Not.Application.RPC.SignalR;

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

        ConnectToHub(app.Services);

        return app;
    }

    static void ConnectToHub(IServiceProvider serviceProvider)
    {
        StartHub();
        var socket = serviceProvider.GetRequiredService<IRpcSocket>();
        socket.Connect();
    }

    static void StartHub()
    {
        try
        {
            var parentPid = Process.GetCurrentProcess().Id;
            var currentDirectory = Directory.GetCurrentDirectory();
            var info = new ProcessStartInfo
            {
                FileName = Path.Combine(currentDirectory, RELAY_APP_EXE),
                Arguments = PARENT_PID_KEY + parentPid.ToString()
            };

            var hubProcess = Process.Start(info);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
}
