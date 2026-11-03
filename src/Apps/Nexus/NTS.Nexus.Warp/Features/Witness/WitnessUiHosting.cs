using NTS.Application.Contracts;

namespace NTS.Nexus.Warp.Features.Witness;

// Everything that serves the Witness WebAssembly UI from Warp lives here.
// To host the UI elsewhere, remove the UseWitnessUi() call in Warp.Start.
internal static class WitnessUiHosting
{
    public static WebApplication UseWitnessUi(this WebApplication app)
    {
        app.Use(
            async (context, next) =>
            {
                if (!IsServerPath(context.Request.Path))
                {
                    context.Response.Headers["Blazor-Environment"] = app.Environment.EnvironmentName;
                }

                await next();
            }
        );
        app.UseBlazorFrameworkFiles();
        app.UseStaticFiles();
        app.MapFallback(context => ServeIndex(context, app.Environment));

        return app;
    }

    static bool IsServerPath(PathString path)
    {
        return path.StartsWithSegments("/api")
            || path.StartsWithSegments("/healthz")
            || path.StartsWithSegments($"/{ApplicationConstants.JUDGE_HUB}")
            || path.StartsWithSegments($"/{ApplicationConstants.WITNESS_HUB}");
    }

    static async Task ServeIndex(HttpContext context, IWebHostEnvironment environment)
    {
        var isReadRequest = HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method);
        if (!isReadRequest || IsServerPath(context.Request.Path))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var index = environment.WebRootFileProvider.GetFileInfo("index.html");
        if (!index.Exists)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        await using var stream = index.CreateReadStream();
        await stream.CopyToAsync(context.Response.Body);
    }
}
