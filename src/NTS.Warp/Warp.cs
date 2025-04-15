using Not.Startup;
using NTS.Application;
using NTS.Warp.Features.Judge;
using NTS.Warp.Features.Witness;

namespace NTS.Warp;

public static class Warp
{
    public static WebApplicationBuilder CreateBuilder(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.ConfigureWarp();

        return builder;
    }

    public static void Start(WebApplication app, string port)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        Console.WriteLine(@$"******* WARP: Starting in '{environment}' environment *******");

        app.Urls.Add($"http://*:{port}");

        app.MapHub<JudgeRpcHub>(ApplicationConstants.JUDGE_HUB);
        app.MapHub<WitnessRpcHub>(ApplicationConstants.WITNESS_HUB);

        foreach (var initializer in app.Services.GetServices<IStartupInitializer>())
        {
            initializer.RunAtStartup();
        }

        app.Run();
    }
}
