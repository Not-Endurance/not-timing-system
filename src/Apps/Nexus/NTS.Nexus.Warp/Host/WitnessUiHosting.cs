using System.Diagnostics;
using Microsoft.Extensions.FileProviders;
using Not.Server.UiClient;
using NTS.Application.Contracts;

namespace NTS.Nexus.Warp.Host;

// Hosts the Witness WebAssembly UI from Warp. The serving itself lives in Not.Server (ServeUiClient); this
// class only decides what Warp serves. To host the UI elsewhere, remove the UseWitnessUi() call in Warp.Start.
internal static class WitnessUiHosting
{
    public static WebApplication UseWitnessUi(this WebApplication app)
    {
        var localUi = ResolveLocalUi(app);

        app.ServeUiClient(settings =>
        {
            settings.FileProvider = localUi;
            settings.ReservedPathPrefixes =
            [
                "/api",
                "/healthz",
                $"/{ApplicationConstants.JUDGE_HUB}",
                $"/{ApplicationConstants.WITNESS_HUB}",
            ];
            settings.ResponseHeaders["Blazor-Environment"] = app.Environment.EnvironmentName;
        });

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
    static IFileProvider? ResolveLocalUi(WebApplication app)
    {
        var localUi = Environment.GetEnvironmentVariable("WITNESS_UI_PATH");
        if (app.Environment.IsDevelopment() && !string.IsNullOrWhiteSpace(localUi))
        {
            return new PhysicalFileProvider(Path.GetFullPath(localUi, app.Environment.ContentRootPath));
        }

        return null;
    }
}
