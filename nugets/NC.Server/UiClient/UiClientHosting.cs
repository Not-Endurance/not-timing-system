using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

namespace NC.Server.UiClient;

internal static class UiClientHosting
{
    static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public static void Use(WebApplication app, UiClientSettings settings)
    {
        var prefix = UiPath.Normalize(settings.PathPrefix);
        var files = settings.FileProvider ?? app.Environment.WebRootFileProvider;
        var headers = settings.ResponseHeaders.ToArray();
        var index = new IndexPage(files, prefix);

        app.Use(
            async (context, next) =>
            {
                var path = context.Request.Path;
                if (path.Value == "/" || path.Value == prefix)
                {
                    context.Response.Redirect(prefix + "/");
                    return;
                }

                if (!path.StartsWithSegments(prefix, out var remaining))
                {
                    await next();
                    return;
                }

                foreach (var header in headers)
                {
                    context.Response.Headers[header.Key] = header.Value;
                }

                if (remaining.Value == "/index.html")
                {
                    await index.WriteAsync(context);
                    return;
                }

                if (await TryServePrecompressedAsync(context, files, remaining))
                {
                    return;
                }

                await next();
            }
        );

        app.UseStaticFiles(
            new StaticFileOptions
            {
                FileProvider = files,
                RequestPath = prefix,
                ServeUnknownFileTypes = true,
            }
        );
        app.MapFallback(prefix + "/{*path}", context => index.WriteAsync(context));

        if (settings.OpenInBrowser)
        {
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("NC.Server.UiClient");
            app.Lifetime.ApplicationStarted.Register(() => BrowserLauncher.Open(app, logger, prefix + "/"));
        }
    }

    static async Task<bool> TryServePrecompressedAsync(HttpContext context, IFileProvider files, PathString remaining)
    {
        if (!IsReadRequest(context) || remaining.Value is null)
        {
            return false;
        }

        var relativePath = remaining.Value.TrimStart('/');
        if (relativePath.Length == 0 || relativePath.EndsWith('/'))
        {
            return false;
        }

        var original = files.GetFileInfo(relativePath);
        if (!original.Exists || original.IsDirectory)
        {
            return false;
        }

        var variant = PrecompressedEncoding.Select(
            context.Request.Headers.AcceptEncoding.ToString(),
            extension => files.GetFileInfo(relativePath + extension) is { Exists: true, IsDirectory: false }
        );
        if (variant is null)
        {
            return false;
        }

        var file = files.GetFileInfo(relativePath + variant.Extension);
        context.Response.Headers["Content-Encoding"] = variant.Encoding;
        context.Response.Headers["Vary"] = "Accept-Encoding";
        context.Response.ContentType = ContentTypes.TryGetContentType(relativePath, out var contentType)
            ? contentType
            : "application/octet-stream";
        context.Response.ContentLength = file.Length;

        await using var stream = file.CreateReadStream();
        await stream.CopyToAsync(context.Response.Body);
        return true;
    }

    static bool IsReadRequest(HttpContext context)
    {
        return HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method);
    }

    // The client's index.html with its base element pointed at the prefix. Read once, on first use.
    sealed class IndexPage
    {
        readonly IFileProvider _files;
        readonly string _prefix;
        string? _html;

        public IndexPage(IFileProvider files, string prefix)
        {
            _files = files;
            _prefix = prefix;
        }

        public async Task WriteAsync(HttpContext context)
        {
            var html = IsReadRequest(context) ? await LoadAsync() : null;
            if (html is null)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(html);
        }

        async Task<string?> LoadAsync()
        {
            if (_html is not null)
            {
                return _html;
            }

            var file = _files.GetFileInfo("index.html");
            if (!file.Exists)
            {
                return null;
            }

            using var reader = new StreamReader(file.CreateReadStream());
            _html = BaseHref.Rewrite(await reader.ReadToEndAsync(), _prefix);
            return _html;
        }
    }
}
