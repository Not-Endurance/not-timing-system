using System.Security.Claims;
using Not.Startup;
using NTS.Application.Contracts;
using NTS.Application.Cors;
using NTS.Nexus.Warp.ConnectionDiagnostics;
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
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("NTS.Nexus.Warp.Startup");
        var environment =
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? app.Environment.EnvironmentName;

        var judgeHubPath = new PathString($"/{ApplicationConstants.JUDGE_HUB}");
        var witnessHubPath = new PathString($"/{ApplicationConstants.WITNESS_HUB}");
        var originValidator = app.Services.GetRequiredService<ICorsOriginValidator>();

        app.Urls.Add($"http://*:{port}");
        logger.LogInformation(
            "Warp starting in {Environment}. InstanceId {InstanceId}, Port {Port}, JudgeHub {JudgeHub}, WitnessHub {WitnessHub}, Urls {Urls}.",
            environment,
            WarpConnectionDiagnostics.GetInstanceId(),
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
                    if (!string.IsNullOrWhiteSpace(origin) && !originValidator.IsAllowed(origin))
                    {
                        logger.LogWarning(
                            "Warp rejected WebSocket request for {Path} because origin {Origin} is not allowed. CorrelationId {CorrelationId}, Client {ClientName}, Version {ClientVersion}, InstanceId {InstanceId}.",
                            context.Request.Path,
                            origin,
                            WarpConnectionDiagnostics.GetCorrelationId(context),
                            WarpConnectionDiagnostics.GetClientName(context),
                            WarpConnectionDiagnostics.GetClientVersion(context),
                            WarpConnectionDiagnostics.GetInstanceId()
                        );
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return;
                    }
                }

                await next();
            }
        );

        app.UseCors(NtsWarpServices.CORS_POLICY_NAME);
        app.UseAuthentication();
        if (IsIntegrationAuthEnabled())
        {
            app.Use(
                async (context, next) =>
                {
                    if (context.User?.Identity?.IsAuthenticated != true)
                    {
                        var principal = CreateIntegrationPrincipal(context);
                        if (principal != null)
                        {
                            context.User = principal;
                        }
                    }

                    await next();
                }
            );
        }
        app.UseAuthorization();

        app.MapGet(
            "/healthz",
            () =>
                Results.Ok(
                    new
                    {
                        status = "ok",
                        environment,
                        instanceId = WarpConnectionDiagnostics.GetInstanceId(),
                        judgeHub = judgeHubPath.Value,
                        witnessHub = witnessHubPath.Value,
                    }
                )
        );
        app.MapHub<JudgeRpcHub>(ApplicationConstants.JUDGE_HUB).RequireCors(NtsWarpServices.CORS_POLICY_NAME);
        app.MapHub<WitnessRpcHub>(ApplicationConstants.WITNESS_HUB).RequireCors(NtsWarpServices.CORS_POLICY_NAME);

        foreach (var initializer in app.Services.GetServices<IStartupInitializer>())
        {
            initializer.RunAtStartup();
        }

        app.Run();
    }

    static bool IsIntegrationAuthEnabled()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable("NTS_INTEGRATION_AUTH"),
            "true",
            StringComparison.OrdinalIgnoreCase
        );
    }

    static ClaimsPrincipal? CreateIntegrationPrincipal(HttpContext context)
    {
        var token = context.Request.Query["access_token"].ToString();
        if (string.IsNullOrWhiteSpace(token))
        {
            var authorization = context.Request.Headers.Authorization.ToString();
            const string bearer = "Bearer ";
            if (authorization.StartsWith(bearer, StringComparison.OrdinalIgnoreCase))
            {
                token = authorization[bearer.Length..];
            }
        }

        if (!token.StartsWith("integration|", StringComparison.Ordinal))
        {
            return null;
        }

        var parts = token.Split('|', 4);
        if (parts.Length < 3 || string.IsNullOrWhiteSpace(parts[1]))
        {
            return null;
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Email, parts[1]),
            new("email", parts[1]),
            new("name", string.IsNullOrWhiteSpace(parts[2]) ? parts[1] : parts[2]),
            new("scp", parts.Length == 4 && !string.IsNullOrWhiteSpace(parts[3]) ? parts[3] : "nts-client-scope"),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "IntegrationTest"));
    }
}
