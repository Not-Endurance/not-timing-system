using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Not.Server.UiClient;

// Opens the UI in the default browser. Failures are logged, not thrown, so a headless machine keeps running.
internal static class BrowserLauncher
{
    public static void Open(WebApplication app, ILogger logger)
    {
        var addresses =
            app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses
            ?? Array.Empty<string>();
        var url = BrowserAddress.From(addresses);
        if (url is null)
        {
            logger.LogWarning("Not opening the UI in a browser: the server reports no address.");
            return;
        }

        logger.LogInformation("Opening the UI at {Url}.", url);
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
            logger.LogWarning(exception, "Could not open a browser for {Url}.", url);
        }
    }
}
