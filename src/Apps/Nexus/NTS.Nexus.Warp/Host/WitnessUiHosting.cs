using System.Diagnostics;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using NTS.Application.Contracts;

namespace NTS.Nexus.Warp.Host;

// Everything that serves the Witness WebAssembly UI from Warp lives here.
// To host the UI elsewhere, remove the UseWitnessUi() call in Warp.Start.
internal static class WitnessUiHosting
{
    public static WebApplication UseWitnessUi(this WebApplication app)
    {
        var uiFiles = ResolveUiFiles(app);

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
        if (uiFiles != app.Environment.WebRootFileProvider)
        {
            app.UseStaticFiles(new StaticFileOptions { FileProvider = uiFiles, ServeUnknownFileTypes = true });
        }
        else
        {
            app.UseBlazorFrameworkFiles();
            app.UseStaticFiles();
        }

        app.MapFallback(context => ServeIndex(context, uiFiles));

        if (app.Environment.IsDevelopment())
        {
            var logger = app
                .Services.GetRequiredService<ILoggerFactory>()
                .CreateLogger("NTS.Nexus.Warp.Host.WitnessUi");
            app.Lifetime.ApplicationStarted.Register(() => OpenLocalUi(logger));
        }

        return app;
    }

    // Development only: open the UI in the default browser once Warp is listening.
    // Done here rather than in launchSettings, because `dotnet run` ignores launchBrowser.
    // Logs what it does, so a failed launch shows up in the IDE output instead of being silent.
    static void OpenLocalUi(ILogger logger)
    {
        var port = Environment.GetEnvironmentVariable("PORT");
        if (string.IsNullOrWhiteSpace(port))
        {
            logger.LogWarning("Not opening the Witness UI: the PORT variable is not set.");
            return;
        }

        var url = $"http://localhost:{port}/";
        logger.LogInformation("Opening the Witness UI at {Url}.", url);
        try
        {
            using var process = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            if (process is null)
            {
                logger.LogInformation(
                    "The system reported no new browser process for {Url}; a tab may already be open.",
                    url
                );
            }
        }
        catch (Exception exception)
        {
            // No desktop browser or no handler for http URLs (for example a headless machine). Warp keeps running.
            logger.LogWarning(exception, "Could not open a browser for {Url}.", url);
        }
    }

    // Development only: WITNESS_UI_PATH serves a locally published UI instead of this project's wwwroot.
    static IFileProvider ResolveUiFiles(WebApplication app)
    {
        var localUi = Environment.GetEnvironmentVariable("WITNESS_UI_PATH");
        if (app.Environment.IsDevelopment() && !string.IsNullOrWhiteSpace(localUi))
        {
            return new PhysicalFileProvider(Path.GetFullPath(localUi, app.Environment.ContentRootPath));
        }

        return app.Environment.WebRootFileProvider;
    }

    static bool IsServerPath(PathString path)
    {
        return path.StartsWithSegments("/api")
            || path.StartsWithSegments("/healthz")
            || path.StartsWithSegments($"/{ApplicationConstants.JUDGE_HUB}")
            || path.StartsWithSegments($"/{ApplicationConstants.WITNESS_HUB}");
    }

    static async Task ServeIndex(HttpContext context, IFileProvider uiFiles)
    {
        var isReadRequest = HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method);
        if (!isReadRequest || IsServerPath(context.Request.Path))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var index = uiFiles.GetFileInfo("index.html");
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
