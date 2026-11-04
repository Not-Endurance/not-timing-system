using Microsoft.AspNetCore.Builder;

namespace Not.Server.UiClient;

public static class UiClientExtensions
{
    // Serves a single-page client (for example a Blazor WebAssembly build) from this application.
    // Precompressed files (.br, .gz) are sent when the browser accepts them. Requests under the prefix that match no file
    // get the client's index.html, so client-side routes resolve. The root redirects to the prefix.
    public static WebApplication ServeUiClient(this WebApplication app, Action<UiClientSettings>? configure = null)
    {
        var settings = new UiClientSettings();
        configure?.Invoke(settings);
        UiClientHosting.Use(app, settings);
        return app;
    }
}
