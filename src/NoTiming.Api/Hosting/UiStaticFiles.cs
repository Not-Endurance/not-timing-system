using System.Text.RegularExpressions;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Net.Http.Headers;

namespace NoTiming.Api.Hosting;

/// <summary>
/// Serves the Ui. A published Ui ships every file also as .br and .gz. A client that accepts one of them gets it, with
/// the content type of the original file, instead of tens of megabytes of WebAssembly. The fingerprinted framework
/// files never change under their name, so they are cacheable for good.
/// </summary>
internal static partial class UiStaticFiles
{
    static readonly string[] COMPRESSED_SUFFIXES = [".br", ".gz"];

    public static WebApplication UseUiStaticFiles(this WebApplication app)
    {
        var contentTypes = new FileExtensionContentTypeProvider();
        contentTypes.Mappings[".dat"] = "application/octet-stream"; // ICU data of the .NET runtime

        app.UseMiddleware<PrecompressedFilesMiddleware>();
        app.UseStaticFiles(
            new StaticFileOptions
            {
                ContentTypeProvider = new PrecompressedContentTypeProvider(contentTypes),
                OnPrepareResponse = context =>
                {
                    var path = OriginalPath(context.Context.Request.Path.Value);
                    if (
                        path.StartsWith("/_framework/", StringComparison.OrdinalIgnoreCase)
                        && Fingerprinted().IsMatch(path)
                    )
                    {
                        context.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
                    }
                },
            }
        );

        return app;
    }

    static string OriginalPath(string? path)
    {
        path ??= "";
        foreach (var suffix in COMPRESSED_SUFFIXES)
        {
            if (path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return path[..^suffix.Length];
            }
        }

        return path;
    }

    // name.<ten characters of hash>.extension, as the Blazor build names its framework files
    [GeneratedRegex(@"\.[a-z0-9]{10}\.[a-z]+$", RegexOptions.IgnoreCase)]
    private static partial Regex Fingerprinted();

    sealed class PrecompressedContentTypeProvider : IContentTypeProvider
    {
        readonly IContentTypeProvider _inner;

        public PrecompressedContentTypeProvider(IContentTypeProvider inner)
        {
            _inner = inner;
        }

        public bool TryGetContentType(string subpath, out string contentType)
        {
            return _inner.TryGetContentType(OriginalPath(subpath), out contentType!);
        }
    }

    sealed class PrecompressedFilesMiddleware
    {
        static readonly (string Token, string Suffix)[] ENCODINGS = [("br", ".br"), ("gzip", ".gz")];

        readonly RequestDelegate _next;
        readonly IFileProvider _files;

        public PrecompressedFilesMiddleware(RequestDelegate next, IWebHostEnvironment environment)
        {
            _next = next;
            _files = environment.WebRootFileProvider;
        }

        public Task InvokeAsync(HttpContext context)
        {
            var request = context.Request;
            if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
            {
                return _next(context);
            }

            var path = request.Path.Value ?? "";
            if (OriginalPath(path) != path)
            {
                // The compressed files are only reachable through content negotiation.
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return Task.CompletedTask;
            }

            if (!Path.HasExtension(path))
            {
                return _next(context);
            }

            var accepted = request.GetTypedHeaders().AcceptEncoding;
            foreach (var (token, suffix) in ENCODINGS)
            {
                var acceptsEncoding = accepted.Any(x =>
                    x.Quality != 0 && string.Equals(x.Value.Value, token, StringComparison.OrdinalIgnoreCase)
                );
                if (!acceptsEncoding || !_files.GetFileInfo(path + suffix).Exists)
                {
                    continue;
                }

                context.Response.Headers.ContentEncoding = token;
                context.Response.Headers.Append(HeaderNames.Vary, HeaderNames.AcceptEncoding);
                request.Path = path + suffix;
                break;
            }

            return _next(context);
        }
    }
}
