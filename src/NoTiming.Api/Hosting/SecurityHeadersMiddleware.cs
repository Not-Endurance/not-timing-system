namespace NoTiming.Api.Hosting;

/// <summary>
/// Baseline hardening for everything the host serves. The Content-Security-Policy is report-only until the Ui has
/// been watched under it; HSTS is added by <c>UseHsts</c> outside Development.
/// </summary>
internal sealed class SecurityHeadersMiddleware
{
    // Blazor WebAssembly needs 'wasm-unsafe-eval'; MudBlazor sets inline styles. Connections may go to any HTTPS or
    // WSS origin while the Ui still talks to the legacy API on another origin.
    const string CONTENT_SECURITY_POLICY_REPORT_ONLY =
        "default-src 'self'; "
        + "script-src 'self' 'wasm-unsafe-eval'; "
        + "style-src 'self' 'unsafe-inline'; "
        + "img-src 'self' data: blob:; "
        + "font-src 'self' data:; "
        + "connect-src 'self' https: wss:; "
        + "frame-src 'self' https:; "
        + "object-src 'none'; "
        + "base-uri 'self'; "
        + "form-action 'self'; "
        + "frame-ancestors 'none'";

    readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), geolocation=(), microphone=(), payment=(), usb=()";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            headers["Content-Security-Policy-Report-Only"] = CONTENT_SECURITY_POLICY_REPORT_ONLY;
            return Task.CompletedTask;
        });

        return _next(context);
    }
}
