using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace Not.Server.UiClient;

internal static class UiClientHosting
{
    static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public static void Use(WebApplication app, UiClientSettings settings)
    {
        var files = settings.FileProvider ?? app.Environment.WebRootFileProvider;
        var reserved = settings.ReservedPathPrefixes.Select(ToPathString).ToArray();
        var headers = settings.ResponseHeaders.ToArray();

        app.Use(
            async (context, next) =>
            {
                if (!IsReserved(context.Request.Path, reserved))
                {
                    foreach (var header in headers)
                    {
                        context.Response.Headers[header.Key] = header.Value;
                    }
                }

                await next();
            }
        );

        app.Use(
            async (context, next) =>
            {
                if (!await TryServePrecompressedAsync(context, files, reserved))
                {
                    await next();
                }
            }
        );

        app.UseStaticFiles(new StaticFileOptions { FileProvider = files, ServeUnknownFileTypes = true });
        app.MapFallback(context => ServeIndexAsync(context, files, reserved));
    }

    static async Task<bool> TryServePrecompressedAsync(HttpContext context, IFileProvider files, PathString[] reserved)
    {
        if (!IsReadRequest(context) || IsReserved(context.Request.Path, reserved))
        {
            return false;
        }

        var relativePath = context.Request.Path.Value!.TrimStart('/');
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

    static async Task ServeIndexAsync(HttpContext context, IFileProvider files, PathString[] reserved)
    {
        if (!IsReadRequest(context) || IsReserved(context.Request.Path, reserved))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var index = files.GetFileInfo("index.html");
        if (!index.Exists)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        await using var stream = index.CreateReadStream();
        await stream.CopyToAsync(context.Response.Body);
    }

    static bool IsReadRequest(HttpContext context)
    {
        return HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method);
    }

    static bool IsReserved(PathString path, PathString[] reserved)
    {
        return reserved.Any(prefix => path.StartsWithSegments(prefix));
    }

    static PathString ToPathString(string prefix)
    {
        return new PathString(prefix.StartsWith('/') ? prefix : "/" + prefix);
    }
}
