using Not.Startup;
using NTS.Application;
using NTS.Warp.RPC;

namespace NTS.Warp;

public static class Warp
{
    public static WebApplicationBuilder CreateBuilder(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        
        builder.Services.ConfigureHub();

        builder.Logging.AddFilter("Microsoft.AspNetCore.SignalR", LogLevel.Debug);
        builder.Logging.AddFilter("Microsoft.AspNetCore.Http.Connections", LogLevel.Debug);

        return builder;
    }

    public static void StartApp(WebApplicationBuilder builder)
    {
        var app = builder.Build();

        app.Urls.Add("http://*:11337");

        app.MapHub<JudgeRpcHub>(ApplicationConstants.JUDGE_HUB);
        app.MapHub<WitnessRpcHub>(ApplicationConstants.WITNESS_HUB);

        foreach (var initializer in app.Services.GetServices<IStartupInitializer>())
        {
            initializer.RunAtStartup();
        }

        app.Run();
    }
}

