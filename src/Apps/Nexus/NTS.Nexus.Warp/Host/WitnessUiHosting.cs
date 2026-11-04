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
            settings.OpenInBrowser = app.Environment.IsDevelopment();
        });

        return app;
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
