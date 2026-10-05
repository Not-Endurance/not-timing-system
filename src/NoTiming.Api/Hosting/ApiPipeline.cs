using NoTiming.Api.Features.Access;
using NoTiming.Api.Features.Account;
using NoTiming.Api.Features.Events;
using NoTiming.Api.Features.Live;
using NoTiming.Api.Features.Profile;
using NoTiming.Api.Features.Reference;
using NoTiming.Api.Features.Tenancy;
using NoTiming.Api.Features.UserSessions;
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
        else
        {
            app.UseWebAssemblyDebugging();
        }

        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseUiStaticFiles();
        app.UseMiddleware<ConnectionDiagnosticsMiddleware>();
        app.UseMiddleware<HubOriginMiddleware>();
        app.UseCors(ApiServices.CORS_POLICY_NAME);
        app.UseAuthentication();

        // Deny by default, and the header of a write: decided for every endpoint in one place, after the session is read.
        app.UseMiddleware<AccessBaselineMiddleware>();
        app.UseAuthorization();

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
        app.MapAccount();
        app.MapProfile();
        app.MapTenancy();
        app.MapEvents();
        app.MapSetups();
        app.MapEventGrants();
        app.MapAccountSearch();
        app.MapRegistrySearch();
        app.MapReference();
        app.MapUserSessions();
        app.MapHub<LiveHub>(ApplicationConstants.LIVE_HUB).RequireCors(ApiServices.CORS_POLICY_NAME);

        // An unknown API route is a 404 in the error format of the rest-api skill, never the Ui's page.
        app.Map(
            "/api/{**rest}",
            () =>
                Results.Json(
                    new
                    {
                        errors = new[]
                        {
                            new
                            {
                                status = "404",
                                code = "not-found",
                                title = "Not found",
                            },
                        },
                    },
                    statusCode: StatusCodes.Status404NotFound,
                    contentType: "application/vnd.api+json"
                )
        );

        // Deep links into the Ui. A path that looks like a file (it has an extension) that is missing stays a 404.
        app.MapFallbackToFile("{**path:nonfile}", "index.html");

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
