using NoTiming.Api.Features.Live;
using NoTiming.Api.JsonApi;

namespace NoTiming.Api.Hosting;

/// <summary>
/// The endpoints that belong to the host and to no feature: whether it is up, and the answer to a route of the Api that does not
/// exist. Both are on the one list of public endpoints (ADR-0012).
/// </summary>
internal static class HostEndpoints
{
    public static IEndpointRouteBuilder MapHealth(this WebApplication app)
    {
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
        return app;
    }

    /// <summary>An unknown API route is a 404 in the error format of the rest-api skill, never the Ui's page.</summary>
    public static IEndpointRouteBuilder MapUnknownApiRoute(this IEndpointRouteBuilder app)
    {
        app.Map("/api/{**rest}", () => JsonApiResults.NotFound());
        return app;
    }
}
