using Microsoft.Extensions.FileProviders;
using NC.Server.UiClient;
using NoTiming.Application.Contracts;

namespace NoTiming.Host;

// Hosts the Ui WebAssembly client from NoTiming. The serving itself lives in NC.Server (ServeUiClient); this
// class only decides what NoTiming serves. To host the UI elsewhere, remove the UseUi() call in NoTimingHost.Start.
internal static class UiHosting
{
    public static WebApplication UseUi(this WebApplication app)
    {
        var localUi = ResolveLocalUi(app);

        app.ServeUiClient(settings =>
        {
            settings.FileProvider = localUi;
            settings.PathPrefix = ApplicationConstants.UI;
            settings.ResponseHeaders["Blazor-Environment"] = app.Environment.EnvironmentName;
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
