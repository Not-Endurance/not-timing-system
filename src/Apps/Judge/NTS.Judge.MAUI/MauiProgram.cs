using System.Diagnostics;
using Not.Application.Configurations;
using Not.Application.Environments;
using Not.Logging.Builder;
using Not.MAUI;
using Not.Startup;
using NTS.Judge.MAUI.Platforms.Services;
using NTS.Judge.MAUI.Platforms.Windows.Services;
using NTS.Warp.InProcess;

namespace NTS.Judge.MAUI;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>().ConfigureFonts(fonts => fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"));

        try
        {
            builder.Services.ConfigureJudgeMaui(builder.Configuration);

            builder.UseNLog().AddFilesystemLogger();

            var assembly = typeof(MauiProgram).Assembly;
            builder.Configuration.AddNAppsettings(assembly);
            builder.Services.AddSingleton<IMauiProcessService, WindowsProcessService>();
            var app = builder.Build();

            if (EnvironmentHelper.IsLocalhost() && EnvironmentHelper.Is(JudgeVariables.NO_WARP))
            {
                StartHub();
            }

            //var krudNode = app.Services.GetRequiredService<IKrudV1ParentNodeOf<Competition>>();
            //var krudRepo = app.Services.GetRequiredService<IRepository<Competition>>();
            var zz = app.Services.GetRequiredService<IEnumerable<IStartupInitializer>>();

            return app;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            throw;
        }
    }

    static void StartHub()
    {
        var parentPid = Environment.ProcessId;
        var currentDirectory = Directory.GetCurrentDirectory();
        var info = new ProcessStartInfo
        {
            FileName = Path.Combine(currentDirectory, "NTS.Warp.InProcess.exe"),
            Arguments = WarpInProcessConstants.PARENT_PID_KEY + parentPid,
        };
        Process.Start(info);
    }
}

public static class JudgeVariables
{
    public const string NO_WARP = nameof(NO_WARP);
}
