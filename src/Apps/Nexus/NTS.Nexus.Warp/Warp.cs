using Not.Startup;
using NTS.Application;
using NTS.Application.Cors;
using NTS.Nexus.Warp.Features.Judge;
using NTS.Nexus.Warp.Features.Witness;

namespace NTS.Nexus.Warp;

public static class Warp
{
    public static WebApplicationBuilder CreateBuilder(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.ConfigureNtsWarp(builder.Configuration);

        return builder;
    }

    public static void Start(WebApplication app, string port)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        Console.WriteLine(@$"******* WARP: Starting in '{environment}' environment *******");

        var judgeHubPath = new PathString($"/{ApplicationConstants.JUDGE_HUB}");
        var witnessHubPath = new PathString($"/{ApplicationConstants.WITNESS_HUB}");
        var originValidator = app.Services.GetRequiredService<ICorsOriginValidator>();

        app.Urls.Add($"http://*:{port}");
        app.Use(
            async (context, next) =>
            {
                var isHubWebSocketRequest =
                    context.WebSockets.IsWebSocketRequest
                    && (
                        context.Request.Path.StartsWithSegments(judgeHubPath)
                        || context.Request.Path.StartsWithSegments(witnessHubPath)
                    );

                if (isHubWebSocketRequest)
                {
                    var origin = context.Request.Headers.Origin.ToString();
                    if (!string.IsNullOrWhiteSpace(origin) && !originValidator.IsAllowed(origin))
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return;
                    }
                }

                await next();
            }
        );

        app.UseCors(NtsWarpServices.CORS_POLICY_NAME);

        app.MapHub<JudgeRpcHub>(ApplicationConstants.JUDGE_HUB).RequireCors(NtsWarpServices.CORS_POLICY_NAME);
        app.MapHub<WitnessRpcHub>(ApplicationConstants.WITNESS_HUB).RequireCors(NtsWarpServices.CORS_POLICY_NAME);

        foreach (var initializer in app.Services.GetServices<IStartupInitializer>())
        {
            initializer.RunAtStartup();
        }

        app.Run();
    }
}
