using Microsoft.Extensions.Localization;
using Not.Localization;
using NoTiming.Api.Features.Access;
using NoTiming.Api.Features.Live;

namespace NoTiming.Api.Hosting;

/// <summary>
/// The middleware of the host, in the order it runs, and then its endpoints (<see cref="ApiEndpoints"/>).
/// </summary>
internal static class ApiPipeline
{
    public static WebApplication UseNoTimingApi(this WebApplication app)
    {
        BindAzurePort(app);
        LocalizationHelper.Configure(app.Services.GetRequiredService<IStringLocalizer>());

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

        return app.MapNoTimingApi();
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
