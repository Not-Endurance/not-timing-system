using Microsoft.Extensions.FileProviders;

namespace NC.Server.UiClient;

public class UiClientSettings
{
    // Files to serve. Null serves the application's web root.
    public IFileProvider? FileProvider { get; set; }

    // The path the client is served under, for example "ui". The client is served at /{PathPrefix}/ and the root
    // redirects there. Required.
    public string PathPrefix { get; set; } = string.Empty;

    // Headers added to every response for the client.
    public Dictionary<string, string> ResponseHeaders { get; set; } = new();

    // Open the client in the default browser once the server is listening, at the address it is bound to. Off by default.
    public bool OpenInBrowser { get; set; }
}
