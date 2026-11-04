using Microsoft.Extensions.FileProviders;

namespace Not.Server.UiClient;

public class UiClientSettings
{
    // Files to serve. Null serves the application's web root.
    public IFileProvider? FileProvider { get; set; }

    // Request paths under these prefixes never receive the client's index.html; they get 404 instead, so server
    // endpoints keep their own responses. Prefixes match whole path segments, for example "/api".
    public string[] ReservedPathPrefixes { get; set; } = [];

    // Headers added to every response for the client, excluding reserved paths.
    public Dictionary<string, string> ResponseHeaders { get; set; } = new();}
