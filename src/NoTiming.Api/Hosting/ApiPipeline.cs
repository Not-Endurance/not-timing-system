using NoTiming.Api.Features.Live;
using NTS.Contracts;

namespace NoTiming.Api.Hosting;

internal static class ApiPipeline
{
    public static WebApplication UseNoTimingApi(this WebApplication app)
    {
        BindAzurePort(app);

        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
            app.UseHttpsRedirection();
        }

        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseMiddleware<ConnectionDiagnosticsMiddleware>();
        app.UseMiddleware<HubOriginMiddleware>();
        app.UseCors(ApiServices.CORS_POLICY_NAME);

        app.MapGet(
            "/healthz",
            () =>
                Results.Ok(
                    new
                    {
                        status = "ok",
                        environment = app.Environment.EnvironmentName,
                        instanceId = ConnectionDiagnostics.GetInstanceId(),
                        hub = ConnectionDiagnostics.HubPath.Value,
                    }
                )
        );
        app.MapHub<LiveHub>(ApplicationConstants.LIVE_HUB).RequireCors(ApiServices.CORS_POLICY_NAME);

        return app;
    }

    static void BindAzurePort(WebApplication app)
    {
        var port = Environment.GetEnvironmentVariable("PORT"); // Supplied by Azure App Service
        if (string.IsNullOrWhiteSpace(port))
        {
            return;
        }

        app.Urls.Add($"http://*:{port}");
        app.Logger.LogInformation(
            "Api starting in {Environment}. InstanceId {InstanceId}, Port {Port}, Hub {Hub}.",
            app.Environment.EnvironmentName,
            ConnectionDiagnostics.GetInstanceId(),
            port,
            ConnectionDiagnostics.HubPath.Value
        );
    }
}
