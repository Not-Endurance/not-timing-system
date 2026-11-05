using Microsoft.Extensions.Localization;
using NC.Application.Configurations;
using NC.Application.Environments;
using NC.Localization;
using NC.Startup;
using NoTiming.Application.Contracts;
using NoTiming.ConnectionDiagnostics;
using NoTiming.EventBus;
using NoTiming.EventBus.ConnectionDiagnostics;
using NoTiming.Host;
using NoTiming.Host.Cors;

namespace NoTiming;

public static class NoTimingHost
{
    public static WebApplicationBuilder CreateBuilder(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Local runs read per-environment secrets (for example the Mongo connection string) from user secrets.
        if (EnvironmentHelper.IsLocalhost())
        {
            builder.Configuration.AddSecrets($"nts-warp-{EnvironmentHelper.GetEnvironment()}");
        }

        builder.Services.ConfigureNoTiming(builder.Configuration);

        return builder;
    }

    public static void Start(WebApplication app, string port)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("NoTiming.Startup");
        var environment =
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? app.Environment.EnvironmentName;

        var judgeHubPath = new PathString($"/{ApplicationConstants.EventBusHub(ApplicationConstants.JUDGE_HUB)}");
        var witnessHubPath = new PathString($"/{ApplicationConstants.EventBusHub(ApplicationConstants.WITNESS_HUB)}");
        var originValidator = app.Services.GetRequiredService<ICorsOriginValidator>();

        app.Urls.Add($"http://*:{port}");
        logger.LogInformation(
            "NoTiming starting in {Environment}. InstanceId {InstanceId}, Port {Port}, JudgeHub {JudgeHub}, WitnessHub {WitnessHub}, Urls {Urls}.",
            environment,
            NoTimingConnectionDiagnostics.GetInstanceId(),
            port,
            judgeHubPath.Value,
            witnessHubPath.Value,
            string.Join(", ", app.Urls)
        );

        app.UseMiddleware<ConnectionDiagnosticsMiddleware>();
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
                    if (
                        !string.IsNullOrWhiteSpace(origin)
                        && !originValidator.IsAllowed(origin)
                        && !CorsOriginMatcher.IsSameHost(origin, context.Request.Host.Host)
                    )
                    {
                        logger.LogWarning(
                            "NoTiming rejected WebSocket request for {Path} because origin {Origin} is not allowed. CorrelationId {CorrelationId}, Client {ClientName}, Version {ClientVersion}, InstanceId {InstanceId}.",
                            context.Request.Path,
                            origin,
                            NoTimingConnectionDiagnostics.GetCorrelationId(context),
                            NoTimingConnectionDiagnostics.GetClientName(context),
                            NoTimingConnectionDiagnostics.GetClientVersion(context),
                            NoTimingConnectionDiagnostics.GetInstanceId()
                        );
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return;
                    }
                }

                await next();
            }
        );

        app.UseUi();

        app.UseCors(NoTimingServices.CORS_POLICY_NAME);
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapGet(
            "/healthz",
            () =>
                Results.Ok(
                    new
                    {
                        status = "ok",
                        environment,
                        instanceId = NoTimingConnectionDiagnostics.GetInstanceId(),
                        judgeHub = judgeHubPath.Value,
                        witnessHub = witnessHubPath.Value,
                    }
                )
        );
        app.MapNoTimingEventBus(NoTimingServices.CORS_POLICY_NAME);
        app.MapControllers();

        LocalizationHelper.Configure(app.Services.GetRequiredService<IStringLocalizer>());

        foreach (var initializer in app.Services.GetServices<IStartupInitializer>())
        {
            initializer.RunAtStartup();
        }

        app.Run();
    }
}
